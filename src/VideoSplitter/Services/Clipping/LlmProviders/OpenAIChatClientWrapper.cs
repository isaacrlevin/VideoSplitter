using System.Runtime.CompilerServices;
using MicrosoftChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MicrosoftChatRole = Microsoft.Extensions.AI.ChatRole;
using Microsoft.Extensions.AI;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// Wraps OpenAI ChatClient to implement Microsoft.Extensions.AI IChatClient interface
/// </summary>
public class OpenAIChatClientWrapper : ChatClientWrapper
{
    private readonly IChatClient _chatClient;
    private readonly string _modelId;

    public OpenAIChatClientWrapper(IChatClient chatClient, string modelId)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
    }

    public override ChatClientMetadata Metadata => new("OpenAI", null, _modelId);

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<MicrosoftChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var completion = await _chatClient.GetResponseAsync(chatMessages, options, cancellationToken);
        var responseText = completion.Text;

        return new ChatResponse([new MicrosoftChatMessage(MicrosoftChatRole.Assistant, responseText)]);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<MicrosoftChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in _chatClient.GetStreamingResponseAsync(chatMessages, options, cancellationToken))
        {
            if (update != null)
            {
                yield return new ChatResponseUpdate(MicrosoftChatRole.Assistant, update.Text);
            }
        }
    }
}
