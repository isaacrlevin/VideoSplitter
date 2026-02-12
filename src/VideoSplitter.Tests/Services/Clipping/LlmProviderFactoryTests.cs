using System.Net;
using RichardSzalay.MockHttp;
using VideoSplitter.Models;
using VideoSplitter.Services.LlmProviders;
using VideoSplitter.Tests.Fixtures;

namespace VideoSplitter.Tests.Services.Clipping;

public class LlmProviderFactoryTests
{
    [Fact]
    public void GetProvider_ReturnsOpenAiProvider_ForOpenAI()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var factory = new LlmProviderFactory(httpClient);

        // Act
        var provider = factory.GetProvider(LlmProvider.OpenAI);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<OpenAiProvider>();
    }

    [Fact]
    public void GetProvider_ReturnsAnthropicProvider_ForAnthropic()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var factory = new LlmProviderFactory(httpClient);

        // Act
        var provider = factory.GetProvider(LlmProvider.Anthropic);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<AnthropicProvider>();
    }

    [Fact]
    public void GetProvider_ReturnsAzureOpenAiProvider_ForAzureOpenAI()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var factory = new LlmProviderFactory(httpClient);

        // Act
        var provider = factory.GetProvider(LlmProvider.AzureOpenAI);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<AzureOpenAiProvider>();
    }

    [Fact]
    public void GetProvider_ReturnsGoogleGeminiProvider_ForGemini()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var factory = new LlmProviderFactory(httpClient);

        // Act
        var provider = factory.GetProvider(LlmProvider.GoogleGemini);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<GoogleGeminiProvider>();
    }

    [Fact]
    public void GetProvider_ReturnsOllamaProvider_ForLocal()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var factory = new LlmProviderFactory(httpClient);

        // Act
        var provider = factory.GetProvider(LlmProvider.Local);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<OllamaProvider>();
    }

    [Fact]
    public void GetProvider_CachesProviderInstances()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var factory = new LlmProviderFactory(httpClient);

        // Act
        var provider1 = factory.GetProvider(LlmProvider.OpenAI);
        var provider2 = factory.GetProvider(LlmProvider.OpenAI);

        // Assert
        provider1.Should().BeSameAs(provider2);
    }
}
