using VideoSplitter.Services;

namespace VideoSplitter.Tests.Services.Audio;

public class AudioExtractionServiceTests
{
    [Fact]
    public async Task ExtractAudioAsync_ReturnsError_WhenVideoPathIsEmpty()
    {
        // Arrange
        var service = new AudioExtractionService();
        
        // Act
        var result = await service.ExtractAudioAsync("", @"C:\test\output.wav");
        
        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractAudioAsync_ReturnsError_WhenVideoPathDoesNotExist()
    {
        // Arrange
        var service = new AudioExtractionService();
        var nonExistentPath = @"C:\nonexistent\video.mp4";
        
        // Act
        var result = await service.ExtractAudioAsync(nonExistentPath, @"C:\test\output.wav");
        
        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExtractAudioAsync_CallsProgress()
    {
        // Arrange
        var service = new AudioExtractionService();
        var progressMessages = new List<string>();
        var progress = new Progress<string>(msg => progressMessages.Add(msg));
        
        // Act
        await service.ExtractAudioAsync(@"C:\nonexistent.mp4", @"C:\test\output.wav", progress);
        
        // Assert
        progressMessages.Should().NotBeEmpty();
        progressMessages.Should().Contain(msg => msg.Contains("Extracting audio"));
    }
}
