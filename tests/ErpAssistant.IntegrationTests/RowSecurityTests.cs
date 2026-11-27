using System.Net;
using ErpAssistant.Application.Common;
using ErpAssistant.Application.Customers;
using ErpAssistant.Application.Orders;
using ErpAssistant.Application.Products;
using ErpAssistant.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace ErpAssistant.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class RowSecurityTests(ErpApiFactory factory)
{
    [Fact]
    public async Task Sales_rep_sees_only_own_customers()
    {
        var client = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var me = await client.GetJsonAsync<ErpAssistant.Application.Auth.UserInfo>("/api/v1/auth/me");

        var page = await client.GetJsonAsync<PagedResult<CustomerListItem>>("/api/v1/customers?pageSize=100");

        page.TotalCount.Should().BeGreaterThan(0);
        page.Items.Should().OnlyContain(c => c.SalesRepresentativeId == me.SalesRepresentativeId);
    }

    [Fact]
    public async Task Sales_rep_cannot_open_another_reps_customer_even_by_id()
    {
        var admin = await factory.CreateClientAsAsync(DemoUsers.Admin);
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var me = await rep.GetJsonAsync<ErpAssistant.Application.Auth.UserInfo>("/api/v1/auth/me");

        var all = await admin.GetJsonAsync<PagedResult<CustomerListItem>>("/api/v1/customers?pageSize=100");
        var foreign = all.Items.First(c => c.SalesRepresentativeId != me.SalesRepresentativeId);

        var response = await rep.GetAsync($"/api/v1/customers/{foreign.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var orders = await rep.GetJsonAsync<PagedResult<OrderListItem>>($"/api/v1/orders?customerId={foreign.Id}");
        orders.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Two_reps_see_disjoint_order_sets_that_sum_to_less_than_admin()
    {
        var admin = await factory.CreateClientAsAsync(DemoUsers.Admin);
        var rep1 = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var rep2 = await factory.CreateClientAsAsync(DemoUsers.OtherSalesRep);

        var all = (await admin.GetJsonAsync<PagedResult<OrderListItem>>("/api/v1/orders?pageSize=1")).TotalCount;
        var first = await rep1.GetJsonAsync<PagedResult<OrderListItem>>("/api/v1/orders?pageSize=100");
        var second = await rep2.GetJsonAsync<PagedResult<OrderListItem>>("/api/v1/orders?pageSize=100");

        first.TotalCount.Should().BeGreaterThan(0);
        (first.TotalCount + second.TotalCount).Should().BeLessThan(all);
        first.Items.Select(o => o.Id).Should().NotIntersectWith(second.Items.Select(o => o.Id));
    }

    [Fact]
    public async Task Warehouse_user_sees_stock_but_no_prices_or_invoices()
    {
        var client = await factory.CreateClientAsAsync(DemoUsers.Warehouse);

        var products = await client.GetJsonAsync<PagedResult<ProductListItem>>("/api/v1/products?pageSize=5");
        products.Items.Should().OnlyContain(p => p.UnitPrice == null);

        var orders = await client.GetJsonAsync<PagedResult<OrderListItem>>("/api/v1/orders?pageSize=5");
        orders.Items.Should().OnlyContain(o => o.TotalAmount == null);

        (await client.GetAsync("/api/v1/invoices")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/reports/sales-summary")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/stock?pageSize=5")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var client = factory.CreateClient();
        (await client.GetAsync("/api/v1/customers")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
