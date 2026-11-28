using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.EntityFrameworkCore;

namespace CCOS.Repositories;

public sealed class OrderRepository(AppDbContext db)
{
    public async Task<IReadOnlyList<Order>> ListAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var orders = db.Orders
            .AsNoTracking()
            .Include(order => order.Customer)
            .Include(order => order.OrderLines)
            .AsQueryable();

        if (status is not null)
        {
            orders = orders.Where(order => order.Status == status);
        }

        return await orders
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.OrderId)
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderStatusChangeResult> ChangeStatusAsync(
        int orderId,
        string nextStatus,
        uint expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var order = await db.Orders
            .Include(candidate => candidate.OrderLines)
            .SingleOrDefaultAsync(candidate => candidate.OrderId == orderId, cancellationToken);

        if (order is null)
        {
            return new OrderStatusChangeResult(OrderStatusChangeOutcome.NotFound);
        }

        if (order.RowVersion != expectedRowVersion)
        {
            return new OrderStatusChangeResult(OrderStatusChangeOutcome.Conflict);
        }

        if (!order.TryTransitionTo(nextStatus))
        {
            return new OrderStatusChangeResult(OrderStatusChangeOutcome.InvalidTransition, order);
        }

        try
        {
            // Save the status first, inside the still-open transaction. PostgreSQL's xmin check
            // makes a stale concurrent save fail before any stock or seats are restored.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new OrderStatusChangeResult(OrderStatusChangeOutcome.Conflict);
        }

        if (nextStatus == OrderStatuses.Canceled)
        {
            var ticketQuantity = await RestoreMerchandiseStockAsync(order.OrderLines, cancellationToken);
            var canceledTickets = await new TicketBookingRepository(db)
                .CancelOrderRegistrationsInCurrentTransactionAsync(orderId, cancellationToken: cancellationToken);

            if (ticketQuantity != canceledTickets)
            {
                throw new InvalidOperationException(
                    $"Order {orderId} has {ticketQuantity} ticket items but {canceledTickets} purchased ticket registrations. "
                    + "Cancellation was rolled back; check the order's ticket records.");
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new OrderStatusChangeResult(OrderStatusChangeOutcome.Updated, order);
    }

    private async Task<int> RestoreMerchandiseStockAsync(
        ICollection<OrderLine> orderLines,
        CancellationToken cancellationToken)
    {
        var linesByProduct = orderLines
            .GroupBy(line => line.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Aggregate(0, (total, line) => checked(total + line.Quantity)),
            })
            .ToList();

        if (linesByProduct.Count == 0)
        {
            return 0;
        }

        var productIds = linesByProduct.Select(line => line.ProductId).ToArray();
        var productKinds = await db.Products
            .IgnoreQueryFilters()
            .Where(product => productIds.Contains(product.ProductId))
            .Select(product => new { product.ProductId, IsTicket = product is TicketType })
            .ToDictionaryAsync(product => product.ProductId, product => product.IsTicket, cancellationToken);

        if (productKinds.Count != linesByProduct.Count)
        {
            throw new InvalidOperationException("An order item no longer has a product record. Cancellation was rolled back.");
        }

        var ticketQuantity = 0;
        foreach (var line in linesByProduct)
        {
            if (productKinds[line.ProductId])
            {
                ticketQuantity = checked(ticketQuantity + line.Quantity);
                continue;
            }

            var maximumStockBeforeRestore = int.MaxValue - line.Quantity;
            var updated = await db.Products
                .IgnoreQueryFilters()
                .Where(product => product.ProductId == line.ProductId
                    && !(product is TicketType)
                    && product.StockQuantity <= maximumStockBeforeRestore)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        product => product.StockQuantity,
                        product => product.StockQuantity + line.Quantity),
                    cancellationToken);

            if (updated != 1)
            {
                throw new InvalidOperationException(
                    $"Stock for product {line.ProductId} could not be restored. Cancellation was rolled back.");
            }
        }

        return ticketQuantity;
    }
}

public enum OrderStatusChangeOutcome
{
    Updated,
    NotFound,
    InvalidTransition,
    Conflict,
}

public sealed record OrderStatusChangeResult(OrderStatusChangeOutcome Outcome, Order? Order = null);
