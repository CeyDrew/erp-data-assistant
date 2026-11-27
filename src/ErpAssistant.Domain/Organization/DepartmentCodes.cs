namespace ErpAssistant.Domain.Organization;

public static class DepartmentCodes
{
    public const string Management = "YONETIM";
    public const string Sales = "SATIS";
    public const string Warehouse = "DEPO";
    public const string Quality = "KALITE";

    public static readonly IReadOnlyList<string> All = [Management, Sales, Warehouse, Quality];
}
