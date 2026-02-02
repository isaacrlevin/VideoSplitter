using Microsoft.Extensions.AI;
using VideoSplitter.Models;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// Custom IChatClient wrapper for providers that don't have native Microsoft.Extensions.AI support
/// </summary>
public abstract class ChatClientWrapper : IChatClient
{
    public abstract ChatClientMetadata Metadata { get; }

    public abstract Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    public virtual object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType?.IsInstanceOfType(this) == true ? this : null;
    }

    public virtual void Dispose()
    {
        // Default implementation
    }
}
