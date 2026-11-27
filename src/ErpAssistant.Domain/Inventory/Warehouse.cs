using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Inventory;

public sealed class Warehouse : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string City { get; set; }
}
