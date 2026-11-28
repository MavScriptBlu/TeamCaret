using System.Security.Claims;
using CCOS.Data.Entities;
using CCOS.Repositories;
using CCOS.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CCOS.Api.Controllers;

/// <summary>
/// Order endpoints (CAR-91). POST /api/orders is the one checkout that merch checkout (CAR-93) and
/// event RSVP (CAR-94) both call. No online payment yet: orders are Pending and paid in person.
/// Anyone signed in can order; member prices and members-only tickets need a current membership,
/// checked fresh on every request (the same rule as the Member policy, CAR-69).
/// </summary>
[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController(
    OrderRepository orders,
    AccountRepository accounts,
    MemberRepository members,
    ClubClock clock) : ControllerBase
{
    /// <summary>Lists all orders, or orders with one selected status, for officers.</summary>
    [HttpGet]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<ActionResult<IReadOnlyList<OfficerOrderDto>>> ListForOfficer(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (status is not null && !IsKnownStatus(status))
        {
            ModelState.AddModelError(nameof(status), "Choose Pending, Paid, Picked up or Canceled.");
            return ValidationProblem(ModelState);
        }

        IReadOnlyList<Order> orderList = await orders.ListForOfficerAsync(status, cancellationToken);
        return Ok(orderList.Select(ToOfficerDto).ToList());
    }

    /// <summary>Changes an order's status if the transition and supplied row version are current.</summary>
    [HttpPut("{id:int}/status")]
    [Authorize(Policy = ClubPolicies.Officer)]
    public async Task<ActionResult<OfficerOrderDto>> ChangeStatus(
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

        OrderStatusChangeResult result = await orders.ChangeStatusAsync(
            id,
            input.Status,
            input.RowVersion.Value,
            cancellationToken);

        return result.Outcome switch
        {
            OrderStatusChangeOutcome.Updated when result.Order is not null => Ok(ToOfficerDto(result.Order)),
            OrderStatusChangeOutcome.Updated => throw new InvalidOperationException("An updated order was not returned by the repository."),
            OrderStatusChangeOutcome.NotFound => NotFound(),
            OrderStatusChangeOutcome.InvalidTransition => Conflict(new ProblemDetails
            {
                Title = "Order status change is not allowed.",
                Detail = $"An order with status '{result.Order?.Status}' cannot be changed to '{input.Status}'.",
                Status = StatusCodes.Status409Conflict,
            }),
            OrderStatusChangeOutcome.Conflict => Conflict(new ProblemDetails
            {
                Title = "The order was changed by someone else.",
                Detail = "Reload the order and try again.",
                Status = StatusCodes.Status409Conflict,
            }),
            _ => throw new InvalidOperationException($"Unhandled order status-change outcome: {result.Outcome}."),
        };
    }

    /// <summary>
    /// POST /api/orders: place one order for the signed-in customer.
    /// 201 with the order, 400 for a bad request, 401 when not signed in,
    /// 403 when a ticket is members-only, 409 when an item is out of stock, sold out or gone.
    /// </summary>
    [HttpPost]
    // No anti-forgery check on purpose: the API only takes bearer tokens, never a sign-in
    // cookie (CAR-83), so a forged cross-site request has nothing to ride on. Turning the
    // check on breaks every order call, since the website and checkout send no such token.
    [IgnoreAntiforgeryToken]
    public async Task<ActionResult<OrderDto>> Place(
        [FromBody] PlaceOrderRequest input,
        CancellationToken cancellationToken)
    {
        // ---- Who is ordering ----
        // The token carries the login id. Every login has a customer, so null means the login is gone.
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? customerId = userId is null ? null : await accounts.FindCustomerIdAsync(userId, cancellationToken);
        if (customerId is null)
        {
            return Unauthorized();
        }

        bool isCurrentMember = await members.IsCurrentMemberAsync(customerId.Value, clock.Today, cancellationToken);

        // ---- Place it ----
        List<OrderRequestLine> lines = input.Lines
            .Select(line => new OrderRequestLine(line.ProductId, line.Quantity))
            .ToList();

        PlaceOrderResult result = await orders.PlaceOrderAsync(customerId.Value, isCurrentMember, lines, cancellationToken);

        // ---- Answer ----
        return result.Outcome switch
        {
            PlaceOrderOutcome.Placed => StatusCode(StatusCodes.Status201Created, ToDto(result.Order!)),
            PlaceOrderOutcome.MembersOnly => ItemProblem(result, "Members only", StatusCodes.Status403Forbidden),
            _ => ItemProblem(result, "Not available", StatusCodes.Status409Conflict),
        };
    }

    /// <summary>
    /// A problem answer that says which item stopped the order, in words and as productId,
    /// so the checkout page can point at the right cart line.
    /// </summary>
    private ObjectResult ItemProblem(PlaceOrderResult result, string title, int statusCode)
    {
        ObjectResult problem = Problem(title: title, detail: result.Message, statusCode: statusCode);
        if (problem.Value is ProblemDetails details)
        {
            details.Extensions["productId"] = result.ProblemProductId;
        }

        return problem;
    }

    /// <summary>
    /// Turns the saved order into the shape sent over the wire. A line is a ticket line when the
    /// order holds registrations for it (each registration remembers its event).
    /// </summary>
    private static OrderDto ToDto(Order order) => new(
        order.OrderId,
        order.OrderDate,
        order.Status,
        order.TotalAmount,
        order.OrderLines
            .Select(line => new OrderLineDto(
                line.ProductId,
                line.ProductNameSnapshot,
                EventIdFor(line),
                line.Quantity,
                line.UnitPrice,
                line.UnitPrice * line.Quantity))
            .ToList());

    /// <summary>Turns the saved order into the shape sent to officers. The lines are in order.</summary>
    private static OfficerOrderDto ToOfficerDto(Order order)
    {
        string? customerName = $"{order.Customer.FirstName} {order.Customer.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(customerName))
        {
            customerName = null;
        }

        return new OfficerOrderDto(
            order.OrderId,
            customerName,
            order.OrderDate,
            order.OrderLines
                .OrderBy(line => line.OrderLineId)
                .Select(line => new OfficerOrderLineDto(
                    line.ProductNameSnapshot,
                    line.Quantity,
                    line.UnitPrice,
                    line.UnitPrice * line.Quantity))
                .ToList(),
            order.TotalAmount,
            order.Status,
            order.RowVersion);
    }

    private static bool IsKnownStatus(string status) => status is
        OrderStatuses.Pending or OrderStatuses.Paid or OrderStatuses.PickedUp or OrderStatuses.Canceled;

    /// <summary>The event a ticket line is for, or null for merch.</summary>
    private static int? EventIdFor(OrderLine line) => line.TicketRegistrations.FirstOrDefault()?.EventId;
}
