using FluentValidation;
using ErpAssistant.Application.Common;

namespace ErpAssistant.Application.Customers;

public sealed record CustomerQuery : PageQuery
{
    public string? Search { get; init; }
    public string? City { get; init; }
    public int? SalesRepresentativeId { get; init; }
}

public sealed class CustomerQueryValidator : PageQueryValidator<CustomerQuery>
{
    public CustomerQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.City).MaximumLength(64);
    }
}

public sealed record CustomerListItem(
    int Id, string Code, string Name, string City, string TaxNumber, int SalesRepresentativeId, string SalesRepresentative);

public sealed record CustomerDetail(
    int Id, string Code, string Name, string City, string TaxNumber, string? Email, string? Phone, decimal CreditLimit,
    int SalesRepresentativeId, string SalesRepresentative, int OrderCount, int OpenOrderCount, decimal TotalRevenue,
    decimal Outstanding, DateTime? LastOrderDate);
