namespace ErpAssistant.Infrastructure.Persistence.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; } = true;
    public int RandomSeed { get; set; } = 20260101;
    public int Customers { get; set; } = 200;
    public int Products { get; set; } = 500;
    public int Orders { get; set; } = 20_000;
    public int SalesRepresentatives { get; set; } = 8;
    public int Months { get; set; } = 24;
}
