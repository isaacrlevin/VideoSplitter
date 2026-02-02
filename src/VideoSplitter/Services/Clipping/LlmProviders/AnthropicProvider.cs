using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using VideoSplitter.Models;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// LLM provider for Anthropic Claude models
/// </summary>
public class AnthropicProvider : LlmProviderBase
{
    private readonly HttpClient _httpClient;

    protected override string ProviderName => "Anthopic";

    public AnthropicProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override bool IsConfigured(AppSettings settings)
    {
        return !string.IsNullOrEmpty(settings.Anthropic.ApiKey);
    }

    public override IChatClient? GetChatClient(AppSettings settings)
    {
        // Anthropic doesn't have a built-in Microsoft.Extensions.AI provider yet
        // Return null and let the base class handle it with a custom implementation
        return null;
    }

    public override async Task<(bool Success, IEnumerable<Segment>? Segments, string? Error)> GenerateSegmentsAsync(
        Project project,
        string transcriptContent,
        AppSettings settings,
        IProgress<string>? progress = null)
    {
        try
        {
            if (!IsConfigured(settings))
            {
                return (false, null, "Anthropic API key not specified in settings");
            }

            progress?.Report("Using Anthropic Claude model...");
            progress?.Report("Note: Transcript content remains available for regeneration");

            var prompt = CreateSegmentationPrompt(transcriptContent, settings);

            var requestBody = new
            {
                model = "claude-3-haiku-20240307",
                max_tokens = 2000,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", settings.Anthropic.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            progress?.Report("Sending request to Anthropic...");

            var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return (false, null, $"Anthropic API error: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var anthropicResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (!anthropicResponse.TryGetProperty("content", out var contentArray) ||
                contentArray.GetArrayLength() == 0 ||
                !contentArray[0].TryGetProperty("text", out var textElement))
            {
                return (false, null, "Invalid response from Anthropic");
            }

            var aiResponse = textElement.GetString();
            progress?.Report("Processing AI response...");

            return ParseSegmentsFromAiResponse(project, aiResponse ?? "", transcriptContent, settings);
        }
        catch (Exception ex)
        {
            return (false, null, $"Anthropic generation failed: {ex.Message}");
        }
    }
}
