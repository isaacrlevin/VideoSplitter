using VideoSplitter.Models;

namespace VideoSplitter.Services;

public interface IAiService
{
    Task<(bool Success, IEnumerable<Segment>? Segments, string? Error)> GenerateSegmentsAsync(
        Project project,
        string transcriptContent,
        AppSettings settings,
        IProgress<string>? progress = null);

    /// <summary>
    /// Generates a channel-neutral title and description that can be reused for every social
    /// platform the clip is posted to.
    /// </summary>
    Task<(bool Success, string? Title, string? Description, string? Error)> GenerateSocialMediaContentAsync(
   string transcriptContent,
   AppSettings settings,
   IProgress<string>? progress = null);
}