using System.Text;
using VideoSplitter.Models;
using VideoSplitter.Services.TranscriptProviders;

namespace VideoSplitter.Services;

public interface ITranscriptService
{
    Task<(bool Success, string? TranscriptPath, string? Error)> GenerateTranscriptAsync(
        string videoPath,
        string outputFolder,
        AppSettings settings,
        IProgress<string>? progress = null);

    Task<string?> ReadTranscriptAsync(string transcriptPath);

    Task<bool> ValidateTranscriptAsync(string transcriptPath);

    /// <summary>
    /// Checks if the Whisper model is available for local transcription
    /// </summary>
    /// <returns>True if the model is available, false otherwise</returns>
    Task<bool> IsWhisperModelAvailableAsync();

    /// <summary>
    /// Gets the expected path for the Whisper model file
    /// </summary>
    /// <returns>The expected model file path</returns>
    string GetExpectedModelPath();

    /// <summary>
    /// Downloads the Whisper model if it's not present
    /// </summary>
    /// <param name="progress">Progress reporter for download status</param>
    /// <returns>True if download was successful or model already exists, false otherwise</returns>
    Task<(bool Success, string? Error)> DownloadWhisperModelAsync(IProgress<string>? progress = null);

    /// <summary>
    /// Initializes and loads the Whisper model, downloading it if necessary
    /// </summary>
    /// <param name="progress">Progress reporter for initialization status</param>
    /// <returns>True if model is loaded and ready, false otherwise</returns>
    Task<(bool Success, string? Error)> InitializeWhisperModelAsync(IProgress<string>? progress = null);

    /// <summary>
    /// Gets the download progress and status of the Whisper model
    /// </summary>
    /// <returns>Status information about the model</returns>
    Task<(bool IsAvailable, bool IsDownloading, string Status)> GetModelStatusAsync();
}
public class TranscriptService : ITranscriptService, IDisposable
{
    private readonly TranscriptProviderFactory _providerFactory;
    private readonly WhisperTranscriptProvider _whisperProvider;
    private readonly IAudioExtractionService _audioExtractionService;
    private bool _disposed = false;

    public TranscriptService(HttpClient httpClient, IAudioExtractionService audioExtractionService)
    {
        _providerFactory = new TranscriptProviderFactory(httpClient);
        _whisperProvider = new WhisperTranscriptProvider(httpClient);
        _audioExtractionService = audioExtractionService;
    }

    public async Task<(bool Success, string? TranscriptPath, string? Error)> GenerateTranscriptAsync(
        string videoPath,
        string outputFolder,
        AppSettings settings,
        IProgress<string>? progress = null)
    {
        string? audioPath = null;

        try
        {
            if (!File.Exists(videoPath))
            {
                return (false, null, "Video file not found");
            }

            Directory.CreateDirectory(outputFolder);
            var transcriptFileName = $"transcript_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            var transcriptPath = Path.Combine(outputFolder, transcriptFileName);

            // Extract audio from video first
            audioPath = Path.ChangeExtension(transcriptPath, ".wav");
            
            progress?.Report("Extracting audio from video...");
            var extractResult = await _audioExtractionService.ExtractAudioAsync(videoPath, audioPath, progress);
            
            if (!extractResult.Success)
            {
                progress?.Report($"Audio extraction failed: {extractResult.Error}");
                return (false, null, $"Audio extraction failed: {extractResult.Error}");
            }

            // Generate transcript using the selected provider
            var provider = _providerFactory.GetProvider(settings.TranscriptProvider);
            return await provider.GenerateTranscriptAsync(audioPath, transcriptPath, settings, progress);
        }
        catch (Exception ex)
        {
            return (false, null, $"Transcript generation failed: {ex.Message}");
        }
        finally
        {
            // Clean up the temporary audio file
            if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
            {
                try
                {
                    File.Delete(audioPath);
                    progress?.Report("Temporary audio file cleaned up");
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    public async Task<string?> ReadTranscriptAsync(string transcriptPath)
    {
        try
        {
            if (!File.Exists(transcriptPath))
            {
                return null;
            }

            return await File.ReadAllTextAsync(transcriptPath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading transcript: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> ValidateTranscriptAsync(string transcriptPath)
    {
        try
        {
            if (!File.Exists(transcriptPath))
            {
                return false;
            }

            var content = await File.ReadAllTextAsync(transcriptPath);
            return !string.IsNullOrWhiteSpace(content) && content.Length > 10;
        }
        catch
        {
            return false;
        }
    }

    // Whisper-specific methods delegate to the Whisper provider instance
    public async Task<bool> IsWhisperModelAvailableAsync()
    {
        return await _whisperProvider.IsWhisperModelAvailableAsync();
    }

    public string GetExpectedModelPath()
    {
        return _whisperProvider.GetExpectedModelPath();
    }

    public async Task<(bool Success, string? Error)> DownloadWhisperModelAsync(IProgress<string>? progress = null)
    {
        return await _whisperProvider.DownloadWhisperModelAsync(progress);
    }

    public async Task<(bool Success, string? Error)> InitializeWhisperModelAsync(IProgress<string>? progress = null)
    {
        return await _whisperProvider.InitializeWhisperModelAsync(progress);
    }

    public async Task<(bool IsAvailable, bool IsDownloading, string Status)> GetModelStatusAsync()
    {
        return await _whisperProvider.GetStatusAsync();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _whisperProvider?.Dispose();
            _disposed = true;
        }
    }
}