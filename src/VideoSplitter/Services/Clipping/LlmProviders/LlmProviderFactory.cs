using VideoSplitter.Models;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// Factory for creating LLM provider instances
/// </summary>
public class LlmProviderFactory
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<LlmProvider, ILlmProvider> _providerCache = new();

    public LlmProviderFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ILlmProvider GetProvider(LlmProvider providerType)
    {
        // Return cached instance if available
        if (_providerCache.TryGetValue(providerType, out var cachedProvider))
        {
            return cachedProvider;
        }

        // Create new instance
        ILlmProvider provider = providerType switch
        {
            LlmProvider.Local => new OllamaProvider(),
            LlmProvider.OpenAI => new OpenAiProvider(),
            LlmProvider.Anthropic => new AnthropicProvider(_httpClient),
            LlmProvider.AzureOpenAI => new AzureOpenAiProvider(),
            LlmProvider.GoogleGemini => new GoogleGeminiProvider(),
            _ => throw new ArgumentException($"Unknown LLM provider: {providerType}")
        };

        // Cache the instance
        _providerCache[providerType] = provider;

        return provider;
    }
}
