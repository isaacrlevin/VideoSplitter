using FFMpegCore;
using VideoSplitter.Models;
using VideoSplitter.Services.TranscriptProviders;

namespace VideoSplitter.Services;

/// <summary>
/// Aspect ratio conversion mode for video extraction.
/// </summary>
public enum AspectRatioMode
{
    /// <summary>Keep the original aspect ratio.</summary>
    Original,

    /// <summary>Convert to 9:16 vertical by cropping the center of the video.</summary>
    VerticalCrop,

    /// <summary>Convert to 9:16 vertical with blurred background.</summary>
    VerticalBlurBackground,

    /// <summary>Convert to 9:16 by stacking left and right halves vertically (for full-frame split screens).</summary>
    VerticalStackSplitScreen,

    /// <summary>Convert to 9:16 by cropping out the header then stacking speakers (ideal for podcasts with title bars).</summary>
    VerticalStackPodcast,

    /// <summary>Convert to 9:16 with letterboxing (black bars top/bottom, preserves full width).</summary>
    VerticalLetterbox
}

public interface IVideoExtractionService
{
    Task<ExtractionResult> ExtractSegmentAsync(
        Project project,
        Segment segment,
        AspectRatioMode aspectRatioMode = AspectRatioMode.Original,
        IProgress<double>? progress = null);

    Task<ExtractionResult> ExtractSegmentAsync(
        Project project,
        Segment segment,
        AspectRatioMode aspectRatioMode,
        SubtitleOptions? subtitleOptions,
        IProgress<double>? progress = null);

    string GetClipUrl(string clipPath);
}

public class VideoExtractionService : IVideoExtractionService
{
    private readonly IProjectService _projectService;
    private readonly IFileStreamService _fileStreamService;
    private readonly ISubtitleService _subtitleService;
    private readonly IAudioExtractionService _audioExtractionService;
    private readonly ITranscriptService _transcriptService;
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;

    // Standard vertical video resolution for TikTok/YouTube Shorts
    private const int VerticalWidth = 1080;
    private const int VerticalHeight = 1920;

    public VideoExtractionService(
        IProjectService projectService, 
        IFileStreamService fileStreamService,
        ISubtitleService subtitleService,
        IAudioExtractionService audioExtractionService,
        ITranscriptService transcriptService,
        ISettingsService settingsService,
        HttpClient httpClient)
    {
        _projectService = projectService;
        _fileStreamService = fileStreamService;
        _subtitleService = subtitleService;
        _audioExtractionService = audioExtractionService;
        _transcriptService = transcriptService;
        _settingsService = settingsService;
        _httpClient = httpClient;
    }

    public Task<ExtractionResult> ExtractSegmentAsync(
        Project project,
        Segment segment,
        AspectRatioMode aspectRatioMode = AspectRatioMode.Original,
        IProgress<double>? progress = null)
    {
        return ExtractSegmentAsync(project, segment, aspectRatioMode, null, progress);
    }

