namespace ErpAssistant.Application.Common;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} bulunamadı: {key}")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}

public sealed class ForbiddenException(string message) : Exception(message);

/// <summary>
/// İstemcinin elindeki veri sunucudakiyle çelişiyor (ör. fiyat değişti); API'de 409 döner.
/// <see cref="Code"/> ve <see cref="Extensions"/> ProblemDetails'e ek alan olarak yazılır.
/// </summary>
public sealed class ConflictException(string code, string message, IReadOnlyDictionary<string, object?>? extensions = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, object?> Extensions { get; } = extensions ?? new Dictionary<string, object?>();
}

/// <summary>Aynı idempotency anahtarı farklı bir istekle geldi; API'de 422 döner.</summary>
public sealed class IdempotencyKeyReusedException()
    : Exception("Bu idempotency anahtarı farklı bir istek için kullanılmış.")
{
    public const string Code = "idempotency_key_reused";
}
