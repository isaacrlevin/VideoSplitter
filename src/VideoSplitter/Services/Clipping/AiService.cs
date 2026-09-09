using Microsoft.Extensions.AI;
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
    public async Task<(bool Success, string? Title, string? Description, string? Error)> GenerateSocialMediaContentAsync(
       string transcriptContent,
       string platformName,
       AppSettings settings,
       IProgress<string>? progress = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(transcriptContent))
            {
                return (false, null, null, "Transcript content is empty");
            }

            progress?.Report("Generating social media content...");

            // Get the appropriate provider based on settings
            var provider = _providerFactory.GetProvider(settings.LlmProvider);

            // Check if the provider is configured
            if (!provider.IsConfigured(settings))
            {
                return (false, null, null, $"LLM Provider '{settings.LlmProvider}' is not configured. Please configure it in Settings.");
            }

            var chatClient = provider.GetChatClient(settings);
            if (chatClient == null)
            {
                return (false, null, null, "Failed to create chat client");
            }

            // The transcript content is already from the specific segment, so no need to truncate
            // However, keep a safety limit to avoid token issues with very long segments
            var safeTranscript = transcriptContent.Length > 5000
                ? transcriptContent.Substring(0, 5000) + "..."
                : transcriptContent;

            var systemPrompt = $$"""
You are a social media expert specializing in creating engaging content for {{platformName}}.
Your task is to generate a catchy title and compelling description based on video transcripts.
The title should be attention-grabbing and concise (max 150 characters).
The description should be engaging and informative (max 500 characters).
Respond ONLY with valid JSON in this exact format:
{
  "title": "Your catchy title here",
  "description": "Your compelling description here"
}
""";

            var userPrompt = $$"""
Based on this video segment transcript, generate an engaging title and description for {{platformName}}:

{{safeTranscript}}

Remember: Respond with ONLY the JSON object, no other text.
""";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            progress?.Report("Sending request to AI provider...");

            var chatOptions = new ChatOptions
            {
                Temperature = 1f, // Higher temperature for more creative titles/descriptions
                //MaxOutputTokens = 500
            };

            var response = await chatClient.GetResponseAsync(messages, chatOptions);
            var aiResponse = response.Messages[0].Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return (false, null, null, "Empty response from AI provider");
            }

            progress?.Report("Processing AI response...");

            // Clean the response - remove markdown code blocks if present
            aiResponse = aiResponse.Trim();
            if (aiResponse.StartsWith("```json"))
            {
                aiResponse = aiResponse.Substring(7);
            }
            if (aiResponse.StartsWith("```"))
            {
                aiResponse = aiResponse.Substring(3);
            }
            if (aiResponse.EndsWith("```"))
            {
                aiResponse = aiResponse.Substring(0, aiResponse.Length - 3);
            }
            aiResponse = aiResponse.Trim();

            // Parse JSON response
            var jsonDoc = JsonDocument.Parse(aiResponse);
            var root = jsonDoc.RootElement;

            var title = root.GetProperty("title").GetString();
            var description = root.GetProperty("description").GetString();

            if (string.IsNullOrWhiteSpace(title))
            {
                return (false, null, null, "AI generated empty title");
            }

            progress?.Report("Content generated successfully!");

            return (true, title, description, null);
        }
        catch (JsonException ex)
        {
            return (false, null, null, $"Failed to parse AI response: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, null, null, $"Failed to generate content: {ex.Message}");
        }
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