    public async Task<ExtractionResult> ExtractSegmentAsync(
        Project project,
        Segment segment,
        AspectRatioMode aspectRatioMode,
        SubtitleOptions? subtitleOptions,
        IProgress<double>? progress = null)
    {
        string? tempVideoPath = null;
        string? tempAudioPath = null;
        string? tempTranscriptPath = null;
        string? tempSrtPath = null;
        
        try
        {
            if (!File.Exists(project.VideoPath))
            {
                return new ExtractionResult
                {
                    Success = false,
                    Error = "Video file not found."
                };
            }

            // Create clips folder for this project
            var projectFolder = await _projectService.CreateProjectFolderAsync(project.Id);
            var clipsFolder = Path.Combine(projectFolder, "clips");
            Directory.CreateDirectory(clipsFolder);

            // Generate filename based on aspect ratio mode and subtitle setting
            var segmentNumber = GetSegmentNumber(project, segment);
            var sanitizedProjectName = SanitizeFileName(project.Name);
            var suffix = aspectRatioMode switch
            {
                AspectRatioMode.VerticalCrop => "-vertical-crop",
                AspectRatioMode.VerticalBlurBackground => "-vertical-blur",
                AspectRatioMode.VerticalStackSplitScreen => "-vertical-stack",
                AspectRatioMode.VerticalStackPodcast => "-vertical-podcast",
                AspectRatioMode.VerticalLetterbox => "-vertical-letterbox",
                _ => ""
            };
            
            if (subtitleOptions?.Enabled == true)
            {
                suffix += "-subtitled";
            }
            
            var fileName = $"{sanitizedProjectName}-{segmentNumber}{suffix}.mp4";
            var outputPath = Path.Combine(clipsFolder, fileName);

            // Step 1: Extract the segment WITHOUT subtitles
            var duration = segment.EndTime - segment.StartTime;
            
            if (subtitleOptions?.Enabled == true)
            {
                // Create temporary video file (without subtitles)
                tempVideoPath = Path.Combine(clipsFolder, $"{sanitizedProjectName}-{segmentNumber}-temp.mp4");
                
                progress?.Report(0.1);
                await FFMpegArguments
                    .FromFileInput(project.VideoPath, false, options => options
                        .Seek(segment.StartTime))
                    .OutputToFile(tempVideoPath, true, options =>
                    {
                        options
                            .WithDuration(duration)
                            .WithVideoCodec("libx264")
                            .WithAudioCodec("aac")
                            .WithFastStart();

                        // Apply aspect ratio filters only (no subtitles)
                        ApplyVideoFilters(options, aspectRatioMode, null, null);
                    })
                    .ProcessAsynchronously();

                if (!File.Exists(tempVideoPath))
                {
                    return new ExtractionResult
                    {
                        Success = false,
                        Error = "Failed to create temporary clip file."
                    };
                }

                progress?.Report(0.3);

                // Step 2: Extract audio from the clipped video
                tempAudioPath = Path.Combine(clipsFolder, $"{sanitizedProjectName}-{segmentNumber}-temp.wav");
                var audioResult = await _audioExtractionService.ExtractAudioAsync(tempVideoPath, tempAudioPath);
                
                if (!audioResult.Success)
                {
                    return new ExtractionResult
                    {
                        Success = false,
                        Error = $"Failed to extract audio from clip: {audioResult.Error}"
                    };
                }

                progress?.Report(0.5);

                // Step 3: Generate transcript from the clipped audio
                // The audio file is already in the correct format for transcription
                tempTranscriptPath = Path.Combine(clipsFolder, $"{sanitizedProjectName}-{segmentNumber}-temp.txt");
                
                // Get current settings to use the configured transcript provider
                var appSettings = await _settingsService.GetSettingsAsync();
                
                // Use the TranscriptService provider factory to get the configured provider
                var transcriptProvider = new TranscriptProviderFactory(_httpClient)
                    .GetProvider(appSettings.TranscriptProvider);
                
                var transcriptResult = await transcriptProvider.GenerateTranscriptAsync(
                    tempAudioPath,       // Audio path (already in WAV 16KHz mono format)
                    tempTranscriptPath,  // Output transcript path
                    appSettings,
                    new Progress<string>(msg => { /* Progress updates */ }));
                
                if (!transcriptResult.Success)
                {
                    return new ExtractionResult
                    {
                        Success = false,
                        Error = $"Failed to generate transcript for clip: {transcriptResult.Error}"
                    };
                }

                progress?.Report(0.7);

                // Step 4: Generate SRT from the new transcript
                tempSrtPath = Path.Combine(clipsFolder, $"{sanitizedProjectName}-{segmentNumber}-temp.srt");
                var srtResult = await _subtitleService.GenerateSrtFromWhisperAsync(tempTranscriptPath, tempSrtPath);
                
                if (!srtResult.Success)
                {
                    return new ExtractionResult
                    {
                        Success = false,
                        Error = $"Failed to generate SRT file: {srtResult.Error}"
                    };
                }

                progress?.Report(0.8);

                // Step 5: Burn subtitles onto the video
                await FFMpegArguments
                    .FromFileInput(tempVideoPath, false)
                    .OutputToFile(outputPath, true, options =>
                    {
                        options
                            .WithVideoCodec("libx264")
                            .WithAudioCodec("copy")
                            .WithFastStart();

                        // Apply subtitle filter only
                        ApplySubtitleFilter(options, subtitleOptions, tempSrtPath);
                    })
                    .ProcessAsynchronously();

                progress?.Report(1.0);
            }
            else
            {
                // No subtitles needed - extract directly
                await FFMpegArguments
                    .FromFileInput(project.VideoPath, false, options => options
                        .Seek(segment.StartTime))
                    .OutputToFile(outputPath, true, options =>
                    {
                        options
                            .WithDuration(duration)
                            .WithVideoCodec("libx264")
                            .WithAudioCodec("aac")
                            .WithFastStart();

                        // Apply aspect ratio filters only
                        ApplyVideoFilters(options, aspectRatioMode, null, null);
                    })
                    .NotifyOnProgress(percent => progress?.Report(percent), duration)
                    .ProcessAsynchronously();
            }

            if (!File.Exists(outputPath))
            {
                return new ExtractionResult
                {
                    Success = false,
                    Error = "Failed to create clip file."
                };
            }

            return new ExtractionResult
            {
                Success = true,
                ClipPath = outputPath
            };
        }
        catch (Exception ex)
        {
            return new ExtractionResult
            {
                Success = false,
                Error = $"Extraction failed: {ex.Message}"
            };
        }
        finally
        {
            // Step 6: Clean up temporary files
            CleanupTempFile(tempVideoPath);
            CleanupTempFile(tempAudioPath);
            CleanupTempFile(tempTranscriptPath);
            CleanupTempFile(tempSrtPath);
        }
    }

