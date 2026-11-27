using CCOS.Data;
using CCOS.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CCOS.Repositories;

/// <summary>
/// Places orders (CAR-91): merch and event tickets in one checkout. Prices come from the database,
/// merch stock goes down, seats are held through TicketBookingRepository, and one TicketRegistration
/// is saved per ticket. It's all one transaction, so if anything is out of stock or sold out,
/// nothing at all is saved. New orders are Pending: reserved online, paid in person.
/// </summary>
public sealed class OrderRepository(AppDbContext db, TicketBookingRepository ticketBookings)
{
    /// <summary>The status every new order starts in. An officer marks it paid later.</summary>
    public const string PendingStatus = "Pending";

    // ─────────────────────────────────────────────────────────────────────────
    // A customer's orders (CAR-95)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every order one customer placed, newest first, with its lines and each ticket line's
    /// registrations (so the caller knows which event a ticket is for). Read only.
    /// </summary>
    /// <param name="customerId">Whose orders (from their login). Nobody else's orders are ever returned.</param>
    /// <param name="cancellationToken">Lets the caller stop the work early.</param>
    public Task<List<Order>> ListForCustomerAsync(int customerId, CancellationToken cancellationToken = default) =>
        db.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId)
            .Include(order => order.OrderLines)
                .ThenInclude(line => line.TicketRegistrations)
            .AsSplitQuery()
            .OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.OrderId)
            .ToListAsync(cancellationToken);

    // ─────────────────────────────────────────────────────────────────────────
    // Place an order
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Places one order for a customer. Lines for the same product are added together first.
    /// Returns Placed with the saved order, or ItemUnavailable / MembersOnly naming the item that
    /// stopped it (and then nothing was saved).
    /// </summary>
    /// <param name="customerId">Who is ordering (from their login).</param>
    /// <param name="isCurrentMember">True when they're a current member today: member prices and members-only tickets.</param>
    /// <param name="lines">Product or ticket type ids and how many of each. Must not be empty.</param>
    /// <param name="cancellationToken">Lets the caller stop the work early.</param>
    public async Task<PlaceOrderResult> PlaceOrderAsync(
        int customerId,
        bool isCurrentMember,
        IEnumerable<OrderRequestLine> lines,
        CancellationToken cancellationToken = default)
    {
        // ---- Check the request ----
        // Same product twice in one request? Treat it as one line with both amounts added up.
        List<OrderRequestLine> mergedLines = lines
            .GroupBy(line => line.ProductId)
            .Select(group => new OrderRequestLine(group.Key, group.Sum(line => line.Quantity)))
            .ToList();

        if (mergedLines.Count == 0)
        {
            throw new ArgumentException("An order needs at least one line.", nameof(lines));
        }

        if (mergedLines.Any(line => line.ProductId <= 0 || line.Quantity <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(lines), "Product ids and quantities must be positive.");
        }

        // ---- Look everything up ----
        // The Products query filter skips deactivated products, so those show up as missing here.
        List<int> productIds = mergedLines.Select(line => line.ProductId).ToList();
        Dictionary<int, Product> productsById = await db.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.ProductId))
            .ToDictionaryAsync(product => product.ProductId, cancellationToken);

        // Ticket types need their event too (members-only, start time, name for messages).
        List<int> eventIds = productsById.Values
            .OfType<TicketType>()
            .Where(ticketType => ticketType.EventId is not null)
            .Select(ticketType => ticketType.EventId!.Value)
            .Distinct()
            .ToList();
        Dictionary<int, Event> eventsById = await db.Events
            .AsNoTracking()
            .Where(@event => eventIds.Contains(@event.EventId))
            .ToDictionaryAsync(@event => @event.EventId, cancellationToken);

        DateTime nowUtc = DateTime.UtcNow;
        List<PricedLine> pricedLines = [];

        foreach (OrderRequestLine line in mergedLines)
        {
            if (!productsById.TryGetValue(line.ProductId, out Product? product))
            {
                return PlaceOrderResult.Unavailable(
                    line.ProductId,
                    $"Item {line.ProductId} isn't for sale anymore. Take it out of your cart and try again.");
            }

            Event? ticketEvent = null;
            if (product is TicketType ticketType)
            {
                // A ticket with no event, or for a removed or already-started event, can't be sold.
                if (ticketType.EventId is null
                    || !eventsById.TryGetValue(ticketType.EventId.Value, out ticketEvent)
                    || !ticketEvent.IsActive
                    || ticketEvent.StartsAtUtc <= nowUtc)
                {
                    return PlaceOrderResult.Unavailable(
                        product.ProductId,
                        $"Tickets for {ticketEvent?.Name ?? product.Name} aren't on sale anymore.");
                }

                // Members-only events stay members-only, even if someone types the id in by hand.
                if (ticketEvent.MembersOnly && !isCurrentMember)
                {
                    return PlaceOrderResult.MembersOnlyRefused(
                        product.ProductId,
                        $"{ticketEvent.Name} is for current club members only.");
                }
            }

            pricedLines.Add(new PricedLine(product, ticketEvent, line.Quantity, PriceFor(product, isCurrentMember)));
        }

        // ---- Hold the stock and seats, then save (all or nothing) ----
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Merch: take the stock down one product at a time, lowest id first, so two orders always
        // lock products in the same order and can't block each other.
        foreach (PricedLine merchLine in pricedLines.Where(line => line.Event is null).OrderBy(line => line.Product.ProductId))
        {
            bool reserved = await TryTakeStockAsync(merchLine.Product.ProductId, merchLine.Quantity, cancellationToken);
            if (!reserved)
            {
                // Disposing the transaction without committing rolls back any stock taken above.
                return PlaceOrderResult.Unavailable(
                    merchLine.Product.ProductId,
                    $"{merchLine.Product.Name} doesn't have {merchLine.Quantity} left in stock.");
            }
        }

        // Tickets: one UPDATE holds the seats for every event on the order, or none of them.
        List<EventTicketReservation> seatRequests = pricedLines
            .Where(line => line.Event is not null)
            .Select(line => new EventTicketReservation(line.Event!.EventId, line.Quantity))
            .ToList();

        if (seatRequests.Count > 0)
        {
            try
            {
                await ticketBookings.ReserveTicketsForOrderAsync(seatRequests, cancellationToken);
            }
            catch (SoldOutException soldOut)
            {
                // Name the ticket line for that event so the customer knows which one to fix.
                PricedLine soldOutLine = pricedLines.First(line => line.Event?.EventId == soldOut.EventId);
                return PlaceOrderResult.Unavailable(
                    soldOutLine.Product.ProductId,
                    $"{soldOutLine.Event!.Name} doesn't have enough seats left for this order.");
            }
        }

        Order order = BuildOrder(customerId, pricedLines, nowUtc);
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return PlaceOrderResult.Placed(order);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// What one costs for this customer: the member price for current members when it's lower,
    /// otherwise the regular price. Same rule as the cart's price tags (CAR-78, CAR-92).
    /// </summary>
    public static decimal PriceFor(Product product, bool isCurrentMember) =>
        isCurrentMember && product.MemberPrice < product.Price ? product.MemberPrice : product.Price;

    /// <summary>
    /// Takes merch out of stock with one conditional UPDATE, so two buyers can't both get the last
    /// one: the stock check and the change happen together in the database. False when there isn't
    /// enough left (or the product was deactivated in the meantime).
    /// </summary>
    private async Task<bool> TryTakeStockAsync(int productId, int quantity, CancellationToken cancellationToken)
    {
        int changedRows = await db.Products
            .Where(product => product.ProductId == productId && product.StockQuantity >= quantity)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(product => product.StockQuantity, product => product.StockQuantity - quantity),
                cancellationToken);

        return changedRows == 1;
    }

    /// <summary>
    /// Builds the Pending order with its lines and one TicketRegistration per ticket. Names and
    /// prices are copied onto the lines, so later product edits never change this order.
    /// </summary>
    private static Order BuildOrder(int customerId, IReadOnlyList<PricedLine> pricedLines, DateTime nowUtc)
    {
        Order order = new()
        {
            CustomerId = customerId,
            OrderDate = nowUtc,
            Status = PendingStatus,
            TotalAmount = pricedLines.Sum(line => line.UnitPrice * line.Quantity),
        };

        foreach (PricedLine pricedLine in pricedLines)
        {
            OrderLine orderLine = new()
            {
                ProductId = pricedLine.Product.ProductId,
                Quantity = pricedLine.Quantity,
                UnitPrice = pricedLine.UnitPrice,
                ProductNameSnapshot = pricedLine.Product.Name,
            };
            order.OrderLines.Add(orderLine);

            // One registration row per seat, so each ticket can be checked in or canceled on its own.
            if (pricedLine.Event is not null)
            {
                for (int ticketNumber = 0; ticketNumber < pricedLine.Quantity; ticketNumber++)
                {
                    orderLine.TicketRegistrations.Add(new TicketRegistration
                    {
                        EventId = pricedLine.Event.EventId,
                        TicketTypeId = pricedLine.Product.ProductId,
                        Status = RegistrationStatus.Purchased,
                        CreatedAtUtc = nowUtc,
                    });
                }
            }
        }

        return order;
    }

    /// <summary>One checked line: what it is, its event for tickets, how many, and the price per item.</summary>
    private sealed record PricedLine(Product Product, Event? Event, int Quantity, decimal UnitPrice);
}

/// <summary>
/// One line handed to OrderRepository.PlaceOrderAsync: a product or ticket type id and how many.
/// </summary>
/// <param name="ProductId">The merch product id or ticket type id.</param>
/// <param name="Quantity">How many. More than zero.</param>
public sealed record OrderRequestLine(int ProductId, int Quantity);
