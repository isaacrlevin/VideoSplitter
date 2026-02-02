using Microsoft.Extensions.AI;
using OpenAI;
using VideoSplitter.Models;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// LLM provider for OpenAI GPT models using Microsoft.Extensions.AI
/// </summary>
public class OpenAiProvider : LlmProviderBase
{
    private IChatClient? _chatClient;

    protected override string ProviderName => "OpenAI";

    public override bool IsConfigured(AppSettings settings)
    {
        return !string.IsNullOrEmpty(settings.OpenAi.ApiKey);
    }

    public override IChatClient? GetChatClient(AppSettings settings)
    {
        if (!IsConfigured(settings))
        {
            return null;
        }

        if (_chatClient == null)
        {
            _chatClient = new OpenAIClient(settings.OpenAi.ApiKey).GetChatClient(settings.OpenAi.Model).AsIChatClient();
        }

        return _chatClient;
    }
}
