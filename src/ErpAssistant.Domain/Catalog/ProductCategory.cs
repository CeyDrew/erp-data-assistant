using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Catalog;

public sealed class ProductCategory : Entity
{
    public required string Name { get; set; }
    public List<Product> Products { get; set; } = [];
}
