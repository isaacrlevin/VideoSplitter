using Microsoft.EntityFrameworkCore;
using VideoSplitter.Data;
using VideoSplitter.Tests.Fixtures;

namespace VideoSplitter.Tests.Helpers;

public static class TestDbContextFactory
{
    public static VideoSplitterDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VideoSplitterDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new VideoSplitterDbContext(options);
        context.Database.EnsureCreated();
        
        return context;
    }

    public static VideoSplitterDbContext CreateInMemoryContextWithData()
    {
        var context = CreateInMemoryContext();
        SeedTestData(context);
        return context;
    }

    private static void SeedTestData(VideoSplitterDbContext context)
    {
        var project = TestDataFixtures.CreateTestProject();
        context.Projects.Add(project);
        
        var segments = TestDataFixtures.CreateTestSegments(project.Id);
        context.Segments.AddRange(segments);
        
        context.SaveChanges();
    }
}
