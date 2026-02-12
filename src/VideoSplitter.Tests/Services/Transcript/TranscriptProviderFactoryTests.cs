using RichardSzalay.MockHttp;
using VideoSplitter.Models;
using VideoSplitter.Services.TranscriptProviders;
using VideoSplitter.Tests.Fixtures;

namespace VideoSplitter.Tests.Services.Transcript;

public class TranscriptProviderFactoryTests
{
    [Fact]
    public void GetProvider_ReturnsWhisperProvider_ForLocal()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var factory = new TranscriptProviderFactory(httpClient);

        // Act
        var provider = factory.GetProvider(TranscriptProvider.Local);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<WhisperTranscriptProvider>();
    }

    [Fact]
    public void GetProvider_ReturnsAzureSpeechProvider_ForAzure()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var factory = new TranscriptProviderFactory(httpClient);

        // Act
        var provider = factory.GetProvider(TranscriptProvider.Azure);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<AzureSpeechProvider>();
    }

    [Fact]
    public void GetProvider_CachesProviderInstances()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var factory = new TranscriptProviderFactory(httpClient);

        // Act
        var provider1 = factory.GetProvider(TranscriptProvider.Local);
        var provider2 = factory.GetProvider(TranscriptProvider.Local);

        // Assert
        provider1.Should().BeSameAs(provider2);
    }
}
