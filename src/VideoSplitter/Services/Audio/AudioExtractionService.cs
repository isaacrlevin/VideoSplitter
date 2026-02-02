using FFMpegCore;

namespace VideoSplitter.Services;

public interface IAudioExtractionService
{
    /// <summary>
    /// Extract audio from video file in a format suitable for transcription
    /// </summary>
    /// <param name="videoPath">Path to the source video file</param>
    /// <param name="audioPath">Path where the extracted audio should be saved</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <returns>Success status and error message if failed</returns>
    Task<(bool Success, string? Error)> ExtractAudioAsync(
        string videoPath,
        string audioPath,
        IProgress<string>? progress = null);
}


/// <summary>
/// Service for extracting audio from video files using FFmpeg
/// </summary>
public class AudioExtractionService : IAudioExtractionService
{
    /// <summary>
    /// Extract audio from video file in a format suitable for speech recognition
    /// </summary>
    /// <param name="videoPath">Path to the source video file</param>
    /// <param name="audioPath">Path where the extracted audio should be saved (should be .wav)</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <returns>Success status and error message if failed</returns>
    public async Task<(bool Success, string? Error)> ExtractAudioAsync(
        string videoPath, 
        string audioPath, 
        IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Extracting audio from video...");

            // Use FFMpegCore for audio extraction with transcription-compatible format
            // Whisper.NET requires: 16KHz sample rate, mono channel, 16-bit signed PCM WAV format
            await FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToFile(audioPath, overwrite: true, options => options
                    .WithAudioSamplingRate(16000)     // 16KHz as required by Whisper
                    .WithCustomArgument("-ac 1")      // Mono channel (1 audio channel)
                    .WithCustomArgument("-acodec pcm_s16le")  // 16-bit signed PCM (little-endian) required by Whisper
                    .ForceFormat("wav"))              // Use WAV format for better compatibility
                .ProcessAsynchronously();

            if (File.Exists(audioPath) && new FileInfo(audioPath).Length > 0)
            {
                progress?.Report("Audio extraction completed successfully");
                return (true, null);
            }
            else
            {
                return (false, "Audio extraction completed but file is empty or missing");
            }
        }
        catch (FFMpegCore.Exceptions.FFMpegException ex)
        {
            return (false, $"FFMpeg audio extraction failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Audio extraction error: {ex.Message}");
        }
    }
}
