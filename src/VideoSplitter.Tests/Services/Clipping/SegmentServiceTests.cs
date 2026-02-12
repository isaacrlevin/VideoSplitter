using VideoSplitter.Data;
using VideoSplitter.Models;
using VideoSplitter.Services;
using VideoSplitter.Tests.Helpers;
using VideoSplitter.Tests.Fixtures;

namespace VideoSplitter.Tests.Services.Clipping;

public class SegmentServiceTests : IDisposable
{
    private readonly VideoSplitterDbContext _context;
    private readonly SegmentService _service;

    public SegmentServiceTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _service = new SegmentService(_context);
    }

    [Fact]
    public async Task GetSegmentsByProjectIdAsync_ReturnsSegmentsOrderedByStartTime()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var segments = new List<Segment>
        {
            new Segment
            {
                ProjectId = project.Id,
                StartTime = TimeSpan.FromSeconds(60),
                EndTime = TimeSpan.FromSeconds(90),
                TranscriptText = "Second segment",
                Summary = "Summary 2"
            },
            new Segment
            {
                ProjectId = project.Id,
                StartTime = TimeSpan.FromSeconds(0),
                EndTime = TimeSpan.FromSeconds(30),
                TranscriptText = "First segment",
                Summary = "Summary 1"
            },
            new Segment
            {
                ProjectId = project.Id,
                StartTime = TimeSpan.FromSeconds(30),
                EndTime = TimeSpan.FromSeconds(60),
                TranscriptText = "Third segment",
                Summary = "Summary 3"
            }
        };
        
        _context.Segments.AddRange(segments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSegmentsByProjectIdAsync(project.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.First().StartTime.Should().Be(TimeSpan.FromSeconds(0));
        result.Last().StartTime.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task GetSegmentByIdAsync_ReturnsSegmentWithProject()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var segment = TestDataFixtures.CreateTestSegments(project.Id, 1)[0];
        _context.Segments.Add(segment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetSegmentByIdAsync(segment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(segment.Id);
        result.Project.Should().NotBeNull();
        result.Project.Id.Should().Be(project.Id);
    }

    [Fact]
    public async Task CreateSegmentAsync_SetsTimestamps()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var segment = new Segment
        {
            ProjectId = project.Id,
            StartTime = TimeSpan.FromSeconds(0),
            EndTime = TimeSpan.FromSeconds(30),
            TranscriptText = "Test",
            Summary = "Test summary"
        };

        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _service.CreateSegmentAsync(segment);

        // Assert
        result.Should().NotBeNull();
        result.CreatedAt.Should().BeCloseTo(beforeCreate, TimeSpan.FromSeconds(2));
        result.UpdatedAt.Should().BeCloseTo(beforeCreate, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task UpdateSegmentAsync_UpdatesTimestamp()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var segment = TestDataFixtures.CreateTestSegments(project.Id, 1)[0];
        _context.Segments.Add(segment);
        await _context.SaveChangesAsync();

        var originalUpdatedAt = segment.UpdatedAt;
        await Task.Delay(100); // Ensure time difference

        segment.Summary = "Updated summary";

        // Act
        var result = await _service.UpdateSegmentAsync(segment);

        // Assert
        result.Should().NotBeNull();
        result.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        result.Summary.Should().Be("Updated summary");
    }

    [Fact]
    public async Task DeleteSegmentAsync_RemovesSegment()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var segment = TestDataFixtures.CreateTestSegments(project.Id, 1)[0];
        _context.Segments.Add(segment);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteSegmentAsync(segment.Id);

        // Assert
        var deletedSegment = await _service.GetSegmentByIdAsync(segment.Id);
        deletedSegment.Should().BeNull();
    }

    [Fact]
    public async Task CreateSegmentsAsync_CreatesMultipleSegments()
    {
        // Arrange
        var project = TestDataFixtures.CreateTestProject();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var segments = TestDataFixtures.CreateTestSegments(project.Id, 5);

        // Act
        var result = await _service.CreateSegmentsAsync(segments);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
        
        var savedSegments = await _service.GetSegmentsByProjectIdAsync(project.Id);
        savedSegments.Should().HaveCount(5);
    }

    [Fact]
    public async Task DeleteAllSegmentsAsync_RemovesAllSegments()
    {
        // Arrange
        var project1 = TestDataFixtures.CreateTestProject(1);
        var project2 = TestDataFixtures.CreateTestProject(2);
        _context.Projects.AddRange(project1, project2);
        await _context.SaveChangesAsync();

        // Create segments without IDs - let DB assign them
        var segment1 = new Segment
        {
            ProjectId = project1.Id,
            StartTime = TimeSpan.FromSeconds(0),
            EndTime = TimeSpan.FromSeconds(30),
            TranscriptText = "Test 1",
            Summary = "Summary 1"
        };
        var segment2 = new Segment
        {
            ProjectId = project1.Id,
            StartTime = TimeSpan.FromSeconds(30),
            EndTime = TimeSpan.FromSeconds(60),
            TranscriptText = "Test 2",
            Summary = "Summary 2"
        };
        var segment3 = new Segment
        {
            ProjectId = project2.Id,
            StartTime = TimeSpan.FromSeconds(0),
            EndTime = TimeSpan.FromSeconds(30),
            TranscriptText = "Test 3",
            Summary = "Summary 3"
        };
        
        _context.Segments.AddRange(segment1, segment2, segment3);
        await _context.SaveChangesAsync();

        // Act
        var deletedCount = await _service.DeleteAllSegmentsAsync();

        // Assert
        deletedCount.Should().Be(3);
        _context.Segments.Count().Should().Be(0);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
