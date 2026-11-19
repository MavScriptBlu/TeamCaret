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
    /// <summary>
    /// POST /api/orders: place one order for the signed-in customer.
    /// 201 with the order, 400 for a bad request, 401 when not signed in,
    /// 403 when a ticket is members-only, 409 when an item is out of stock, sold out or gone.
    /// </summary>
    [HttpPost]
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

    /// <summary>The event a ticket line is for, or null for merch.</summary>
    private static int? EventIdFor(OrderLine line) => line.TicketRegistrations.FirstOrDefault()?.EventId;
}
