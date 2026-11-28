using System.Globalization;
using System.Net;
using System.Text.Json;
using CCOS.Shared;
using CCOS.Web.Pages.Events;
using CCOS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CCOS.Web.Pages.Officer.Orders;

/// <summary>The officer order list. The /Officer folder is protected by the Officer policy.</summary>
public sealed class IndexModel(OrdersApiClient ordersApi, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public IReadOnlyList<OfficerOrderDto> Orders { get; private set; } = [];
    public bool LoadFailed { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Status = NormalizeStatus(Status);

        try
        {
            Orders = await ordersApi.ListAsync(Status, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(ex, "Couldn't load orders from CCOS.Api.");
            LoadFailed = true;
        }
    }

    public Task<IActionResult> OnPostPayAsync(
        int id,
        string? status,
        uint? rowVersion,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, OrderStatuses.Paid, status, rowVersion, cancellationToken);

    public Task<IActionResult> OnPostPickupAsync(
        int id,
        string? status,
        uint? rowVersion,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, OrderStatuses.PickedUp, status, rowVersion, cancellationToken);

    public Task<IActionResult> OnPostCancelAsync(
        int id,
        string? status,
        uint? rowVersion,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, OrderStatuses.Canceled, status, rowVersion, cancellationToken);

    public static string FormatOrderDate(DateTime orderDateUtc) =>
        ClubTime.ToClubTime(orderDateUtc).ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);

    private async Task<IActionResult> ChangeStatusAsync(
        int id,
        string nextStatus,
        string? status,
        uint? rowVersion,
        CancellationToken cancellationToken)
    {
        string? filter = NormalizeStatus(status);
        if (id <= 0 || rowVersion is null)
        {
            TempData["OrderError"] = "The order could not be identified. Refresh the list and try again.";
            return RedirectToList(filter);
        }

        try
        {
            using HttpResponseMessage response = await ordersApi.ChangeStatusAsync(
                id,
                new ChangeOrderStatusRequest(nextStatus, rowVersion),
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                TempData["OrderMessage"] = nextStatus switch
                {
                    OrderStatuses.Paid => "Order marked as paid.",
                    OrderStatuses.PickedUp => "Order marked as picked up.",
                    OrderStatuses.Canceled => "Order canceled. Merchandise stock and event seats were returned; no payment refund was issued.",
                    _ => throw new InvalidOperationException($"Unhandled order status: {nextStatus}."),
                };
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogInformation("Order {OrderId} status change to {Status} conflicted.", id, nextStatus);
                TempData["OrderError"] = "This order changed or that status change is not allowed. Refresh the list and try again.";
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                TempData["OrderError"] = "That order could not be found. Refresh the list and try again.";
            }
            else
            {
                logger.LogWarning("Failed to change order {OrderId} to {Status}. Status code: {StatusCode}", id, nextStatus, response.StatusCode);
                TempData["OrderError"] = "The order could not be updated. Please try again.";
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "The order API could not be reached while changing order {OrderId} to {Status}", id, nextStatus);
            TempData["OrderError"] = "The orders API could not be reached. Please try again later.";
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "The order API request timed out while changing order {OrderId} to {Status}", id, nextStatus);
            TempData["OrderError"] = "The orders API request timed out. Please try again later.";
        }

        return RedirectToList(filter);
    }

    private IActionResult RedirectToList(string? status) =>
        RedirectToPage("/Officer/Orders/Index", new { status });

    private static string? NormalizeStatus(string? status) => status is
        OrderStatuses.Pending or OrderStatuses.Paid or OrderStatuses.PickedUp or OrderStatuses.Canceled
            ? status
            : null;
}
