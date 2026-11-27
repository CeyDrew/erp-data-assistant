using ErpAssistant.Application.Customers;
using ErpAssistant.Application.Inventory;
using ErpAssistant.Application.Invoices;
using ErpAssistant.Application.Orders;
using ErpAssistant.Application.Products;
using ErpAssistant.Application.Reports;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ErpAssistant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<OrderService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<ReportService>();

        return services;
    }
}
