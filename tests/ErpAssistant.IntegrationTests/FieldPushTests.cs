using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ErpAssistant.Application.Field;
using ErpAssistant.Application.Orders;
using ErpAssistant.Domain.Field;
using ErpAssistant.Domain.Sales;
using ErpAssistant.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static ErpAssistant.IntegrationTests.Infrastructure.FieldApi;

namespace ErpAssistant.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class FieldPushTests(ErpApiFactory factory)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Batch_applies_each_operation_independently_and_dedupes_by_id()
    {
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var visit = (await rep.SyncAsync()).Visits.First(v => v.Status == VisitStatus.Planned);
        var (checkIn, missing, locations, checkOut) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (p1, p2) = (Guid.NewGuid(), Guid.NewGuid());
        object[] batch =
        [
            Op(checkIn, "visit.checkIn", new { visitId = visit.Id, at = Now, latitude = 41.01, longitude = 28.97 }),
            Op(checkIn, "visit.checkIn", new { visitId = visit.Id, at = Now, latitude = 41.01, longitude = 28.97 }),
            Op(missing, "visit.checkIn", new { visitId = 999_999, at = Now }),
            Op(locations, "location.batch", new
            {
                points = new object[]
                {
                    new { id = p1, at = Now, latitude = 41.0, longitude = 29.0, accuracy = 8.5 },
                    new { id = p1, at = Now, latitude = 41.0, longitude = 29.0, accuracy = 8.5 },
                    new { id = p2, at = Now.AddSeconds(30), latitude = 41.001, longitude = 29.001, accuracy = 6 },
                },
            }),
            Op(checkOut, "visit.checkOut", new { visitId = visit.Id, at = Now.AddMinutes(20), note = "Sipariş alındı" }),
        ];

        var first = await rep.PushAsync(batch);

        first.Results.Select(r => r.Status).Should().Equal("applied", "duplicate", "rejected", "applied", "applied");
        first.Results[2].Code.Should().Be("not_found");
        first.Results[3].Data!.Value.GetProperty("accepted").GetInt32().Should().Be(2);
        first.Results[3].Data!.Value.GetProperty("duplicates").GetInt32().Should().Be(1);
        first.Results[4].Data!.Value.GetProperty("status").GetString().Should().Be("Completed");

        // Aynı paket yeniden gönderilir (ağ kesildi, yanıt kayboldu): hiçbir şey ikinci kez uygulanmaz.
        var replay = await rep.PushAsync(batch);
        replay.Results.Select(r => r.Status).Should().Equal("duplicate", "duplicate", "rejected", "duplicate", "duplicate");
        (await factory.WithDbAsync(db => db.LocationPings.CountAsync(p => p.ClientId == p1 || p.ClientId == p2))).Should().Be(2);

        var synced = (await rep.SyncAsync()).Visits.Single(v => v.Id == visit.Id);
        synced.Status.Should().Be(VisitStatus.Completed);
        synced.Note.Should().Be("Sipariş alındı");
    }

    [Fact]
    public async Task Invalid_payloads_are_rejected_without_stopping_the_batch()
    {
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var visit = (await rep.SyncAsync()).Visits.First(v => v.Status == VisitStatus.Planned);

        var result = await rep.PushAsync(
            Op(Guid.NewGuid(), "visit.checkOut", new { visitId = visit.Id, at = Now }),   // giriş yok → domain
            Op(Guid.NewGuid(), "unknown.op", new { }),                                   // → validation
            Op(Guid.NewGuid(), "visit.checkIn", new { visitId = visit.Id }),             // at yok → validation
            Op(Guid.NewGuid(), "delivery.complete", new { deliveryId = 1, at = Now, recipientName = "x" }), // temsilci → rejected
            Op(Guid.NewGuid(), "visit.checkIn", new { visitId = visit.Id, at = Now }));

        result.Results.Select(r => r.Status).Should().Equal("rejected", "rejected", "rejected", "rejected", "applied");
        result.Results.Take(3).Select(r => r.Code).Should().Equal("domain", "validation", "validation");
        result.Results[3].Code.Should().BeOneOf("not_found", "forbidden");
        result.Results[4].Code.Should().BeNull();
    }

    [Fact]
    public async Task Order_create_via_push_is_idempotent_and_reports_price_conflicts()
    {
        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        var (_, _, pages) = await rep.FullSyncAsync();
        var customer = pages.SelectMany(p => p.Customers).First();
        var product = pages.SelectMany(p => p.Products).Last();
        var warehouse = pages[0].Warehouses[0];
        var opId = Guid.NewGuid();
        var payload = new { customerId = customer.Id, warehouseId = warehouse.Id, lines = new[] { new { productId = product.Id, quantity = 1, unitPrice = product.UnitPrice, discountRate = 0 } } };
        var stale = new { customerId = customer.Id, warehouseId = warehouse.Id, lines = new[] { new { productId = product.Id, quantity = 1, unitPrice = product.UnitPrice + 3, discountRate = 0 } } };

        var result = await rep.PushAsync(
            Op(Guid.NewGuid(), "order.create", stale),
            Op(opId, "order.create", payload),
            Op(opId, "order.create", payload));

        result.Results[0].Status.Should().Be("conflict");
        result.Results[0].Code.Should().Be("price_changed");
        result.Results[0].Data!.Value.GetProperty("newTotal").GetDecimal().Should().Be(product.UnitPrice);
        result.Results[1].Status.Should().Be("applied");
        result.Results[2].Status.Should().Be("duplicate");
        var orderId = result.Results[1].Data!.Value.GetProperty("order").GetProperty("id").GetInt32();
        result.Results[2].Data!.Value.GetProperty("order").GetProperty("id").GetInt32().Should().Be(orderId);
        (await factory.WithDbAsync(db => db.Orders.CountAsync(o => o.ClientRequestId == opId))).Should().Be(1);
    }

    [Fact]
    public async Task Completing_a_delivery_marks_the_order_delivered()
    {
        var driver = await factory.CreateClientAsAsync(DemoUsers.Driver);
        var deliveries = (await driver.SyncAsync()).Deliveries.Where(d => d.Status == DeliveryStatus.Assigned).ToList();
        var (done, failed) = (deliveries[0], deliveries[1]);

        var result = await driver.PushAsync(
            Op(Guid.NewGuid(), "delivery.complete", new { deliveryId = done.Id, at = Now, recipientName = "Hasan Usta", latitude = 41.0, longitude = 29.0 }),
            Op(Guid.NewGuid(), "delivery.fail", new { deliveryId = failed.Id, at = Now, reason = "CustomerAbsent", note = "Kapalı" }),
            Op(Guid.NewGuid(), "delivery.complete", new { deliveryId = failed.Id, at = Now, recipientName = "x" }));

        result.Results.Select(r => r.Status).Should().Equal("applied", "applied", "rejected");
        result.Results[0].Data!.Value.GetProperty("orderStatus").GetString().Should().Be("Delivered");

        var order = await factory.Admin.GetJsonAsync<OrderDetail>($"/api/v1/orders/{done.OrderId}");
        order.Status.Should().Be(OrderStatus.Delivered);

        var synced = (await driver.SyncAsync()).Deliveries;
        synced.Single(d => d.Id == done.Id).Should().Match<DeliverySync>(d => d.Status == DeliveryStatus.Delivered && d.RecipientName == "Hasan Usta");
        synced.Single(d => d.Id == failed.Id).FailureReason.Should().Be(DeliveryFailureReason.CustomerAbsent);

        // Başka sürücü aynı teslimatı göremez.
        var other = await factory.CreateClientAsAsync(DemoUsers.OtherDriver);
        var foreign = await other.PushAsync(Op(Guid.NewGuid(), "delivery.fail", new { deliveryId = done.Id, at = Now, reason = "Refused" }));
        foreign.Results[0].Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Proof_upload_validates_type_size_and_owner()
    {
        var driver = await factory.CreateClientAsAsync(DemoUsers.Driver);
        var delivery = (await driver.SyncAsync()).Deliveries.First(d => d.Status != DeliveryStatus.Failed);
        var url = $"/api/v1/field/deliveries/{delivery.Id}/proof";

        (await driver.PostAsync(url, Proof(Jpeg(2_000), "image/jpeg", Png(500), "image/png"))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await driver.SyncAsync()).Deliveries.Single(d => d.Id == delivery.Id).HasProof.Should().BeTrue();

        // Tekrar yükleme üzerine yazar.
        (await driver.PostAsync(url, Proof(Png(3_000), "image/png", Png(400), "image/png"))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        // İçerik türü JPEG diyor ama baytlar metin.
        var fake = await driver.PostAsync(url, Proof("merhaba dünya"u8.ToArray(), "image/jpeg", Png(400), "image/png"));
        fake.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await fake.JsonAsync()).GetProperty("errors").TryGetProperty("photo", out _).Should().BeTrue();

        // İmza yalnızca PNG ve en fazla 1 MB.
        (await driver.PostAsync(url, Proof(Jpeg(1_000), "image/jpeg", Jpeg(400), "image/jpeg"))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await driver.PostAsync(url, Proof(Jpeg(1_000), "image/jpeg", Png(1_100_000), "image/png"))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        var missing = new MultipartFormDataContent { { File(Jpeg(1_000), "image/jpeg"), "photo", "p.jpg" } };
        (await driver.PostAsync(url, missing)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var rep = await factory.CreateClientAsAsync(DemoUsers.SalesRep);
        (await rep.PostAsync(url, Proof(Jpeg(1_000), "image/jpeg", Png(400), "image/png"))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        var otherDriver = await factory.CreateClientAsAsync(DemoUsers.OtherDriver);
        (await otherDriver.PostAsync(url, Proof(Jpeg(1_000), "image/jpeg", Png(400), "image/png"))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Device_registration_upserts_by_installation()
    {
        var driver = await factory.CreateClientAsAsync(DemoUsers.Driver);
        var installation = Guid.NewGuid().ToString("N");

        (await driver.PostAsJsonAsync("/api/v1/field/devices", new { installationId = installation, platform = "android", appVersion = "1.0.0" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await driver.PostAsJsonAsync("/api/v1/field/devices", new { installationId = installation, platform = "android", appVersion = "1.1.0", pushToken = "fcm-token" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await driver.PostAsJsonAsync("/api/v1/field/devices", new { installationId = installation, platform = "windows", appVersion = "1" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var devices = await factory.WithDbAsync(db => db.Devices.Where(d => d.InstallationId == installation).ToListAsync());
        devices.Should().ContainSingle().Which.Should().Match<Device>(d => d.AppVersion == "1.1.0" && d.PushToken == "fcm-token");
    }

    private static MultipartFormDataContent Proof(byte[] photo, string photoType, byte[] signature, string signatureType) => new()
    {
        { File(photo, photoType), "photo", "photo.bin" },
        { File(signature, signatureType), "signature", "signature.bin" },
    };

    private static ByteArrayContent File(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    private static byte[] Png(int size) => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], size);
    private static byte[] Jpeg(int size) => Pad([0xFF, 0xD8, 0xFF, 0xE0], size);

    private static byte[] Pad(byte[] header, int size)
    {
        var bytes = new byte[Math.Max(size, header.Length)];
        header.CopyTo(bytes, 0);
        return bytes;
    }
}