    private static void CleanupTempFile(string? filePath)
    {
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public string GetClipUrl(string clipPath)
    {
        if (string.IsNullOrEmpty(clipPath) || !File.Exists(clipPath))
            return string.Empty;

        // Use the same encoding approach as videos
        var encodedPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(clipPath));
        return $"data:video-path;base64,{encodedPath}";
    }

    /// <summary>
    /// Applies the appropriate FFmpeg video filter based on the aspect ratio mode.
    /// </summary>
    private static void ApplyAspectRatioFilter(FFMpegArgumentOptions options, AspectRatioMode mode)
    {
        switch (mode)
        {
            case AspectRatioMode.VerticalCrop:
                // Crop the center of the video to 9:16 aspect ratio
                options.WithCustomArgument(
                    $"-vf \"crop=ih*9/16:ih,scale={VerticalWidth}:{VerticalHeight}:flags=lanczos\"");
                break;

            case AspectRatioMode.VerticalBlurBackground:
                // Blurred background with centered original video
                var blurFilter =
                    $"[0:v]scale={VerticalWidth}:{VerticalHeight}:force_original_aspect_ratio=increase," +
                    $"crop={VerticalWidth}:{VerticalHeight},boxblur=25:5[bg];" +
                    $"[0:v]scale={VerticalWidth}:{VerticalHeight}:force_original_aspect_ratio=decrease[fg];" +
                    $"[bg][fg]overlay=(W-w)/2:(H-h)/2";
                options.WithCustomArgument($"-filter_complex \"{blurFilter}\"");
                break;

            case AspectRatioMode.VerticalStackSplitScreen:
                // Stack left and right halves vertically (for full-frame split screens)
                // 1. Crop left half, scale to 1080x960
                // 2. Crop right half, scale to 1080x960
                // 3. Stack vertically to get 1080x1920
                var stackFilter =
                    $"[0:v]crop=iw/2:ih:0:0,scale={VerticalWidth}:{VerticalHeight / 2}[top];" +
                    $"[0:v]crop=iw/2:ih:iw/2:0,scale={VerticalWidth}:{VerticalHeight / 2}[bottom];" +
                    $"[top][bottom]vstack";
                options.WithCustomArgument($"-filter_complex \"{stackFilter}\"");
                break;

            case AspectRatioMode.VerticalStackPodcast:
                // For podcast layouts with header bar: crop out top ~15%, then stack speakers
                // This removes the title/header and keeps just the two speaker boxes
                // 1. Crop out the header (top 15% of video)
                // 2. Split remaining content left/right
                // 3. Stack vertically
                //var podcastFilter =
                //    $"[0:v]crop=iw:ih*0.85:0:ih*0.15,split[left][right];" +
                //    $"[left]crop=iw/2:ih:0:0,scale={VerticalWidth}:{VerticalHeight / 2}[top];" +
                //    $"[right]crop=iw/2:ih:iw/2:0,scale={VerticalWidth}:{VerticalHeight / 2}[bottom];" +
                //    $"[top][bottom]vstack";
                //options.WithCustomArgument($"-filter_complex \"{podcastFilter}\"");
                //break;

                var podcastFilter =
    $"[0:v]crop=iw*0.90:ih*0.95:iw*0.05:0," +           // Remove 5% borders (L/R/B)
    $"crop=iw:ih*0.80:0:ih*0.20,split[left][right];" +  // Remove top 15% header
    $"[left]crop=iw/2:ih:0:0,scale={VerticalWidth}:{VerticalHeight / 2}[top];" +
    $"[right]crop=iw/2:ih:iw/2:0,scale={VerticalWidth}:{VerticalHeight / 2}[bottom];" +
    $"[top][bottom]vstack";
                options.WithCustomArgument($"-filter_complex \"{podcastFilter}\"");
                break;


            case AspectRatioMode.VerticalLetterbox:
                // Letterbox: scale to fit width, add black bars top/bottom
                var letterboxFilter =
                    $"scale={VerticalWidth}:-2," +
                    $"pad={VerticalWidth}:{VerticalHeight}:(ow-iw)/2:(oh-ih)/2:black";
                options.WithCustomArgument($"-vf \"{letterboxFilter}\"");
                break;

            case AspectRatioMode.Original:
            default:
                // No filter needed
                break;
        }
    }

