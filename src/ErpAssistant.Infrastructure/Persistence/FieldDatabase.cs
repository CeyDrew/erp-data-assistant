using ErpAssistant.Application.Field;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpAssistant.Infrastructure.Persistence;

internal sealed class FieldDatabase(ErpDbContext db) : IFieldDatabase
{
    public async Task<ulong> SyncUpperBoundAsync(CancellationToken ct)
    {
        // MIN_ACTIVE_ROWVERSION: veritabanındaki en küçük commit edilmemiş rowversion (açık işlem yoksa
        // bir sonraki değer). Bir eksiği, altında açık işlem kalmadığı garanti edilen en büyük değerdir.
        var rows = await db.Database
            .SqlQueryRaw<long>("SELECT CAST(MIN_ACTIVE_ROWVERSION() AS bigint) - 1 AS [Value]")
            .ToListAsync(ct);
        return (ulong)Math.Max(0, rows[0]);
    }

    public Task<long> NextOrderSequenceAsync(CancellationToken ct) => NextAsync(ErpDbContext.OrderNumberSequence, ct);

    public Task<long> NextInvoiceSequenceAsync(CancellationToken ct) => NextAsync(ErpDbContext.InvoiceNumberSequence, ct);

    private async Task<long> NextAsync(string sequence, CancellationToken ct)
    {
        // NEXT VALUE FOR alt sorguda kullanılamadığından sorgu EF tarafından sarmalanmadan çalıştırılır.
        // Sequence adı sabittir (kullanıcı girdisi değil).
        var rows = await db.Database
            .SqlQueryRaw<long>("SELECT NEXT VALUE FOR dbo." + sequence + " AS [Value]")
            .ToListAsync(ct);
        return rows[0];
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        // Bağlantı yeniden deneme stratejisi açıkken kullanıcı işlemi stratejinin içinde başlatılmalı;
        // geçici bir hatada tüm blok baştan çalışır (iş, başında izlenen değişiklikleri temizler).
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await work(ct);
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    public bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    public void DiscardChanges() => db.ChangeTracker.Clear();
}
