using FFMpegCore;
using VideoSplitter.Models;

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

    // Standard vertical video resolution for TikTok/YouTube Shorts
    private const int VerticalWidth = 1080;
    private const int VerticalHeight = 1920;

    public VideoExtractionService(
        IProjectService projectService, 
        IFileStreamService fileStreamService,
        ISubtitleService subtitleService)
    {
        _projectService = projectService;
        _fileStreamService = fileStreamService;
        _subtitleService = subtitleService;
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
        string? clippedSrtPath = null;
        
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

            // Prepare subtitle file if enabled
            if (subtitleOptions?.Enabled == true && !string.IsNullOrEmpty(project.TranscriptPath))
            {
                var srtPath = Path.ChangeExtension(project.TranscriptPath, ".srt");
                
                // Check if SRT file exists, if not try to generate it from the transcript
                if (!File.Exists(srtPath))
                {
                    var generateResult = await _subtitleService.GenerateSrtFromWhisperAsync(
                        project.TranscriptPath, srtPath);
                    
                    if (!generateResult.Success)
                    {
                        return new ExtractionResult
                        {
                            Success = false,
                            Error = $"Failed to generate SRT file: {generateResult.Error}"
                        };
                    }
                }
                
                // Clip the SRT file to match the segment
                clippedSrtPath = Path.Combine(clipsFolder, $"{sanitizedProjectName}-{segmentNumber}.srt");
                var clipResult = await _subtitleService.ClipSrtAsync(
                    srtPath,
                    segment.StartTime,
                    segment.EndTime,
                    clippedSrtPath);
                
                if (!clipResult.Success)
                {
                    return new ExtractionResult
                    {
                        Success = false,
                        Error = $"Failed to clip SRT file: {clipResult.Error}"
                    };
                }
            }

            // Extract the segment using FFmpeg
            var duration = segment.EndTime - segment.StartTime;
            
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

                    // Apply aspect ratio and subtitle filters
                    ApplyVideoFilters(options, aspectRatioMode, subtitleOptions, clippedSrtPath);
                })
                .NotifyOnProgress(percent => progress?.Report(percent), duration)
                .ProcessAsynchronously();

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
