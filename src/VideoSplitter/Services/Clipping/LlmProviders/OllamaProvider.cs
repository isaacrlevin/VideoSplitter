using Microsoft.Extensions.AI;
using OllamaSharp;
using VideoSplitter.Models;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// LLM provider for local Ollama models using Microsoft.Extensions.AI
/// </summary>
public class OllamaProvider : LlmProviderBase
{
    private IChatClient? _chatClient;

    protected override string ProviderName => "Ollama";
    public override bool IsConfigured(AppSettings settings)
    {
        return !string.IsNullOrEmpty(settings.Ollama.Model);
    }

    public override IChatClient? GetChatClient(AppSettings settings)
    {
        if (!IsConfigured(settings))
        {
            return null;
        }

        if (_chatClient == null)
        {
            var ollamaClient = new OllamaApiClient(new Uri("http://localhost:11434"), settings.Ollama.Model!);
            _chatClient = new OllamaChatClient(ollamaClient, settings.Ollama.Model!);
        }

        return _chatClient;
    }
}
