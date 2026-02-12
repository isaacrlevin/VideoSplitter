using VideoSplitter.Models;
using VideoSplitter.Tests.Fixtures;

namespace VideoSplitter.Tests.Models;

public class SegmentTests
{
    [Fact]
    public void Segment_DefaultValues_AreSet()
    {
        // Act
        var segment = new Segment();

        // Assert
        segment.Status.Should().Be(SegmentStatus.Generated);
        segment.TranscriptText.Should().Be(string.Empty);
        segment.Summary.Should().Be(string.Empty);
    }

    [Fact]
    public void Segment_CanSetAllProperties()
    {
        // Arrange
        var projectId = 1;
        var startTime = TimeSpan.FromSeconds(0);
        var endTime = TimeSpan.FromSeconds(30);
        var transcriptText = "Test transcript";
        var summary = "Test summary";
        var reasoning = "Test reasoning";
        var status = SegmentStatus.Approved;
        var clipPath = @"C:\clips\test.mp4";

        // Act
        var segment = new Segment
        {
            ProjectId = projectId,
            StartTime = startTime,
            EndTime = endTime,
            TranscriptText = transcriptText,
            Summary = summary,
            Reasoning = reasoning,
            Status = status,
            ClipPath = clipPath
        };

        // Assert
        segment.ProjectId.Should().Be(projectId);
        segment.StartTime.Should().Be(startTime);
        segment.EndTime.Should().Be(endTime);
        segment.TranscriptText.Should().Be(transcriptText);
        segment.Summary.Should().Be(summary);
        segment.Reasoning.Should().Be(reasoning);
        segment.Status.Should().Be(status);
        segment.ClipPath.Should().Be(clipPath);
    }

    [Fact]
    public void Segment_Duration_IsCalculatedCorrectly()
    {
        // Arrange
        var segment = new Segment
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(40)
        };

        // Act
        var duration = segment.EndTime - segment.StartTime;

        // Assert
        duration.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Segment_Timestamps_AreUtc()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var segment = TestDataFixtures.CreateTestSegments(1, 1)[0];

        // Assert
        segment.CreatedAt.Should().BeCloseTo(beforeCreation, TimeSpan.FromSeconds(1));
        segment.UpdatedAt.Should().BeCloseTo(beforeCreation, TimeSpan.FromSeconds(1));
        segment.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        segment.UpdatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(SegmentStatus.Generated)]
    [InlineData(SegmentStatus.Approved)]
    [InlineData(SegmentStatus.Extracting)]
    [InlineData(SegmentStatus.Extracted)]
    [InlineData(SegmentStatus.Failed)]
    public void Segment_CanSetAllStatusValues(SegmentStatus status)
    {
        // Arrange
        var segment = new Segment();

        // Act
        segment.Status = status;

        // Assert
        segment.Status.Should().Be(status);
    }
}
