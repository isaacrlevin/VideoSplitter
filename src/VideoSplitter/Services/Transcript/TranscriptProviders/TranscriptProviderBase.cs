using System.Text;
using VideoSplitter.Models;

namespace VideoSplitter.Services.TranscriptProviders;

/// <summary>
/// Interface for transcript providers that can generate transcripts from video/audio files
/// </summary>
public interface ITranscriptProvider
{
    /// <summary>
    /// Generate a transcript from a video file
    /// </summary>
    /// <param name="videoPath">Path to the video file</param>
    /// <param name="audioPath">Path to the extracted audio file (WAV, 16KHz, mono)</param>
    /// <param name="transcriptPath">Path where the transcript should be saved</param>
    /// <param name="settings">Application settings</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <returns>Success status, transcript path, and error message if failed</returns>
    Task<(bool Success, string? TranscriptPath, string? Error)> GenerateTranscriptAsync(
        string audioPath,
        string transcriptPath,
        AppSettings settings,
        IProgress<string>? progress = null);

    /// <summary>
    /// Indicates whether this provider is configured and ready to use
    /// </summary>
    bool IsConfigured(AppSettings settings);

    /// <summary>
    /// Gets the status of the provider
    /// </summary>
    Task<(bool IsAvailable, bool IsDownloading, string Status)> GetStatusAsync();
}

/// <summary>
/// Base class providing common functionality for all transcript providers
/// </summary>
public abstract class TranscriptProviderBase : ITranscriptProvider
{
    public abstract bool IsConfigured(AppSettings settings);
    public abstract Task<(bool Success, string? TranscriptPath, string? Error)> GenerateTranscriptAsync(
        string audioPath,
        string transcriptPath, 
        AppSettings settings, 
        IProgress<string>? progress = null);
    public abstract Task<(bool IsAvailable, bool IsDownloading, string Status)> GetStatusAsync();

    /// <summary>
    /// Format bytes for human-readable display
    /// </summary>
    protected string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
        return $"{bytes / (1024 * 1024 * 1024):F1} GB";
    }
}
