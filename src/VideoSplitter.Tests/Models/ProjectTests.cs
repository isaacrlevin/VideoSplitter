using VideoSplitter.Models;
using VideoSplitter.Tests.Fixtures;

namespace VideoSplitter.Tests.Models;

public class ProjectTests
{
    [Fact]
    public void Project_DefaultValues_AreSet()
    {
        // Act
        var project = new Project();

        // Assert
        project.Status.Should().Be(ProjectStatus.VideoUploaded);
        project.Segments.Should().NotBeNull();
        project.Segments.Should().BeEmpty();
        project.Name.Should().Be(string.Empty);
        project.VideoPath.Should().Be(string.Empty);
    }

    [Fact]
    public void Project_CanSetAllProperties()
    {
        // Arrange
        var name = "Test Project";
        var description = "Test Description";
        var videoPath = @"C:\test.mp4";
        var duration = TimeSpan.FromMinutes(5);
        var fileSize = 1024L * 1024 * 50;
        var status = ProjectStatus.TranscriptGenerated;

        // Act
        var project = new Project
        {
            Name = name,
            Description = description,
            VideoPath = videoPath,
            Duration = duration,
            FileSizeBytes = fileSize,
            Status = status
        };

        // Assert
        project.Name.Should().Be(name);
        project.Description.Should().Be(description);
        project.VideoPath.Should().Be(videoPath);
        project.Duration.Should().Be(duration);
        project.FileSizeBytes.Should().Be(fileSize);
        project.Status.Should().Be(status);
    }

    [Fact]
    public void Project_Timestamps_AreUtc()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var project = TestDataFixtures.CreateTestProject();

        // Assert
        project.CreatedAt.Should().BeCloseTo(beforeCreation, TimeSpan.FromSeconds(1));
        project.UpdatedAt.Should().BeCloseTo(beforeCreation, TimeSpan.FromSeconds(1));
        project.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        project.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }
}
