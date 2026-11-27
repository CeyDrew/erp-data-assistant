using System.Net;
using ErpAssistant.Application.Auth;
using ErpAssistant.Application.Common;
using ErpAssistant.Application.Customers;
using ErpAssistant.Application.Field;
using ErpAssistant.Domain.Field;
using ErpAssistant.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class FieldSyncTests(ErpApiFactory factory)
{
    [Fact]
    public async Task First_sync_returns_only_the_reps_own_customers_and_a_plan_for_today()
    {
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var own = await rep.GetJsonAsync<PagedResult<CustomerListItem>>("/api/v1/customers?pageSize=100");

        var (cursor, _, pages) = await rep.FullSyncAsync();
        var customers = pages.SelectMany(p => p.Customers).ToList();
        var first = pages[0];

        cursor.Should().MatchRegex("^[0-9A-F]{16}$");
        customers.Select(c => c.Id).Should().BeEquivalentTo(own.Items.Select(c => c.Id));
        customers.Should().OnlyContain(c => c.Latitude != null && c.Longitude != null && c.Address != null);
        first.Warehouses.Should().NotBeEmpty();
        pages.SelectMany(p => p.Products).Should().NotBeEmpty()
            .And.OnlyContain(p => p.IsActive && p.Barcode != null && Ean13.IsValid(p.Barcode));
        pages.SelectMany(p => p.Stock).Should().NotBeEmpty();
        pages.SelectMany(p => p.Orders).Should().NotBeEmpty();
        pages.SelectMany(p => p.Deliveries).Should().BeEmpty();
        pages.SelectMany(p => p.Tombstones).Should().BeEmpty();

        var visits = pages.SelectMany(p => p.Visits).Where(v => v.PlannedDate == Today).ToList();
        visits.Should().HaveCount(6);
        visits.Select(v => v.Sequence).Should().BeEquivalentTo([1, 2, 3, 4, 5, 6]);
        visits.Select(v => v.CustomerId).Should().BeSubsetOf(own.Items.Select(c => c.Id));
    }

    [Fact]
    public async Task Paging_with_small_limit_returns_the_same_rows_as_one_page()
    {
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);

        var (_, _, whole) = await rep.FullSyncAsync();
        var (_, _, paged) = await rep.FullSyncAsync(limit: 3);

        paged.Should().HaveCountGreaterThan(1);
        paged[0].HasMore.Should().BeTrue();
        paged[^1].HasMore.Should().BeFalse();
        paged.SelectMany(p => p.Customers).Select(c => c.Id).Distinct()
            .Should().BeEquivalentTo(whole.SelectMany(p => p.Customers).Select(c => c.Id));
        paged.SelectMany(p => p.Products).Select(c => c.Id).Distinct()
            .Should().BeEquivalentTo(whole.SelectMany(p => p.Products).Select(c => c.Id));
    }

    [Fact]
    public async Task Delta_after_price_change_returns_only_that_product()
    {
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var (cursor, last, _) = await rep.FullSyncAsync();
        var target = (await rep.SyncAsync()).Products[^1];

        var empty = await rep.SyncAsync(cursor);
        empty.Products.Should().BeEmpty();
        empty.Customers.Should().BeEmpty();

        await factory.WithDbAsync(async db =>
        {
            var product = await db.Products.SingleAsync(p => p.Id == target.Id);
            product.UnitPrice += 1.25m;
            await db.SaveChangesAsync();
        });

        var delta = await rep.SyncAsync(cursor);
        delta.Products.Should().ContainSingle().Which.Should().Match<ProductSync>(p =>
            p.Id == target.Id && p.UnitPrice == target.UnitPrice + 1.25m);
        delta.Customers.Should().BeEmpty();
        string.CompareOrdinal(delta.Cursor, cursor).Should().BePositive();
        last.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Customer_reassignment_sends_a_tombstone_to_the_previous_rep()
    {
        var rep1 = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var rep2 = await factory.CreateClientAsAsync(DemoUsers.OtherSalesRep);
        var rep2Info = await rep2.GetJsonAsync<UserInfo>("/api/v1/auth/me");
        var (cursor1, _, pages1) = await rep1.FullSyncAsync();
        var (cursor2, _, _) = await rep2.FullSyncAsync();
        var moved = pages1.SelectMany(p => p.Customers).First();

        var originalRep = await factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync(c => c.Id == moved.Id);
            var previous = customer.SalesRepresentativeId;
            customer.SalesRepresentativeId = rep2Info.SalesRepresentativeId!.Value;
            await db.SaveChangesAsync();
            return previous;
        });

        try
        {
            var lost = await rep1.SyncAsync(cursor1);
            lost.Tombstones.Should().ContainEquivalentOf(new TombstoneSync(FieldTombstone.Customer, moved.Id));
            lost.Customers.Should().NotContain(c => c.Id == moved.Id);

            var gained = await rep2.SyncAsync(cursor2);
            gained.Customers.Should().Contain(c => c.Id == moved.Id);
            gained.Tombstones.Should().NotContain(t => t.Id == moved.Id);
        }
        finally
        {
            await factory.WithDbAsync(async db =>
            {
                var customer = await db.Customers.SingleAsync(c => c.Id == moved.Id);
                customer.SalesRepresentativeId = originalRep;
                await db.SaveChangesAsync();
            });
        }
    }

    [Fact]
    public async Task Driver_sees_only_own_deliveries_and_no_sales_data()
    {
        var driver1 = await factory.CreateClientAsAsync(DemoUsers.Driver);
        var driver2 = await factory.CreateClientAsAsync(DemoUsers.OtherDriver);
        var me = await driver1.GetJsonAsync<UserInfo>("/api/v1/auth/me");
        me.DriverId.Should().NotBeNull();
        me.Roles.Should().Equal("Surucu");

        var first = await driver1.SyncAsync();
        var second = await driver2.SyncAsync();

        first.Warehouses.Should().NotBeEmpty();
        first.Customers.Should().BeEmpty();
        first.Products.Should().BeEmpty();
        first.Stock.Should().BeEmpty();
        first.Orders.Should().BeEmpty();
        first.Visits.Should().BeEmpty();

        var today = first.Deliveries.Where(d => d.PlannedDate == Today).ToList();
        today.Should().HaveCount(8);
        today.Select(d => d.Sequence).Should().BeEquivalentTo(Enumerable.Range(1, 8));
        today.Should().OnlyContain(d => d.CustomerName.Length > 0 && d.Items.Count > 0 && d.Latitude != null);
        first.Deliveries.Select(d => d.Id).Should().NotIntersectWith(second.Deliveries.Select(d => d.Id));
        second.Deliveries.Should().NotBeEmpty();

        // REST tarafında sürücünün müşteri/sipariş görünürlüğü yok (mevcut erişim genişletilmedi).
        var customers = await driver1.GetJsonAsync<PagedResult<CustomerListItem>>("/api/v1/customers");
        customers.TotalCount.Should().Be(0);
        (await driver1.PostOrderAsync(Guid.NewGuid(), new FieldOrderRequest(1, 1, null, [new FieldOrderLineRequest(1, 1, 1, 0)])))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Planner_is_idempotent_for_the_same_day()
    {
        var driver = await factory.CreateClientAsAsync(DemoUsers.Driver);
        var a = await driver.SyncAsync();
        var b = await driver.SyncAsync();

        b.Deliveries.Where(d => d.PlannedDate == Today).Select(d => d.Id)
            .Should().BeEquivalentTo(a.Deliveries.Where(d => d.PlannedDate == Today).Select(d => d.Id));
    }

    [Fact]
    public async Task Invalid_cursor_or_limit_is_rejected_and_warehouse_user_is_forbidden()
    {
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        (await rep.GetAsync("/api/v1/field/sync/changes?since=not-hex")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rep.GetAsync("/api/v1/field/sync/changes?limit=5000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var warehouse = await factory.CreateClientAsAsync(DemoUsers.Warehouse);
        (await warehouse.GetAsync("/api/v1/field/sync/changes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
}
