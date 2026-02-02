using VideoSplitter.Models;

namespace VideoSplitter.Services.TranscriptProviders;

/// <summary>
/// Factory for creating transcript provider instances
/// </summary>
public class TranscriptProviderFactory
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<TranscriptProvider, ITranscriptProvider> _providerCache = new();

    public TranscriptProviderFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ITranscriptProvider GetProvider(TranscriptProvider providerType)
    {
        // Return cached instance if available
        if (_providerCache.TryGetValue(providerType, out var cachedProvider))
        {
            return cachedProvider;
        }

        // Create new instance
        ITranscriptProvider provider = providerType switch
        {
            TranscriptProvider.Local => new WhisperTranscriptProvider(_httpClient),
            TranscriptProvider.Azure => new AzureSpeechProvider(),
            _ => throw new ArgumentException($"Unknown transcript provider: {providerType}")
        };

        // Cache the instance
        _providerCache[providerType] = provider;

        return provider;
    }
}
