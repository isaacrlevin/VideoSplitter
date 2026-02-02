using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Exceptions;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Exceptions;

namespace VideoSplitter.Services;

public interface IVideoService
{
    Task<(bool Success, string? FilePath, string? Error)> DownloadYouTubeVideoAsync(string youTubeUrl, string outputPath, IProgress<double>? progress = null);
    Task<TimeSpan?> GetVideoDurationAsync(string videoPath);
    Task<long> GetFileSizeAsync(string filePath);
    Task<bool> IsValidVideoFileAsync(string filePath);
    Task<string> GenerateThumbnailAsync(string videoPath, string outputPath, TimeSpan position);
    Task<(bool Success, string? Error)> ConfigureFFMpegAsync();
}

public class VideoService : IVideoService
{
    private readonly YoutubeClient _youtubeClient;
    private bool _isFFMpegConfigured = false;

    public VideoService()
    {
        _youtubeClient = new YoutubeClient();
    }

    public async Task<(bool Success, string? Error)> ConfigureFFMpegAsync()
    {
        try
        {
            // Test FFMpeg availability by checking version info
            // We'll use a simple probe command that doesn't require a file
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = processInfo };
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                _isFFMpegConfigured = true;
                Console.WriteLine("FFMpeg configured successfully.");
                return (true, null);
            }
            else
            {
                throw new Exception($"FFMpeg test failed. Exit code: {process.ExitCode}, Error: {error}");
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            // File not found error (FFMpeg not in PATH)
            _isFFMpegConfigured = false;
            var error = "FFMpeg not found in system PATH. Please install FFMpeg and ensure it's accessible from the command line.";
            Console.WriteLine(error);
            return (false, error);
        }
        catch (Exception ex)
        {
            _isFFMpegConfigured = false;
            var error = $"FFMpeg not found or not configured properly: {ex.Message}. " +
                       "Please ensure FFMpeg is installed and accessible in your system PATH.";
            Console.WriteLine(error);
            return (false, error);
        }
    }

    public async Task<(bool Success, string? FilePath, string? Error)> DownloadYouTubeVideoAsync(
        string youTubeUrl, 
        string outputPath, 
        IProgress<double>? progress = null)
    {
        try
        {
            // Validate YouTube URL
            if (!IsValidYouTubeUrl(youTubeUrl))
            {
                return (false, null, "Invalid YouTube URL format");
            }

            // Get video information
            var video = await _youtubeClient.Videos.GetAsync(youTubeUrl);
            
            // Get stream manifest with retry logic for cipher issues
            StreamManifest? streamManifest = null;
            int maxRetries = 3;
            int retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(video.Id);
                    break;
                }
                catch (VideoUnplayableException ex)
                {
                    return (false, null, $"Video is not available: {ex.Message}");
                }
                catch (Exception ex) when (ex.Message.Contains("cipher") || ex.Message.Contains("decrypt"))
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        return (false, null, "Unable to download video due to YouTube protection measures. This video may not be available for download, or YouTube has updated their security. Please try again later or use a different video.");
                    }
                    
                    // Wait before retry
                    await Task.Delay(1000 * retryCount);
                    continue;
                }
            }

            if (streamManifest == null)
            {
                return (false, null, "Failed to get video stream information");
            }

            // Try different stream types in order of preference
            IStreamInfo? streamInfo = null;
            
            // First try: Best quality muxed MP4 streams
            streamInfo = streamManifest.GetMuxedStreams()
                .Where(s => s.Container == Container.Mp4)
                .OrderByDescending(s => s.VideoQuality)
                .FirstOrDefault();

            // Second try: Any muxed streams
            if (streamInfo == null)
            {
                streamInfo = streamManifest.GetMuxedStreams()
                    .OrderByDescending(s => s.VideoQuality)
                    .FirstOrDefault();
            }

            // Third try: Video-only streams (we'll miss audio but at least get video)
            if (streamInfo == null)
            {
                streamInfo = streamManifest.GetVideoOnlyStreams()
                    .Where(s => s.Container == Container.Mp4)
                    .OrderByDescending(s => s.VideoQuality)
                    .FirstOrDefault();
            }

            // Fourth try: Any video-only stream
            if (streamInfo == null)
            {
                streamInfo = streamManifest.GetVideoOnlyStreams()
                    .OrderByDescending(s => s.VideoQuality)
                    .FirstOrDefault();
            }

            if (streamInfo == null)
            {
                return (false, null, "No downloadable streams found for this video");
            }

            // Ensure output directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            // Sanitize filename
            var sanitizedTitle = SanitizeFileName(video.Title);
            var fileName = $"{sanitizedTitle}.{streamInfo.Container}";
            var filePath = Path.Combine(Path.GetDirectoryName(outputPath)!, fileName);

            // Download the stream with progress reporting
            try
            {
                await _youtubeClient.Videos.Streams.DownloadAsync(streamInfo, filePath, progress);
            }
            catch (Exception ex) when (ex.Message.Contains("cipher") || ex.Message.Contains("decrypt"))
            {
                return (false, null, "Download failed due to YouTube protection measures. Please try again later or use a different video.");
            }

            // Verify the downloaded file
            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                return (false, null, "Download completed but file is empty or missing");
            }

            return (true, filePath, null);
        }
        catch (VideoUnplayableException ex)
        {
            return (false, null, $"Video is not available for download: {ex.Message}");
        }
        catch (RequestLimitExceededException)
        {
            return (false, null, "YouTube request limit exceeded. Please try again later.");
        }
        catch (Exception ex)
        {
            // Log the full exception for debugging
            Console.WriteLine($"YouTube download error: {ex}");
            
            if (ex.Message.Contains("cipher") || ex.Message.Contains("decrypt"))
            {
                return (false, null, "Unable to download video due to YouTube protection measures. This may be due to:\n- YouTube's anti-bot protection\n- Recent changes to YouTube's security\n- Video restrictions\n\nPlease try again later or use a different video.");
            }
            
            return (false, null, $"Download failed: {ex.Message}");
        }
    }

    private bool IsValidYouTubeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Check for common YouTube URL patterns
        var youtubePatterns = new[]
        {
            @"^https?://(www\.)?youtube\.com/watch\?v=[\w-]+",
            @"^https?://(www\.)?youtu\.be/[\w-]+",
            @"^https?://(www\.)?youtube\.com/embed/[\w-]+",
            @"^https?://(www\.)?youtube\.com/v/[\w-]+"
        };

        return youtubePatterns.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(url, pattern));
    }

    private string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "video";

        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars));
        
        // Limit length
        if (sanitized.Length > 100)
            sanitized = sanitized.Substring(0, 100);
            
        return string.IsNullOrWhiteSpace(sanitized) ? "video" : sanitized;
    }

    public async Task<TimeSpan?> GetVideoDurationAsync(string videoPath)
    {
        try
        {
            if (!File.Exists(videoPath))
            {
                Console.WriteLine($"Video file not found: {videoPath}");
                return null;
            }

            // Ensure FFMpeg is configured
            if (!_isFFMpegConfigured)
            {
                var configResult = await ConfigureFFMpegAsync();
                if (!configResult.Success)
                {
                    Console.WriteLine($"FFMpeg configuration failed: {configResult.Error}");
                    return null;
                }
            }

            var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
            return mediaInfo.Duration;
        }
        catch (FFMpegException ex)
        {
            Console.WriteLine($"FFMpeg error getting video duration: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting video duration: {ex.Message}");
            return null;
        }
    }

    public async Task<long> GetFileSizeAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return 0;

            return await Task.FromResult(new FileInfo(filePath).Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting file size: {ex.Message}");
            return 0;
        }
    }

    public async Task<bool> IsValidVideoFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return false;
            }

            var validExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".webm", ".m4v", ".flv", ".ogv" };
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (!validExtensions.Contains(extension))
            {
                Console.WriteLine($"Invalid video file extension: {extension}");
                return false;
            }

            // Ensure FFMpeg is configured
            if (!_isFFMpegConfigured)
            {
                var configResult = await ConfigureFFMpegAsync();
                if (!configResult.Success)
                {
                    Console.WriteLine($"FFMpeg configuration failed, falling back to extension check: {configResult.Error}");
                    return true; // Fallback to extension-based validation
                }
            }

            // Use FFProbe to validate the video file structure
            var mediaInfo = await FFProbe.AnalyseAsync(filePath);
            
            // Check if the file has video streams and valid duration
            var hasValidVideoStream = mediaInfo.VideoStreams.Any();
            var hasValidDuration = mediaInfo.Duration > TimeSpan.Zero;
            
            if (!hasValidVideoStream)
            {
                Console.WriteLine($"No valid video streams found in file: {filePath}");
                return false;
            }

            return hasValidVideoStream && hasValidDuration;
        }
        catch (FFMpegException ex)
        {
            Console.WriteLine($"FFMpeg error validating video file: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error validating video file: {ex.Message}");
            return false;
        }
    }

    public async Task<string> GenerateThumbnailAsync(string videoPath, string outputPath, TimeSpan position)
    {
        try
        {
            if (!File.Exists(videoPath))
            {
                Console.WriteLine($"Video file not found: {videoPath}");
                return string.Empty;
            }

            // Ensure FFMpeg is configured
            if (!_isFFMpegConfigured)
            {
                var configResult = await ConfigureFFMpegAsync();
                if (!configResult.Success)
                {
                    Console.WriteLine($"FFMpeg configuration failed: {configResult.Error}");
                    return string.Empty;
                }
            }

            var thumbnailPath = Path.Combine(outputPath, $"thumbnail_{Guid.NewGuid()}.png");
            Directory.CreateDirectory(outputPath);

            // Get video duration to ensure we don't seek beyond the video length
            var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
            var videoDuration = mediaInfo.Duration;
            
            // Adjust position if it's beyond the video duration
            if (position >= videoDuration)
            {
                position = TimeSpan.FromSeconds(Math.Max(0, videoDuration.TotalSeconds - 10)); // 10 seconds before end
            }

            // Use FFMpegCore to generate thumbnail with specific settings
            await FFMpeg.SnapshotAsync(
                videoPath, 
                thumbnailPath, 
                new System.Drawing.Size(480, 270), // 16:9 aspect ratio, reasonable size
                position
            );

            if (File.Exists(thumbnailPath) && new FileInfo(thumbnailPath).Length > 0)
            {
                Console.WriteLine($"Thumbnail generated successfully: {thumbnailPath}");
                return thumbnailPath;
            }
            else
            {
                Console.WriteLine("Thumbnail generation completed but file is empty or missing");
                return string.Empty;
            }
        }
        catch (FFMpegException ex)
        {
            Console.WriteLine($"FFMpeg error generating thumbnail: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating thumbnail: {ex.Message}");
        }

        return string.Empty;
    }
}