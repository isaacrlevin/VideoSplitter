using VideoSplitter.Models;

namespace VideoSplitter.Services;

public interface IAiService
{
    Task<(bool Success, IEnumerable<Segment>? Segments, string? Error)> GenerateSegmentsAsync(
        Project project,
        string transcriptContent,
        AppSettings settings,
        IProgress<string>? progress = null);

    Task<(bool Success, string? Title, string? Description, string? Error)> GenerateSocialMediaContentAsync(
        string transcriptContent,
        string platformName,
        AppSettings settings,
        IProgress<string>? progress = null);
}