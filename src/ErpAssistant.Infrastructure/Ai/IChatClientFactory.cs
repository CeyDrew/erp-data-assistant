using Microsoft.Extensions.AI;

namespace ErpAssistant.Infrastructure.Ai;

/// <summary>Ham sağlayıcı istemcisini üretir; testler bunu sahte istemciyle değiştirir.</summary>
public interface IChatClientFactory
{
    string Provider { get; }
    string Model { get; }
    IChatClient Create();
}
