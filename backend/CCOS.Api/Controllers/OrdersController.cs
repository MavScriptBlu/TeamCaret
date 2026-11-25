using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>Officer-only order history and status changes.</summary>
[ApiController]
[Route("api/orders")]
[Authorize(Policy = ClubPolicies.Officer)]
public sealed class OrdersController(OrderRepository orders) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (status is not null && !IsKnownStatus(status))
        {
            ModelState.AddModelError(nameof(status), "Choose Pending, Paid, Picked up or Canceled.");
            return ValidationProblem(ModelState);
        }

        var items = await orders.ListAsync(status, cancellationToken);
        return Ok(items.Select(ToDto).ToList());
    }

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<OrderDto>> ChangeStatus(
        int id,
        [FromBody] ChangeOrderStatusRequest input,
        CancellationToken cancellationToken)
    {
        if (input.Status is null || !IsKnownStatus(input.Status))
        {
            ModelState.AddModelError(nameof(input.Status), "Choose Pending, Paid, Picked up or Canceled.");
            return ValidationProblem(ModelState);
        }

        if (input.RowVersion is null)
        {
            ModelState.AddModelError(nameof(input.RowVersion), "The order version is required. Reload the order and try again.");
            return ValidationProblem(ModelState);
        }

        var result = await orders.ChangeStatusAsync(id, input.Status, input.RowVersion.Value, cancellationToken);
        switch (result.Outcome)
        {
            case OrderStatusChangeOutcome.Updated:
                return result.Order is null
                    ? throw new InvalidOperationException("An updated order was not returned by the repository.")
                    : Ok(ToDto(result.Order));

            case OrderStatusChangeOutcome.NotFound:
                return NotFound();

            case OrderStatusChangeOutcome.InvalidTransition:
                return Conflict(new ProblemDetails
                {
                    Title = "Order status change is not allowed.",
                    Detail = $"An order with status '{result.Order?.Status}' cannot be changed to '{input.Status}'.",
                    Status = StatusCodes.Status409Conflict,
                });

            case OrderStatusChangeOutcome.Conflict:
                return Conflict(new ProblemDetails
                {
                    Title = "The order was changed by someone else.",
                    Detail = "Reload the order and try again.",
                    Status = StatusCodes.Status409Conflict,
                });

            default:
                throw new InvalidOperationException($"Unhandled order status-change outcome: {result.Outcome}.");
        }
    }

    private static bool IsKnownStatus(string status) => status is
        OrderStatuses.Pending or OrderStatuses.Paid or OrderStatuses.PickedUp or OrderStatuses.Canceled;

    private static OrderDto ToDto(Order order)
    {
        var customerName = order.Customer is null
            ? null
            : $"{order.Customer.FirstName} {order.Customer.LastName}".Trim();

        return new OrderDto(
            order.OrderId,
            customerName,
            order.OrderDate,
            order.OrderLines
                .OrderBy(line => line.OrderLineId)
                .Select(line => new OrderLineDto(
                    line.ProductNameSnapshot,
                    line.Quantity,
                    line.UnitPrice,
                    line.UnitPrice * line.Quantity))
                .ToList(),
            order.TotalAmount,
            order.Status,
            order.RowVersion);
    }
}