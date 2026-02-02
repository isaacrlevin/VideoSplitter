using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using VideoSplitter.Models;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// LLM provider for Azure OpenAI models using Microsoft.Extensions.AI
/// </summary>
public class AzureOpenAiProvider : LlmProviderBase
{
    private IChatClient? _chatClient;

    protected override string ProviderName => "Azure OpenAI";

    public override bool IsConfigured(AppSettings settings)
    {
        return !string.IsNullOrEmpty(settings.AzureOpenAi.ApiKey) &&
               !string.IsNullOrEmpty(settings.AzureOpenAi.Endpoint);
    }

    public override IChatClient? GetChatClient(AppSettings settings)
    {
        if (!IsConfigured(settings))
        {
            return null;
        }

        if (_chatClient == null)
        {
            var azureOpenAIClient = new AzureOpenAIClient(
                new Uri(settings.AzureOpenAi.Endpoint!),
                new AzureKeyCredential(settings.AzureOpenAi.ApiKey!));
            
            var deploymentName = settings.AzureOpenAi.DeploymentName ?? "gpt-35-turbo";
            _chatClient = azureOpenAIClient.GetChatClient(deploymentName).AsIChatClient();
        }

        return _chatClient;
    }
}
