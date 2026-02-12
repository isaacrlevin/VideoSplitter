using VideoSplitter.Models;

namespace VideoSplitter.Tests.Fixtures;

public static class TestDataFixtures
{
    public static Project CreateTestProject(int id = 1)
    {
        return new Project
        {
            Id = id,
            Name = "Test Project",
            Description = "Test project for unit testing",
            VideoPath = @"C:\test\video.mp4",
            Duration = TimeSpan.FromMinutes(10),
            FileSizeBytes = 1024 * 1024 * 50, // 50 MB
            Status = ProjectStatus.VideoUploaded,
            TranscriptPath = @"C:\test\transcript.txt",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static List<Segment> CreateTestSegments(int projectId = 1, int count = 3)
    {
        var segments = new List<Segment>();
        
        for (int i = 0; i < count; i++)
        {
            segments.Add(new Segment
            {
                Id = i + 1,
                ProjectId = projectId,
                StartTime = TimeSpan.FromSeconds(i * 30),
                EndTime = TimeSpan.FromSeconds((i + 1) * 30),
                TranscriptText = $"This is test segment {i + 1} transcript text.",
                Summary = $"Summary for segment {i + 1}",
                Reasoning = $"This segment is engaging because of reason {i + 1}",
                Status = SegmentStatus.Generated,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        
        return segments;
    }

    public static AppSettings CreateTestAppSettings()
    {
        return new AppSettings
        {
            LlmProvider = LlmProvider.OpenAI,
            OpenAi = new OpenAiSettings { ApiKey = "test-api-key", Model = "gpt-4o-mini" },
            TranscriptProvider = TranscriptProvider.Local,
            DefaultSegmentLengthSeconds = 60,
            DefaultSegmentCount = 5
        };
    }

    public static string CreateTestTranscript()
    {
        return @"[00:00:00] This is a test transcript.
[00:00:05] It contains multiple segments.
[00:00:10] Each segment has engaging content.
[00:00:15] The AI will identify the best parts.
[00:00:20] These parts will become short-form videos.
[00:00:25] Perfect for social media platforms.
[00:00:30] Like TikTok, YouTube Shorts, and Instagram Reels.";
    }

    public static AiSegmentData CreateTestAiSegmentData()
    {
        return new AiSegmentData
        {
            Start = "00:00:00",
            End = "00:00:15",
            Duration = 15,
            Excerpt = "This is a test segment",
            Reasoning = "Test reasoning"
        };
    }
}
