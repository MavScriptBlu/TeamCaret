using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CCOS.Data;
using CCOS.Data.Entities;
using CCOS.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CCOS.Api.Tests;

public sealed class OfficerOrdersControllerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private static readonly string TestSecret = $"T{Guid.NewGuid():N}!a1";
    private readonly WebApplicationFactory<Program> _baseFactory;
    private WebApplicationFactory<Program> _factory = null!;
    private SqliteConnection _connection = null!;
    private HttpClient _officerClient = null!;
    private int _orderId;
    private int _paidOrderId;
    private int _merchandiseId;
    private int _eventId;
    private DateTime _orderDateUtc;

    public OfficerOrdersControllerTests(WebApplicationFactory<Program> factory)
    {
        _baseFactory = factory;
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Testing", "true");
            builder.ConfigureTestServices(services =>
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection)));
        });

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        _officerClient = _factory.CreateClient();
        await TestAccounts.SignInAsOfficerAsync(_factory, _officerClient);
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SeedOrdersAsync(db);
        }
    }

    public async Task DisposeAsync()
    {
        _officerClient.Dispose();
        await _factory.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task List_returns_purchase_snapshot_and_filters_by_status()
    {
        List<OfficerOrderDto>? orders = await _officerClient.GetFromJsonAsync<List<OfficerOrderDto>>("/api/orders");

        Assert.NotNull(orders);
        Assert.Equal(2, orders.Count);
        OfficerOrderDto order = Assert.Single(orders, item => item.OrderId == _orderId);
        Assert.Equal("Test Officer", order.CustomerName);
        Assert.Equal(DateTime.SpecifyKind(_orderDateUtc, DateTimeKind.Unspecified), order.OrderDateUtc);
        Assert.Equal(50m, order.TotalAmount);
        Assert.Equal(OrderStatuses.Pending, order.Status);
        Assert.Contains(order.Items, item => item.ProductName == "Saved shirt"
            && item.Quantity == 3 && item.UnitPrice == 10m && item.LineTotal == 30m);
        Assert.Contains(order.Items, item => item.ProductName == "Saved admission"
            && item.Quantity == 2 && item.UnitPrice == 10m && item.LineTotal == 20m);

        List<OfficerOrderDto>? paidOrders = await _officerClient.GetFromJsonAsync<List<OfficerOrderDto>>("/api/orders?status=Paid");
        Assert.NotNull(paidOrders);
        Assert.Equal(_paidOrderId, Assert.Single(paidOrders).OrderId);

        using HttpResponseMessage invalidFilter = await _officerClient.GetAsync("/api/orders?status=Unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_allows_pending_to_paid_and_paid_to_picked_up()
    {
        OfficerOrderDto pending = await GetOrderAsync(_orderId);
        using HttpResponseMessage paidResponse = await ChangeStatusAsync(_orderId, OrderStatuses.Paid, pending.RowVersion);
        Assert.Equal(HttpStatusCode.OK, paidResponse.StatusCode);
        OfficerOrderDto? paid = await paidResponse.Content.ReadFromJsonAsync<OfficerOrderDto>();
        Assert.NotNull(paid);
        Assert.Equal(OrderStatuses.Paid, paid.Status);

        using HttpResponseMessage pickupResponse = await ChangeStatusAsync(_orderId, OrderStatuses.PickedUp, paid.RowVersion);
        Assert.Equal(HttpStatusCode.OK, pickupResponse.StatusCode);
        OfficerOrderDto? pickedUp = await pickupResponse.Content.ReadFromJsonAsync<OfficerOrderDto>();
        Assert.NotNull(pickedUp);
        Assert.Equal(OrderStatuses.PickedUp, pickedUp.Status);
    }

    [Theory]
    [InlineData(OrderStatuses.Pending)]
    [InlineData(OrderStatuses.Paid)]
    public async Task Cancel_returns_merchandise_and_seats_once(string currentStatus)
    {
        int orderId = currentStatus == OrderStatuses.Pending ? _orderId : _paidOrderId;
        OfficerOrderDto before = await GetOrderAsync(orderId);
        using HttpResponseMessage first = await ChangeStatusAsync(orderId, OrderStatuses.Canceled, before.RowVersion);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(5, await LoadMerchandiseStockAsync());
        Assert.Equal(2, await LoadTicketsSoldAsync());

        using HttpResponseMessage second = await ChangeStatusAsync(orderId, OrderStatuses.Canceled, before.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(5, await LoadMerchandiseStockAsync());
        Assert.Equal(2, await LoadTicketsSoldAsync());
    }

    [Theory]
    [InlineData(OrderStatuses.Canceled, OrderStatuses.Paid)]
    [InlineData(OrderStatuses.PickedUp, OrderStatuses.Canceled)]
    [InlineData(OrderStatuses.Pending, OrderStatuses.PickedUp)]
    public async Task ChangeStatus_rejects_illegal_transition(string initialStatus, string nextStatus)
    {
        int orderId = initialStatus == OrderStatuses.Pending ? _orderId : _paidOrderId;
        OfficerOrderDto current = await GetOrderAsync(orderId);
        if (initialStatus == OrderStatuses.Canceled)
        {
            using HttpResponseMessage canceled = await ChangeStatusAsync(orderId, OrderStatuses.Canceled, current.RowVersion);
            Assert.Equal(HttpStatusCode.OK, canceled.StatusCode);
            current = await GetOrderAsync(orderId);
        }
        else if (initialStatus == OrderStatuses.PickedUp)
        {
            using HttpResponseMessage pickedUp = await ChangeStatusAsync(orderId, OrderStatuses.PickedUp, current.RowVersion);
            Assert.Equal(HttpStatusCode.OK, pickedUp.StatusCode);
            current = await GetOrderAsync(orderId);
        }

        using HttpResponseMessage response = await ChangeStatusAsync(orderId, nextStatus, current.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(initialStatus, (await GetOrderAsync(orderId)).Status);
    }

    [Fact]
    public async Task ChangeStatus_returns_not_found_for_unknown_order()
    {
        using HttpResponseMessage response = await ChangeStatusAsync(999999, OrderStatuses.Paid, 0);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Officer_policy_rejects_anonymous_and_signed_in_non_officer_requests()
    {
        using HttpClient anonymous = _factory.CreateClient();
        using HttpResponseMessage anonymousList = await anonymous.GetAsync("/api/orders");
        using HttpResponseMessage anonymousChange = await ChangeStatusAsync(
            anonymous,
            _orderId,
            OrderStatuses.Paid,
            0);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousList.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousChange.StatusCode);

        using HttpClient member = await CreateSignedInMemberAsync();
        using HttpResponseMessage memberList = await member.GetAsync("/api/orders");
        using HttpResponseMessage memberChange = await ChangeStatusAsync(member, _orderId, OrderStatuses.Paid, 0);
        Assert.Equal(HttpStatusCode.Forbidden, memberList.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, memberChange.StatusCode);
    }

    private async Task SeedOrdersAsync(AppDbContext db)
    {
        int customerId = await db.Officers.Select(officer => officer.OfficerId).SingleAsync();
        Venue venue = new() { Name = "Officer orders test venue", Capacity = 20 };
        DateTime startsAt = DateTime.UtcNow.AddDays(1);
        Event @event = new()
        {
            Venue = venue,
            Name = "Officer orders test event",
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddHours(1),
            TicketCapacity = 10,
            TicketsSold = 4,
        };
        Product merchandise = new()
        {
            Category = "Apparel",
            Name = "Current shirt name",
            Price = 20m,
            MemberPrice = 15m,
            StockQuantity = 2,
        };
        TicketType ticketType = new()
        {
            Category = "Tickets",
            Name = "Current admission name",
            Price = 10m,
            MemberPrice = 8m,
            Event = @event,
        };
        _orderDateUtc = new DateTime(2026, 11, 20, 17, 0, 0, DateTimeKind.Utc);
        Order pending = new()
        {
            CustomerId = customerId,
            OrderDate = _orderDateUtc,
            Status = OrderStatuses.Pending,
            TotalAmount = 50m,
        };
        Order paid = new()
        {
            CustomerId = customerId,
            OrderDate = _orderDateUtc.AddMinutes(-5),
            Status = OrderStatuses.Paid,
            TotalAmount = 50m,
        };
        db.Orders.AddRange(pending, paid);
        db.Products.AddRange(merchandise, ticketType);
        await db.SaveChangesAsync();

        OrderLine shirtLine = new()
        {
            Order = pending,
            ProductId = merchandise.ProductId,
            Quantity = 3,
            UnitPrice = 10m,
            ProductNameSnapshot = "Saved shirt",
        };
        OrderLine ticketLine = new()
        {
            Order = pending,
            ProductId = ticketType.ProductId,
            Quantity = 2,
            UnitPrice = 10m,
            ProductNameSnapshot = "Saved admission",
        };
        OrderLine paidShirtLine = new()
        {
            Order = paid,
            ProductId = merchandise.ProductId,
            Quantity = 3,
            UnitPrice = 10m,
            ProductNameSnapshot = "Saved shirt",
        };
        OrderLine paidTicketLine = new()
        {
            Order = paid,
            ProductId = ticketType.ProductId,
            Quantity = 2,
            UnitPrice = 10m,
            ProductNameSnapshot = "Saved admission",
        };
        db.OrderLines.AddRange(shirtLine, ticketLine, paidShirtLine, paidTicketLine);
        await db.SaveChangesAsync();

        db.TicketRegistrations.AddRange(
            new TicketRegistration { Event = @event, TicketTypeId = ticketType.ProductId, OrderLine = ticketLine, CreatedAtUtc = _orderDateUtc },
            new TicketRegistration { Event = @event, TicketTypeId = ticketType.ProductId, OrderLine = ticketLine, CreatedAtUtc = _orderDateUtc },
            new TicketRegistration { Event = @event, TicketTypeId = ticketType.ProductId, OrderLine = paidTicketLine, CreatedAtUtc = _orderDateUtc },
            new TicketRegistration { Event = @event, TicketTypeId = ticketType.ProductId, OrderLine = paidTicketLine, CreatedAtUtc = _orderDateUtc });
        await db.SaveChangesAsync();

        _orderId = pending.OrderId;
        _paidOrderId = paid.OrderId;
        _merchandiseId = merchandise.ProductId;
        _eventId = @event.EventId;
    }

    private async Task<HttpClient> CreateSignedInMemberAsync()
    {
        HttpClient client = _factory.CreateClient();
        string email = $"member-{Guid.NewGuid():N}@ccos.test";
        using HttpResponseMessage register = await client.PostAsJsonAsync(
            "/api/account/register",
            new RegisterAccountRequest("Test", "Member", email, TestSecret));
        register.EnsureSuccessStatusCode();
        using HttpResponseMessage login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = TestSecret });
        login.EnsureSuccessStatusCode();
        JsonElement body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private Task<OfficerOrderDto> GetOrderAsync(int orderId) => GetOrderAsync(_officerClient, orderId);

    private async Task<OfficerOrderDto> GetOrderAsync(HttpClient client, int orderId)
    {
        List<OfficerOrderDto>? orders = await client.GetFromJsonAsync<List<OfficerOrderDto>>("/api/orders");
        return orders?.Single(order => order.OrderId == orderId)
            ?? throw new InvalidOperationException($"Order {orderId} was not returned by the API.");
    }

    private Task<HttpResponseMessage> ChangeStatusAsync(int orderId, string status, uint rowVersion) =>
        ChangeStatusAsync(_officerClient, orderId, status, rowVersion);

    private Task<HttpResponseMessage> ChangeStatusAsync(HttpClient client, int orderId, string status, uint rowVersion) =>
        client.PutAsJsonAsync(
            $"/api/orders/{orderId}/status",
            new ChangeOrderStatusRequest(status, rowVersion));

    private async Task<int> LoadMerchandiseStockAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(product => product.ProductId == _merchandiseId)
            .Select(product => product.StockQuantity)
            .SingleAsync();
    }

    private async Task<int> LoadTicketsSoldAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Events.AsNoTracking()
            .Where(@event => @event.EventId == _eventId)
            .Select(@event => @event.TicketsSold)
            .SingleAsync();
    }
}
