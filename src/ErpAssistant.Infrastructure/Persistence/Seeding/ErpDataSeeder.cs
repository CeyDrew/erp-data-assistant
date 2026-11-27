using System.Diagnostics;
using Bogus;
using ErpAssistant.Domain.Billing;
using ErpAssistant.Domain.Catalog;
using ErpAssistant.Domain.Inventory;
using ErpAssistant.Domain.Organization;
using ErpAssistant.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErpAssistant.Infrastructure.Persistence.Seeding;

/// <summary>
/// Sentetik ERP verisi üretir. Sabit tohum sayesinde her kurulumda aynı veri oluşur; tarihler ise
/// bugüne göre kaydırılır ki "geçen ay" gibi sorular her zaman veri bulsun.
/// </summary>
public sealed class ErpDataSeeder(
    ErpDbContext db,
    IOptions<SeedOptions> options,
    TimeProvider clock,
    ILogger<ErpDataSeeder> logger)
{
    private const int BatchSize = 2_000;

    private static readonly string[] Categories =
    [
        "Rulman", "Vana", "Pompa", "Conta ve Sızdırmazlık", "Hidrolik Hortum", "Elektrik Motoru",
        "Sensör", "Kablo", "Bağlantı Elemanı", "Endüstriyel Boya", "Ambalaj", "Redüktör",
    ];

    private static readonly Dictionary<string, string[]> ProductStems = new()
    {
        ["Rulman"] = ["Sabit Bilyalı Rulman", "Konik Makaralı Rulman", "Eksenel Rulman", "İğneli Rulman"],
        ["Vana"] = ["Küresel Vana", "Kelebek Vana", "Çekvalf", "Sürgülü Vana", "Emniyet Ventili"],
        ["Pompa"] = ["Santrifüj Pompa", "Dişli Pompa", "Dalgıç Pompa", "Dozaj Pompası"],
        ["Conta ve Sızdırmazlık"] = ["O-Ring", "Keçe", "Klingrit Conta", "Mekanik Salmastra"],
        ["Hidrolik Hortum"] = ["Hidrolik Hortum 1SN", "Hidrolik Hortum 2SN", "Spiral Hortum", "Teflon Hortum"],
        ["Elektrik Motoru"] = ["Trifaze Motor", "Monofaze Motor", "Frenli Motor", "Servo Motor"],
        ["Sensör"] = ["İndüktif Sensör", "Basınç Transmitteri", "Sıcaklık Sensörü", "Fotoelektrik Sensör"],
        ["Kablo"] = ["NYAF Kablo", "TTR Kablo", "Kontrol Kablosu", "Halojen Free Kablo"],
        ["Bağlantı Elemanı"] = ["Altıgen Cıvata", "Somun", "Pul", "Saplama", "Dübel"],
        ["Endüstriyel Boya"] = ["Epoksi Astar", "Poliüretan Son Kat", "Antipas", "Yüksek Isı Boyası"],
        ["Ambalaj"] = ["Streç Film", "Oluklu Koli", "Palet Çemberi", "Köpük Dolgu"],
        ["Redüktör"] = ["Helisel Redüktör", "Sonsuz Vida Redüktör", "Planet Redüktör"],
    };

    private static readonly (string Code, string Name, string City)[] WarehouseSeeds =
    [
        ("IST", "İstanbul Ana Depo", "İstanbul"),
        ("ANK", "Ankara Bölge Deposu", "Ankara"),
        ("IZM", "İzmir Bölge Deposu", "İzmir"),
    ];

    private static readonly string[] Regions = ["Marmara", "İç Anadolu", "Ege", "Akdeniz", "Karadeniz"];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var o = options.Value;
        if (!o.Enabled) return;

        if (await db.Products.AnyAsync(ct))
        {
            logger.LogInformation("Veri zaten var, tohumlama atlandı");
            return;
        }

        var sw = Stopwatch.StartNew();
        Randomizer.Seed = new Random(o.RandomSeed);
        var faker = new Faker("tr");
        var today = clock.GetUtcNow().UtcDateTime.Date;
        var start = today.AddMonths(-o.Months);

        db.ChangeTracker.AutoDetectChangesEnabled = false;

        // --- Organizasyon --------------------------------------------------------------------
        var departments = new[]
        {
            new Department { Code = DepartmentCodes.Management, Name = "Yönetim" },
            new Department { Code = DepartmentCodes.Sales, Name = "Satış" },
            new Department { Code = DepartmentCodes.Warehouse, Name = "Depo ve Lojistik" },
            new Department { Code = DepartmentCodes.Quality, Name = "Kalite" },
        };
        db.Departments.AddRange(departments);

        var salesDept = departments[1];
        var reps = new List<SalesRepresentative>();
        for (var i = 0; i < o.SalesRepresentatives; i++)
        {
            var first = faker.Name.FirstName();
            var last = faker.Name.LastName();
            reps.Add(new SalesRepresentative
            {
                FullName = $"{first} {last}",
                Email = i == 0 ? "temsilci@erp.local" : $"temsilci{i + 1}@erp.local",
                Region = Regions[i % Regions.Length],
                Department = salesDept,
            });
        }
        db.SalesRepresentatives.AddRange(reps);

        var warehouses = WarehouseSeeds
            .Select(w => new Warehouse { Code = w.Code, Name = w.Name, City = w.City })
            .ToList();
        db.Warehouses.AddRange(warehouses);

        // --- Katalog -------------------------------------------------------------------------
        var categories = Categories.Select(c => new ProductCategory { Name = c }).ToList();
        db.ProductCategories.AddRange(categories);

        var products = new List<Product>();
        var usedNames = new HashSet<string>();
        for (var i = 0; i < o.Products; i++)
        {
            var category = categories[i % categories.Count];
            var stem = faker.PickRandom(ProductStems[category.Name]);
            string name;
            do name = $"{stem} {faker.Random.Replace("??-###").ToUpperInvariant()}";
            while (!usedNames.Add(name));

            // Log-uniform fiyat: ucuz ürün çok, pahalı ürün az. Metreyle satılanlar metre fiyatıdır.
            var unit = category.Name is "Kablo" or "Hidrolik Hortum" ? "metre" : "adet";
            var cost = unit == "metre" ? LogUniform(faker, 8, 180) : LogUniform(faker, 12, 3_000);
            products.Add(new Product
            {
                Sku = $"SKU-{i + 1:D5}",
                Name = name,
                Category = category,
                Unit = unit,
                UnitCost = cost,
                UnitPrice = Math.Round(cost * faker.Random.Decimal(1.18m, 1.65m), 2),
                ReorderLevel = faker.Random.Int(10, 120),
                IsActive = faker.Random.Bool(0.96f),
            });
        }
        db.Products.AddRange(products);

        // --- Müşteriler ----------------------------------------------------------------------
        var customers = new List<Customer>();
        var usedTax = new HashSet<string>();
        for (var i = 0; i < o.Customers; i++)
        {
            string tax;
            do tax = faker.Random.ReplaceNumbers("##########");
            while (!usedTax.Add(tax));

            var companyName = $"{faker.Name.LastName()} {faker.PickRandom("Makine", "Endüstri", "Mühendislik", "Otomasyon", "Yapı", "Enerji", "Gıda", "Tekstil")} " +
                              faker.PickRandom("A.Ş.", "Ltd. Şti.", "San. ve Tic. A.Ş.");
            customers.Add(new Customer
            {
                Code = $"M-{i + 1:D4}",
                Name = companyName,
                TaxNumber = tax,
                City = faker.Address.City(),
                Email = $"satinalma{i + 1}@musteri{i + 1}.example",
                Phone = faker.Phone.PhoneNumber("05## ### ## ##"),
                CreditLimit = faker.PickRandom(100_000m, 250_000m, 500_000m, 1_000_000m),
                SalesRepresentative = reps[i % reps.Count],
                CreatedAt = start.AddDays(-faker.Random.Int(0, 900)),
            });
        }
        db.Customers.AddRange(customers);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Ana veri yazıldı: {Products} ürün, {Customers} müşteri", products.Count, customers.Count);

        // --- Açılış stoğu --------------------------------------------------------------------
        var openingMovements = new List<StockMovement>();
        foreach (var product in products)
        foreach (var warehouse in warehouses)
        {
            openingMovements.Add(StockMovement.Create(product.Id, warehouse.Id, StockMovementType.Purchase,
                faker.Random.Int(300, 2_500), start.AddDays(-1), "ACILIS"));
        }
        db.StockMovements.AddRange(openingMovements);
        await db.SaveChangesAsync(ct);

        // --- Siparişler, faturalar, tahsilatlar ----------------------------------------------
        // Ürün ve müşteri seçimi ağırlıklı: az sayıda ürün satışların büyük kısmını oluşturur
        // (gerçekçi bir "en çok satanlar" dağılımı için).
        var productWeights = products.Select((_, i) => 1.0 / Math.Pow(i % 97 + 1, 0.9)).ToArray();
        var productPicker = new WeightedPicker<Product>(products, productWeights);
        var customerWeights = customers.Select((_, i) => 1.0 / Math.Pow(i % 41 + 1, 0.6)).ToArray();
        var customerPicker = new WeightedPicker<Customer>(customers, customerWeights);

        var totalDays = (today - start).Days;
        var orderDates = Enumerable.Range(0, o.Orders)
            .Select(_ => PickOrderDate(faker, start, totalDays))
            .OrderBy(d => d)
            .ToList();

        var invoiceSeq = 0;
        var orderBatch = new List<Order>(BatchSize);
        var movementBatch = new List<StockMovement>(BatchSize * 3);

        for (var i = 0; i < orderDates.Count; i++)
        {
            var orderDate = orderDates[i];
            var customer = customerPicker.Pick(faker.Random);
            var warehouse = warehouses[faker.Random.WeightedRandom([0, 1, 2], [0.55f, 0.25f, 0.20f])];
            var order = Order.Create($"SP-{orderDate:yyyy}-{i + 1:D6}", customer, warehouse.Id, orderDate);

            var lineCount = faker.Random.WeightedRandom([1, 2, 3, 4, 5, 6], [0.25f, 0.25f, 0.2f, 0.15f, 0.1f, 0.05f]);
            var chosen = new HashSet<int>();
            for (var l = 0; l < lineCount; l++)
            {
                var product = productPicker.Pick(faker.Random);
                if (!chosen.Add(product.Id)) continue;
                var qty = product.Unit == "metre"
                    ? faker.Random.Int(1, 30) * 10
                    : product.UnitPrice > 1_000 ? faker.Random.Int(1, 8) : faker.Random.Int(2, 50);
                var discount = faker.Random.WeightedRandom([0m, 0.05m, 0.10m, 0.15m], [0.6f, 0.2f, 0.15f, 0.05f]);
                order.AddLine(product, qty, discount);
            }

            var ageDays = (today - orderDate).Days;
            ApplyLifecycle(faker, order, ageDays, today);

            if (order.Status is OrderStatus.Shipped or OrderStatus.Delivered)
            {
                foreach (var line in order.Lines)
                {
                    movementBatch.Add(StockMovement.Create(line.ProductId, order.WarehouseId, StockMovementType.Sale,
                        line.Quantity, order.ShippedAt!.Value, order.OrderNumber));
                }

                var issueDate = order.ShippedAt!.Value.Date;
                var invoice = Invoice.ForOrder($"FT-{issueDate:yyyy}-{++invoiceSeq:D6}", order, issueDate,
                    faker.PickRandom(30, 45, 60, 90));
                SimulatePayments(faker, invoice, today);
                order.Invoice = invoice;
            }

            orderBatch.Add(order);

            if (orderBatch.Count == BatchSize || i == orderDates.Count - 1)
            {
                await FlushOrdersAsync(orderBatch, movementBatch, ct);
                orderBatch.Clear();
                movementBatch.Clear();
            }
        }

        // Dönem içi satın alma: stok bakiyesi çok düşen ürünler için tedarik girişleri.
        await ReplenishAsync(faker, today, ct);

        db.ChangeTracker.AutoDetectChangesEnabled = true;
        logger.LogInformation("Tohumlama tamamlandı: {Orders} sipariş, {Elapsed:n1} sn", o.Orders, sw.Elapsed.TotalSeconds);
    }

    private static decimal LogUniform(Faker faker, double min, double max) =>
        Math.Round((decimal)Math.Exp(faker.Random.Double(Math.Log(min), Math.Log(max))), 2);

    private static DateTime PickOrderDate(Faker faker, DateTime start, int totalDays)
    {
        // Hafif büyüme trendi + mevsimsellik: son aylar ve sonbahar biraz daha yoğun.
        while (true)
        {
            var day = faker.Random.Int(0, totalDays - 1);
            var date = start.AddDays(day);
            var trend = 0.7 + 0.3 * day / totalDays;
            var season = 1 + 0.15 * Math.Sin((date.Month - 3) / 12.0 * 2 * Math.PI);
            var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.25 : 1;
            if (faker.Random.Double() < trend * season * weekend / 1.15)
                return date.AddHours(faker.Random.Int(8, 18)).AddMinutes(faker.Random.Int(0, 59));
        }
    }

    private static void ApplyLifecycle(Faker faker, Order order, int ageDays, DateTime today)
    {
        if (order.Lines.Count == 0) return;

        if (ageDays <= 2 && faker.Random.Bool(0.5f)) return; // taslak

        if (faker.Random.Bool(0.04f))
        {
            order.Cancel();
            return;
        }

        order.Confirm();
        if (ageDays <= 5 && faker.Random.Bool(0.7f)) return; // onaylı, sevk bekliyor

        var shipAfter = faker.Random.Int(1, 4);
        var shippedAt = order.OrderDate.AddDays(shipAfter);
        if (shippedAt > today) return;
        order.Ship(shippedAt);

        if (ageDays > 10 || faker.Random.Bool(0.5f)) order.MarkDelivered();
    }

    private static void SimulatePayments(Faker faker, Invoice invoice, DateTime today)
    {
        // Müşterilerin çoğu vadesinde öder; bir kısmı geç, küçük bir kısmı hiç ödemez.
        var behavior = faker.Random.WeightedRandom(["onTime", "late", "partial", "none"], [0.72f, 0.15f, 0.07f, 0.06f]);
        var method = faker.PickRandom<PaymentMethod>();

        switch (behavior)
        {
            case "onTime":
            {
                var paidAt = invoice.IssueDate.AddDays(faker.Random.Int(5, (invoice.DueDate - invoice.IssueDate).Days));
                if (paidAt <= today) invoice.RegisterPayment(invoice.TotalAmount, paidAt, method);
                break;
            }
            case "late":
            {
                var paidAt = invoice.DueDate.AddDays(faker.Random.Int(5, 60));
                if (paidAt <= today) invoice.RegisterPayment(invoice.TotalAmount, paidAt, method);
                break;
            }
            case "partial":
            {
                var paidAt = invoice.IssueDate.AddDays(faker.Random.Int(10, 40));
                if (paidAt <= today)
                    invoice.RegisterPayment(Math.Round(invoice.TotalAmount * faker.Random.Decimal(0.3m, 0.7m), 2), paidAt, method);
                break;
            }
        }
    }

    private async Task FlushOrdersAsync(List<Order> orders, List<StockMovement> movements, CancellationToken ct)
    {
        // Sipariş grafındaki ana veri nesnelerini (müşteri, ürün) koparıyoruz; aksi halde EF onları
        // yeniden eklenecek yeni kayıtlar sanar. Yalnızca FK değerleri kalır.
        foreach (var order in orders)
        {
            order.Customer = null!;
            foreach (var line in order.Lines) line.Product = null!;
            if (order.Invoice is { } invoice)
            {
                invoice.Customer = null!;
            }
        }

        // Fatura, siparişle bire bir ilişkili olduğundan aynı SaveChanges içinde eklenir.
        db.Orders.AddRange(orders);
        db.StockMovements.AddRange(movements);
        await db.SaveChangesAsync(ct);

        db.ChangeTracker.Clear();
    }

    private async Task ReplenishAsync(Faker faker, DateTime today, CancellationToken ct)
    {
        var low = await db.StockBalances.AsNoTracking()
            .Join(db.Products, b => b.ProductId, p => p.Id, (b, p) => new { b.ProductId, b.WarehouseId, b.Quantity, p.ReorderLevel })
            .Where(x => x.Quantity < x.ReorderLevel * 3)
            .ToListAsync(ct);

        // Bakiyeler sıfırın altına düşmesin; bir kısmını bilerek yeniden sipariş seviyesinin altında
        // bırakıyoruz ki "stoğu kritik ürünler" sorusu anlamlı olsun.
        foreach (var item in low)
        {
            var critical = faker.Random.Bool(0.12f);
            var target = critical ? faker.Random.Int(0, Math.Max(0, item.ReorderLevel - 1)) : item.ReorderLevel * 6;
            var need = target - item.Quantity;
            if (need <= 0) continue;
            db.StockMovements.Add(StockMovement.Create(item.ProductId, item.WarehouseId, StockMovementType.Purchase,
                need, today.AddDays(-faker.Random.Int(1, 60)), $"SA-{faker.Random.Int(10000, 99999)}"));
        }
        await db.SaveChangesAsync(ct);
    }

    private sealed class WeightedPicker<T>(IReadOnlyList<T> items, double[] weights)
    {
        private readonly double[] _cumulative = weights.Select((_, i) => weights.Take(i + 1).Sum()).ToArray();

        public T Pick(Randomizer random)
        {
            var r = random.Double() * _cumulative[^1];
            var idx = Array.BinarySearch(_cumulative, r);
            if (idx < 0) idx = ~idx;
            return items[Math.Min(idx, items.Count - 1)];
        }
    }
}
