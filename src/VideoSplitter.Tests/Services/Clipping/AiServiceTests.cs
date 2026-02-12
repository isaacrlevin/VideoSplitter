using System.Net;
using RichardSzalay.MockHttp;
using VideoSplitter.Models;
using VideoSplitter.Services;
using VideoSplitter.Tests.Fixtures;

namespace VideoSplitter.Tests.Services.Clipping;

public class AiServiceTests
{
    [Fact]
    public async Task GenerateSegmentsAsync_ReturnsError_WhenTranscriptIsEmpty()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var service = new AiService(httpClient);
        
        var project = TestDataFixtures.CreateTestProject();
        var settings = TestDataFixtures.CreateTestAppSettings();
        
        // Act
        var result = await service.GenerateSegmentsAsync(project, "", settings);
        
        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("empty");
        result.Segments.Should().BeNull();
    }

    [Fact]
    public async Task GenerateSegmentsAsync_ReturnsError_WhenTranscriptIsWhitespace()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var service = new AiService(httpClient);
        
        var project = TestDataFixtures.CreateTestProject();
        var settings = TestDataFixtures.CreateTestAppSettings();
        
        // Act
        var result = await service.GenerateSegmentsAsync(project, "   ", settings);
        
        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task GenerateSegmentsAsync_ReturnsError_WhenProviderNotConfigured()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var service = new AiService(httpClient);
        
        var project = TestDataFixtures.CreateTestProject();
        var settings = TestDataFixtures.CreateTestAppSettings();
        settings.LlmProvider = LlmProvider.OpenAI;
        settings.OpenAi.ApiKey = ""; // Not configured
        
        var transcript = TestDataFixtures.CreateTestTranscript();
        
        // Act
        var result = await service.GenerateSegmentsAsync(project, transcript, settings);
        
        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not configured");
    }

    [Fact]
    public async Task GenerateSegmentsAsync_CallsProgress()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var httpClient = mockHttp.ToHttpClient();
        var service = new AiService(httpClient);
        
        var project = TestDataFixtures.CreateTestProject();
        var settings = TestDataFixtures.CreateTestAppSettings();
        settings.OpenAi.ApiKey = ""; // Will fail but still call progress
        
        var transcript = TestDataFixtures.CreateTestTranscript();
        var progressCalled = false;
        var progress = new Progress<string>(msg =>
        {
            progressCalled = true;
        });
        
        // Act
        await service.GenerateSegmentsAsync(project, transcript, settings, progress);
        
        // Assert
        progressCalled.Should().BeTrue();
    }
}
