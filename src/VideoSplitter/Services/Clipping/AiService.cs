using System.Text;
using System.Text.Json;
using VideoSplitter.Models;
using VideoSplitter.Services.LlmProviders;

namespace VideoSplitter.Services;

public class AiService : IAiService
{
    private readonly LlmProviderFactory _providerFactory;

    public AiService(HttpClient httpClient)
    {
        _providerFactory = new LlmProviderFactory(httpClient);
    }

    public async Task<(bool Success, IEnumerable<Segment>? Segments, string? Error)> GenerateSegmentsAsync(
        Project project,
        string transcriptContent,
        AppSettings settings,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(transcriptContent))
        {
            return (false, null, "Transcript content is empty");
        }

        progress?.Report("Analyzing transcript for segment generation...");

        // Get the appropriate provider based on settings
        var provider = _providerFactory.GetProvider(settings.LlmProvider);

        // Check if the provider is configured
        if (!provider.IsConfigured(settings))
        {
            return (false, null, $"LLM Provider '{settings.LlmProvider}' is not configured. Please configure it in Settings before generating segments.");
        }

        // Generate segments using the provider
        return await provider.GenerateSegmentsAsync(project, transcriptContent, settings, progress);
    }
}