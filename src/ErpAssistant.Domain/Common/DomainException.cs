namespace ErpAssistant.Domain.Common;

/// <summary>Bir domain kuralı ihlal edildiğinde fırlatılır; API katmanında 422 olarak döner.</summary>
public sealed class DomainException(string message) : Exception(message);
