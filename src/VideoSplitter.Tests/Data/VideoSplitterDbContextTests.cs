using VideoSplitter.Data;
using VideoSplitter.Models;
using VideoSplitter.Tests.Helpers;
using VideoSplitter.Tests.Fixtures;

namespace VideoSplitter.Tests.Data;

public class VideoSplitterDbContextTests : IDisposable
{
    private readonly VideoSplitterDbContext _context;

    public VideoSplitterDbContextTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
    }

    [Fact]
    public void CanAddProject()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();

        // Act
        _context.Projects.Add(project);
        _context.SaveChanges();

        // Assert
        var savedProject = _context.Projects.Find(project.Id);
        savedProject.Should().NotBeNull();
        savedProject!.Name.Should().Be(project.Name);
    }

    [Fact]
    public void CanAddSegmentWithProject()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        _context.Projects.Add(project);
        _context.SaveChanges();

        var segment = TestDataFixtures.CreateTestSegments(project.Id, 1)[0];

        // Act
        _context.Segments.Add(segment);
        _context.SaveChanges();

        // Assert
        var savedSegment = _context.Segments.Find(segment.Id);
        savedSegment.Should().NotBeNull();
        savedSegment!.ProjectId.Should().Be(project.Id);
    }

    [Fact]
    public void DeletingProjectCascadeDeletesSegments()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        _context.Projects.Add(project);
        _context.SaveChanges();

        var segments = TestDataFixtures.CreateTestSegments(project.Id, 3);
        _context.Segments.AddRange(segments);
        _context.SaveChanges();

        // Act
        _context.Projects.Remove(project);
        _context.SaveChanges();

        // Assert
        _context.Segments.Count().Should().Be(0);
    }

    [Fact]
    public void TimeSpanConversionWorks()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        project.Duration = TimeSpan.FromMinutes(5.5);

        // Act
        _context.Projects.Add(project);
        _context.SaveChanges();

        // Assert
        var savedProject = _context.Projects.Find(project.Id);
        savedProject.Should().NotBeNull();
        savedProject!.Duration.Should().Be(TimeSpan.FromMinutes(5.5));
    }

    [Fact]
    public void SegmentTimeSpanConversionWorks()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        _context.Projects.Add(project);
        _context.SaveChanges();

        var segment = new Segment
        {
            ProjectId = project.Id,
            StartTime = TimeSpan.FromSeconds(30.5),
            EndTime = TimeSpan.FromSeconds(60.75),
            TranscriptText = "Test",
            Summary = "Test summary"
        };

        // Act
        _context.Segments.Add(segment);
        _context.SaveChanges();

        // Assert
        var savedSegment = _context.Segments.Find(segment.Id);
        savedSegment.Should().NotBeNull();
        savedSegment!.StartTime.Should().Be(TimeSpan.FromSeconds(30.5));
        savedSegment.EndTime.Should().Be(TimeSpan.FromSeconds(60.75));
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
