using System.Net;
using System.Net.Http.Json;
using ErpAssistant.Application.Common;
using ErpAssistant.Application.Customers;
using ErpAssistant.Application.Invoices;
using ErpAssistant.Application.Orders;
using ErpAssistant.Application.Products;
using ErpAssistant.Application.Reports;
using ErpAssistant.Domain.Sales;
using ErpAssistant.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace ErpAssistant.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ErpEndpointsTests(ErpApiFactory factory)
{
    private readonly HttpClient _client = factory.Admin;

    [Fact]
    public async Task Health_endpoint_is_healthy()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Products_are_paged()
    {
        var page = await _client.GetJsonAsync<PagedResult<ProductListItem>>("/api/v1/products?page=2&pageSize=10");

        page.Items.Should().HaveCount(10);
        page.Page.Should().Be(2);
        page.TotalCount.Should().Be(60);
        page.TotalPages.Should().Be(6);
    }

    [Fact]
    public async Task Page_size_above_limit_returns_validation_problem()
    {
        var response = await _client.GetAsync("/api/v1/products?pageSize=500");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("PageSize");
    }

    [Fact]
    public async Task Missing_order_returns_problem_details_404()
    {
        var response = await _client.GetAsync("/api/v1/orders/987654");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("987654");
    }

    [Fact]
    public async Task Customer_search_is_turkish_case_insensitive()
    {
        var all = await _client.GetJsonAsync<PagedResult<CustomerListItem>>("/api/v1/customers?pageSize=100");
        var withDotted = all.Items.FirstOrDefault(c => c.City.StartsWith('İ'));
        if (withDotted is null) return; // tohum verisi bu şehri üretmediyse anlamsız

        var lower = withDotted.City.Replace("İ", "i").ToLowerInvariant();
        var found = await _client.GetJsonAsync<PagedResult<CustomerListItem>>($"/api/v1/customers?city={Uri.EscapeDataString(lower)}&pageSize=100");

        found.Items.Should().Contain(c => c.Id == withDotted.Id);
    }

    [Fact]
    public async Task Order_detail_lines_sum_to_total()
    {
        var page = await _client.GetJsonAsync<PagedResult<OrderListItem>>("/api/v1/orders?status=Delivered&pageSize=1");
        var order = await _client.GetJsonAsync<OrderDetail>($"/api/v1/orders/{page.Items[0].Id}");

        order.Lines.Should().NotBeEmpty();
        order.Lines.Sum(l => l.LineTotal).Should().Be(order.TotalAmount);
        order.InvoiceNumber.Should().NotBeNull();
    }

    [Fact]
    public async Task Open_orders_filter_returns_only_draft_or_confirmed()
    {
        var page = await _client.GetJsonAsync<PagedResult<OrderListItem>>("/api/v1/orders?openOnly=true&pageSize=100");

        page.Items.Should().OnlyContain(o => o.Status == OrderStatus.Draft || o.Status == OrderStatus.Confirmed);
    }

    [Fact]
    public async Task Overdue_invoices_have_positive_days_and_outstanding()
    {
        var page = await _client.GetJsonAsync<PagedResult<InvoiceListItem>>("/api/v1/invoices?overdueOnly=true&pageSize=50");

        page.Items.Should().OnlyContain(i => i.DaysOverdue > 0 && i.Outstanding > 0);
    }

    [Fact]
    public async Task Top_products_are_ranked_by_revenue()
    {
        var rows = await _client.GetJsonAsync<List<TopProductRow>>("/api/v1/reports/top-products?top=5&from=2024-06-01");

        rows.Should().HaveCount(5);
        rows.Select(r => r.Revenue).Should().BeInDescendingOrder();
        rows.Select(r => r.Rank).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task Sales_summary_by_month_adds_up_to_total()
    {
        var summary = await _client.GetJsonAsync<SalesSummary>("/api/v1/reports/sales-summary?from=2024-06-01&groupBy=Month");

        summary.Rows.Sum(r => r.Revenue).Should().Be(summary.Revenue);
        summary.Rows.Sum(r => r.OrderCount).Should().Be(summary.OrderCount);
    }

    [Fact]
    public async Task Reversed_date_range_is_rejected()
    {
        var response = await _client.GetAsync("/api/v1/reports/sales-summary?from=2026-05-01&to=2026-01-01");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
