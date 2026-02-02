using VideoSplitter.Models;

namespace VideoSplitter.Services;

public interface IAiService
{
    Task<(bool Success, IEnumerable<Segment>? Segments, string? Error)> GenerateSegmentsAsync(
        Project project,
        string transcriptContent,
        AppSettings settings,
        IProgress<string>? progress = null);
}