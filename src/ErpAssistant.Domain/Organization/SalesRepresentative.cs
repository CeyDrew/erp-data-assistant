using ErpAssistant.Domain.Common;
using ErpAssistant.Domain.Sales;

namespace ErpAssistant.Domain.Organization;

public sealed class SalesRepresentative : Entity
{
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public string? Region { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public List<Customer> Customers { get; set; } = [];
}
