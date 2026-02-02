using System.Runtime.CompilerServices;
using Azure.AI.OpenAI;
using MicrosoftChatMessage = Microsoft.Extensions.AI.ChatMessage;
using MicrosoftChatRole = Microsoft.Extensions.AI.ChatRole;
using MicrosoftChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;
using OpenAIChatMessage = OpenAI.Chat.ChatMessage;
using OpenAIChatResponseFormat = OpenAI.Chat.ChatResponseFormat;
using Microsoft.Extensions.AI;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// Wraps Azure OpenAI ChatClient to implement Microsoft.Extensions.AI IChatClient interface
/// </summary>
public class AzureOpenAIChatClientWrapper : ChatClientWrapper
{
    private readonly IChatClient _chatClient;
    private readonly string _deploymentName;

    public AzureOpenAIChatClientWrapper(IChatClient chatClient, string deploymentName)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _deploymentName = deploymentName ?? throw new ArgumentNullException(nameof(deploymentName));
    }

    public override ChatClientMetadata Metadata => new("AzureOpenAI", null, _deploymentName);

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
            if (update != null
                )
            {
                yield return new ChatResponseUpdate(MicrosoftChatRole.Assistant, update.Text);
            }
        }
    }
}
