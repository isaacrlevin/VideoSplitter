using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// Wraps OllamaSharp client to implement Microsoft.Extensions.AI IChatClient interface
/// </summary>
public class OllamaChatClient : ChatClientWrapper
{
    private readonly OllamaApiClient _ollamaClient;
    private readonly string _modelName;

    public OllamaChatClient(OllamaApiClient ollamaClient, string modelName)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _modelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
    }

    public override ChatClientMetadata Metadata => new("Ollama", _ollamaClient.Uri, _modelName);

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPromptFromMessages(chatMessages);

        var request = new GenerateRequest
        {
            Prompt = prompt,
            Stream = false,
            Format = options?.ResponseFormat == ChatResponseFormat.Json ? "json" : null,
            Options = new RequestOptions
            {
                Temperature = options?.Temperature ?? 0.7f,
                TopP = options?.TopP ?? 0.9f,
                NumPredict = options?.MaxOutputTokens ?? 2000
            }
        };

        var responseBuilder = new StringBuilder();
        await foreach (var chunk in _ollamaClient.GenerateAsync(request, cancellationToken))
        {
            if (chunk?.Response != null)
            {
                responseBuilder.Append(chunk.Response);
            }
        }

        var responseText = responseBuilder.ToString();
        var message = new ChatMessage(ChatRole.Assistant, responseText);

        return new ChatResponse([message]);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = BuildPromptFromMessages(chatMessages);

        var request = new GenerateRequest
        {
            Prompt = prompt,
            Stream = true,
            Format = options?.ResponseFormat == ChatResponseFormat.Json ? "json" : null,
            Options = new RequestOptions
            {
                Temperature = options?.Temperature ?? 0.7f,
                TopP = options?.TopP ?? 0.9f,
                NumPredict = options?.MaxOutputTokens ?? 2000
            }
        };

        await foreach (var chunk in _ollamaClient.GenerateAsync(request, cancellationToken))
        {
            if (chunk?.Response != null)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, chunk.Response);
            }
        }
    }

    private string BuildPromptFromMessages(IEnumerable<ChatMessage> messages)
    {
        var promptBuilder = new StringBuilder();

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
            {
                promptBuilder.AppendLine($"System: {message.Text}");
                promptBuilder.AppendLine();
            }
            else if (message.Role == ChatRole.User)
            {
                promptBuilder.AppendLine($"User: {message.Text}");
                promptBuilder.AppendLine();
            }
            else if (message.Role == ChatRole.Assistant)
            {
                promptBuilder.AppendLine($"Assistant: {message.Text}");
                promptBuilder.AppendLine();
            }
        }

        promptBuilder.AppendLine("Assistant:");

        return promptBuilder.ToString();
    }
}
