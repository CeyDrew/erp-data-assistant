namespace ErpAssistant.Application.Common;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} bulunamadı: {key}")
{
    public string Resource { get; } = resource;
    public object Key { get; } = key;
}

public sealed class ForbiddenException(string message) : Exception(message);
