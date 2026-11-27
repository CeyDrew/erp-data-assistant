using System.Net;
using System.Net.Http.Json;
using ErpAssistant.Application.Field;
using ErpAssistant.Domain.Sales;
using ErpAssistant.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class FieldOrderTests(ErpApiFactory factory)
{
    private async Task<(HttpClient Client, SyncChangesResponse Data)> RepAsync()
    {
        var client = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var (_, _, pages) = await client.FullSyncAsync();
        var data = pages[0] with
        {
            Customers = pages.SelectMany(p => p.Customers).ToList(),
            Products = pages.SelectMany(p => p.Products).ToList(),
            Stock = pages.SelectMany(p => p.Stock).ToList(),
        };
        return (client, data);
    }

    private static FieldOrderRequest Request(SyncChangesResponse data, int quantity = 2, int productIndex = 0, decimal priceDelta = 0)
    {
        var product = data.Products[productIndex];
        return new FieldOrderRequest(data.Customers[0].Id, data.Warehouses[0].Id, "Mobil test",
            [new FieldOrderLineRequest(product.Id, quantity, product.UnitPrice + priceDelta, 0.05m)]);
    }

    private Task<int> CountOrdersAsync(Guid key) =>
        factory.WithDbAsync(db => db.Orders.CountAsync(o => o.ClientRequestId == key));

    [Fact]
    public async Task Same_key_and_body_returns_the_same_order_with_replay_header()
    {
        var (client, data) = await RepAsync();
        var key = Guid.NewGuid();
        var request = Request(data);

        var first = await client.PostOrderAsync(key, request);
        var second = await client.PostOrderAsync(key, request);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        first.Headers.Contains("Idempotent-Replayed").Should().BeFalse();
        second.Headers.GetValues("Idempotent-Replayed").Should().Equal("true");

        var a = (await first.Content.ReadFromJsonAsync<FieldOrderCreated>(HttpExtensions.Json))!;
        var b = (await second.Content.ReadFromJsonAsync<FieldOrderCreated>(HttpExtensions.Json))!;
        b.Order.Id.Should().Be(a.Order.Id);
        a.Order.OrderNumber.Should().MatchRegex(@"^SM-\d{4}-\d{6}$");
        a.Order.Status.Should().Be(OrderStatus.Confirmed);
        a.Order.ClientRequestId.Should().Be(key);
        a.Order.TotalAmount.Should().Be(OrderLine.CalculateTotal(2, request.Lines[0].UnitPrice, 0.05m));
        (await CountOrdersAsync(key)).Should().Be(1);

        // Yeni sipariş temsilcinin bir sonraki senkronunda gelir.
        var (_, _, pages) = await client.FullSyncAsync();
        pages.SelectMany(p => p.Orders).Should().Contain(o => o.Id == a.Order.Id && o.ClientRequestId == key);
    }

    [Fact]
    public async Task Concurrent_duplicates_create_exactly_one_order()
    {
        var (client, data) = await RepAsync();
        var key = Guid.NewGuid();
        var request = Request(data, productIndex: 1);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostOrderAsync(key, request)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        var ids = new List<int>();
        foreach (var r in responses)
            ids.Add((await r.Content.ReadFromJsonAsync<FieldOrderCreated>(HttpExtensions.Json))!.Order.Id);
        ids.Distinct().Should().ContainSingle();
        responses.Count(r => r.Headers.Contains("Idempotent-Replayed")).Should().Be(3);
        (await CountOrdersAsync(key)).Should().Be(1);
    }

    [Fact]
    public async Task Same_key_with_a_different_body_is_rejected()
    {
        var (client, data) = await RepAsync();
        var key = Guid.NewGuid();
        (await client.PostOrderAsync(key, Request(data))).StatusCode.Should().Be(HttpStatusCode.Created);

        var reused = await client.PostOrderAsync(key, Request(data, quantity: 3));

        reused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await reused.JsonAsync()).GetProperty("code").GetString().Should().Be("idempotency_key_reused");
        (await CountOrdersAsync(key)).Should().Be(1);
    }

    [Fact]
    public async Task Stale_price_returns_409_with_new_prices_and_stores_nothing()
    {
        var (client, data) = await RepAsync();
        var key = Guid.NewGuid();
        var stale = Request(data, quantity: 4, priceDelta: -0.5m);

        var response = await client.PostOrderAsync(key, stale);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.JsonAsync();
        problem.GetProperty("code").GetString().Should().Be("price_changed");
        var line = problem.GetProperty("lines")[0];
        line.GetProperty("productId").GetInt32().Should().Be(data.Products[0].Id);
        line.GetProperty("clientUnitPrice").GetDecimal().Should().Be(data.Products[0].UnitPrice - 0.5m);
        line.GetProperty("unitPrice").GetDecimal().Should().Be(data.Products[0].UnitPrice);
        problem.GetProperty("newTotal").GetDecimal().Should().Be(OrderLine.CalculateTotal(4, data.Products[0].UnitPrice, 0.05m));
        (await CountOrdersAsync(key)).Should().Be(0);

        // İstemci güncel fiyatla aynı anahtarı tekrar kullanabilir.
        (await client.PostOrderAsync(key, Request(data, quantity: 4))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Line_beyond_stock_is_kept_and_flagged_short()
    {
        var (client, data) = await RepAsync();
        var request = Request(data, quantity: 99_999, productIndex: 2);

        var response = await client.PostOrderAsync(Guid.NewGuid(), request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<FieldOrderCreated>(HttpExtensions.Json))!;
        var productId = request.Lines[0].ProductId;
        var available = data.Stock.Where(s => s.ProductId == productId && s.WarehouseId == request.WarehouseId)
            .Select(s => s.Quantity).FirstOrDefault();
        body.ShortLines.Should().ContainSingle().Which.Should().Be(new ShortLine(productId, 99_999, Math.Max(0, available)));
        body.Order.Lines.Should().ContainSingle().Which.ShortQuantity.Should().Be(99_999 - Math.Max(0, available));
    }

    [Fact]
    public async Task Missing_key_foreign_customer_and_invalid_body_are_rejected()
    {
        var (client, data) = await RepAsync();

        (await client.PostOrderAsync(null, Request(data))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var foreignCustomer = await factory.WithDbAsync(db =>
        {
            var own = data.Customers.Select(c => c.Id).ToList();
            return db.Customers.Where(c => !own.Contains(c.Id)).Select(c => c.Id).FirstAsync();
        });
        var foreign = await client.PostOrderAsync(Guid.NewGuid(), Request(data) with { CustomerId = foreignCustomer });
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var invalid = await client.PostOrderAsync(Guid.NewGuid(), Request(data) with { Lines = [] });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
