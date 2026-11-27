using ErpAssistant.Domain.Common;

namespace ErpAssistant.Domain.Organization;

public sealed class Department : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
}