    /// <summary>
    /// Applies video filters including aspect ratio conversion and subtitle burning.
    /// </summary>
    private static void ApplyVideoFilters(
        FFMpegArgumentOptions options, 
        AspectRatioMode mode, 
        SubtitleOptions? subtitleOptions,
        string? srtPath)
    {
        var hasSubtitles = subtitleOptions?.Enabled == true && !string.IsNullOrEmpty(srtPath) && File.Exists(srtPath);
        
        // Check if SRT file has content (not empty)
        if (hasSubtitles)
        {
            var srtContent = File.ReadAllText(srtPath!);
            hasSubtitles = !string.IsNullOrWhiteSpace(srtContent);
        }
        
        // Build subtitle filter string
        string GetSubtitleFilter(string? outputLabel = null)
        {
            if (!hasSubtitles) return string.Empty;
            
            // Escape special characters in file path for FFmpeg
            var escapedPath = srtPath!
                .Replace("\\", "/")
                .Replace(":", "\\:")
                .Replace("'", "\\'");
            
            var style = subtitleOptions!.GetFFmpegStyleString();
            var subtitleFilter = $"subtitles='{escapedPath}':force_style='{style}'";
            
            return outputLabel != null 
                ? $"{subtitleFilter}[{outputLabel}]" 
                : subtitleFilter;
        }

        switch (mode)
        {
            case AspectRatioMode.VerticalCrop:
                if (hasSubtitles)
                {
                    // Crop, scale, then apply subtitles
                    var cropSubFilter = $"crop=ih*9/16:ih,scale={VerticalWidth}:{VerticalHeight}:flags=lanczos,{GetSubtitleFilter()}";
                    options.WithCustomArgument($"-vf \"{cropSubFilter}\"");
                }
                else
                {
                    options.WithCustomArgument(
                        $"-vf \"crop=ih*9/16:ih,scale={VerticalWidth}:{VerticalHeight}:flags=lanczos\"");
                }
                break;

            case AspectRatioMode.VerticalBlurBackground:
                if (hasSubtitles)
                {
                    var blurSubFilter =
                        $"[0:v]scale={VerticalWidth}:{VerticalHeight}:force_original_aspect_ratio=increase," +
                        $"crop={VerticalWidth}:{VerticalHeight},boxblur=25:5[bg];" +
                        $"[0:v]scale={VerticalWidth}:{VerticalHeight}:force_original_aspect_ratio=decrease[fg];" +
                        $"[bg][fg]overlay=(W-w)/2:(H-h)/2,{GetSubtitleFilter()}";
                    options.WithCustomArgument($"-filter_complex \"{blurSubFilter}\"");
                }
                else
                {
                    var blurFilter =
                        $"[0:v]scale={VerticalWidth}:{VerticalHeight}:force_original_aspect_ratio=increase," +
                        $"crop={VerticalWidth}:{VerticalHeight},boxblur=25:5[bg];" +
                        $"[0:v]scale={VerticalWidth}:{VerticalHeight}:force_original_aspect_ratio=decrease[fg];" +
                        $"[bg][fg]overlay=(W-w)/2:(H-h)/2";
                    options.WithCustomArgument($"-filter_complex \"{blurFilter}\"");
                }
                break;

            case AspectRatioMode.VerticalStackSplitScreen:
                if (hasSubtitles)
                {
                    var stackSubFilter =
                        $"[0:v]crop=iw/2:ih:0:0,scale={VerticalWidth}:{VerticalHeight / 2}[top];" +
                        $"[0:v]crop=iw/2:ih:iw/2:0,scale={VerticalWidth}:{VerticalHeight / 2}[bottom];" +
                        $"[top][bottom]vstack,{GetSubtitleFilter()}";
                    options.WithCustomArgument($"-filter_complex \"{stackSubFilter}\"");
                }
                else
                {
                    var stackFilter =
                        $"[0:v]crop=iw/2:ih:0:0,scale={VerticalWidth}:{VerticalHeight / 2}[top];" +
                        $"[0:v]crop=iw/2:ih:iw/2:0,scale={VerticalWidth}:{VerticalHeight / 2}[bottom];" +
                        $"[top][bottom]vstack";
                    options.WithCustomArgument($"-filter_complex \"{stackFilter}\"");
                }
                break;

            case AspectRatioMode.VerticalStackPodcast:
                if (hasSubtitles)
                {
                    var podcastSubFilter =
                        $"[0:v]crop=iw*0.90:ih*0.95:iw*0.05:0," +
                        $"crop=iw:ih*0.80:0:ih*0.20,split[left][right];" +
                        $"[left]crop=iw/2:ih:0:0,scale={VerticalWidth}:{VerticalHeight / 2}[top];" +
                        $"[right]crop=iw/2:ih:iw/2:0,scale={VerticalWidth}:{VerticalHeight / 2}[bottom];" +
                        $"[top][bottom]vstack,{GetSubtitleFilter()}";
                    options.WithCustomArgument($"-filter_complex \"{podcastSubFilter}\"");
                }
                else
                {
                    var podcastFilter =
                        $"[0:v]crop=iw*0.90:ih*0.95:iw*0.05:0," +
                        $"crop=iw:ih*0.80:0:ih*0.20,split[left][right];" +
                        $"[left]crop=iw/2:ih:0:0,scale={VerticalWidth}:{VerticalHeight / 2}[top];" +
                        $"[right]crop=iw/2:ih:iw/2:0,scale={VerticalWidth}:{VerticalHeight / 2}[bottom];" +
                        $"[top][bottom]vstack";
                    options.WithCustomArgument($"-filter_complex \"{podcastFilter}\"");
                }
                break;

            case AspectRatioMode.VerticalLetterbox:
                if (hasSubtitles)
                {
                    var letterboxSubFilter =
                        $"scale={VerticalWidth}:-2," +
                        $"pad={VerticalWidth}:{VerticalHeight}:(ow-iw)/2:(oh-ih)/2:black," +
                        GetSubtitleFilter();
                    options.WithCustomArgument($"-vf \"{letterboxSubFilter}\"");
                }
                else
                {
                    var letterboxFilter =
                        $"scale={VerticalWidth}:-2," +
                        $"pad={VerticalWidth}:{VerticalHeight}:(ow-iw)/2:(oh-ih)/2:black";
                    options.WithCustomArgument($"-vf \"{letterboxFilter}\"");
                }
                break;

            case AspectRatioMode.Original:
            default:
                if (hasSubtitles)
                {
                    options.WithCustomArgument($"-vf \"{GetSubtitleFilter()}\"");
                }
                // No filter needed for original without subtitles
                break;
        }
    }

