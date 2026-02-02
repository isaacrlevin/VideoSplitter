using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using VideoSplitter.Models;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Microsoft;
namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// LLM provider for Google Gemini models
/// </summary>
public class GoogleGeminiProvider : LlmProviderBase
{
    private IChatClient? _chatClient;

    protected override string ProviderName => "Google Gemini";
    public override bool IsConfigured(AppSettings settings)
    {
        return !string.IsNullOrEmpty(settings.GoogleGemini.ApiKey);
    }

    public override IChatClient? GetChatClient(AppSettings settings)
    {
        if (!IsConfigured(settings))
        {
            return null;
        }

        if (_chatClient == null)
        {
            _chatClient = new GeminiChatClient(apiKey: settings.GoogleGemini.ApiKey, model: settings.GoogleGemini.Model);
        }

        return _chatClient;
    }
}