    /// <summary>
    /// Applies subtitle filter to the video.
    /// </summary>
    private static void ApplySubtitleFilter(
        FFMpegArgumentOptions options,
        SubtitleOptions? subtitleOptions,
        string? srtPath)
    {
        if (subtitleOptions?.Enabled != true || string.IsNullOrEmpty(srtPath) || !File.Exists(srtPath))
            return;

        // Check if SRT file has content (not empty)
        var srtContent = File.ReadAllText(srtPath);
        if (string.IsNullOrWhiteSpace(srtContent))
            return;

        // Escape special characters in file path for FFmpeg
        var escapedPath = srtPath
            .Replace("\\", "/")
            .Replace(":", "\\:")
            .Replace("'", "\\'");

        var style = subtitleOptions.GetFFmpegStyleString();
        var subtitleFilter = $"subtitles='{escapedPath}':force_style='{style}'";

        options.WithCustomArgument($"-vf \"{subtitleFilter}\"");
    }

    private int GetSegmentNumber(Project project, Segment segment)
    {
        // Get all segments for the project ordered by start time
        var segments = project.Segments.OrderBy(s => s.StartTime).ToList();
        var index = segments.FindIndex(s => s.Id == segment.Id);
        return index + 1; // 1-based numbering
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Trim();
    }
}

public class ExtractionResult
{
    public bool Success { get; set; }
    public string? ClipPath { get; set; }
    public string? Error { get; set; }
}
