using System.Runtime.InteropServices;
using System.Web;
using FFMpegCore;

namespace VideoSplitter.Services;

public interface IFileStreamService
{
    string GetVideoUrl(string videoPath);
    string GetThumbnailUrl(string thumbnailPath);
    Task<byte[]> ReadFileChunkAsync(string filePath, long start, long length);
    long GetFileSize(string filePath);

    /// <summary>
    /// Extracts a frame from a video at the specified time and returns it as a base64 data URL.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <param name="timestamp">Time position to extract the frame from.</param>
    /// <param name="aspectRatioMode">Aspect ratio transformation to apply.</param>
    /// <param name="width">Target width (0 for auto).</param>
    /// <param name="height">Target height (0 for auto).</param>
    Task<string> ExtractFrameAsBase64Async(string videoPath, TimeSpan timestamp, AspectRatioMode aspectRatioMode = AspectRatioMode.Original, int width = 0, int height = 0);
}
public class FileStreamService : IFileStreamService
{
    // Standard vertical video resolution for TikTok/YouTube Shorts (scaled down for preview)
    private const int PreviewVerticalWidth = 270;
    private const int PreviewVerticalHeight = 480;

    public string GetVideoUrl(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            return string.Empty;

        // Encode the file path for JavaScript to handle
        var encodedPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(videoPath));
        return $"app://video/{encodedPath}";
    }

    public string GetThumbnailUrl(string thumbnailPath)
    {
        if (string.IsNullOrEmpty(thumbnailPath) || !File.Exists(thumbnailPath))
            return string.Empty;

        // For thumbnails, we can still use data URLs since they're much smaller
        try
        {
            var bytes = File.ReadAllBytes(thumbnailPath);
            var base64 = Convert.ToBase64String(bytes);
            var extension = Path.GetExtension(thumbnailPath).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
            return $"data:{mimeType};base64,{base64}";
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<byte[]> ReadFileChunkAsync(string filePath, long start, long length)
    {
        if (!File.Exists(filePath))
            return Array.Empty<byte>();

        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fileStream.Seek(start, SeekOrigin.Begin);
            
            var buffer = new byte[length];
            var bytesRead = await fileStream.ReadAsync(buffer, 0, (int)length);
            
            if (bytesRead < length)
            {
                Array.Resize(ref buffer, bytesRead);
            }
            
            return buffer;
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    public long GetFileSize(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;

        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<string> ExtractFrameAsBase64Async(string videoPath, TimeSpan timestamp, AspectRatioMode aspectRatioMode = AspectRatioMode.Original, int width = 0, int height = 0)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            return string.Empty;

        try
        {
            // Create a temporary file for the frame
            var tempPath = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid()}.jpg");
            
            try
            {
                // Build the video filter based on aspect ratio mode
                var videoFilter = GetAspectRatioFilter(aspectRatioMode);
                
                // Use FFmpeg to extract a single frame with aspect ratio transformation
                var args = FFMpegArguments
                    .FromFileInput(videoPath, false, options => options
                        .Seek(timestamp))
                    .OutputToFile(tempPath, true, options =>
                    {
                        options
                            .WithFrameOutputCount(1)
                            .WithVideoCodec("mjpeg")
                            .ForceFormat("image2");
                        
                        if (!string.IsNullOrEmpty(videoFilter))
                        {
                            options.WithCustomArgument($"-vf \"{videoFilter}\"");
                        }
                        else if (width > 0 && height > 0)
                        {
                            options.WithCustomArgument($"-vf \"scale={width}:{height}\"");
                        }
                    });
                
                await args.ProcessAsynchronously();
                
                if (File.Exists(tempPath))
                {
                    var bytes = await File.ReadAllBytesAsync(tempPath);
                    var base64 = Convert.ToBase64String(bytes);
                    return $"data:image/jpeg;base64,{base64}";
                }
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting frame: {ex.Message}");
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Gets the FFmpeg video filter string for the specified aspect ratio mode (scaled for preview).
    /// </summary>
    private static string GetAspectRatioFilter(AspectRatioMode mode)
    {
        return mode switch
        {
            AspectRatioMode.VerticalCrop => 
                $"crop=ih*9/16:ih,scale={PreviewVerticalWidth}:{PreviewVerticalHeight}:flags=lanczos",
            
            AspectRatioMode.VerticalBlurBackground => 
                $"split[bg][fg];" +
                $"[bg]scale={PreviewVerticalWidth}:{PreviewVerticalHeight}:force_original_aspect_ratio=increase," +
                $"crop={PreviewVerticalWidth}:{PreviewVerticalHeight},boxblur=25:5[bgblur];" +
                $"[fg]scale={PreviewVerticalWidth}:{PreviewVerticalHeight}:force_original_aspect_ratio=decrease[fgscale];" +
                $"[bgblur][fgscale]overlay=(W-w)/2:(H-h)/2",
            
            AspectRatioMode.VerticalStackSplitScreen => 
                $"split[left][right];" +
                $"[left]crop=iw/2:ih:0:0,scale={PreviewVerticalWidth}:{PreviewVerticalHeight / 2}[top];" +
                $"[right]crop=iw/2:ih:iw/2:0,scale={PreviewVerticalWidth}:{PreviewVerticalHeight / 2}[bottom];" +
                $"[top][bottom]vstack",
            
            AspectRatioMode.VerticalStackPodcast => 
                $"crop=iw*0.90:ih*0.95:iw*0.05:0," +
                $"crop=iw:ih*0.80:0:ih*0.20,split[left][right];" +
                $"[left]crop=iw/2:ih:0:0,scale={PreviewVerticalWidth}:{PreviewVerticalHeight / 2}[top];" +
                $"[right]crop=iw/2:ih:iw/2:0,scale={PreviewVerticalWidth}:{PreviewVerticalHeight / 2}[bottom];" +
                $"[top][bottom]vstack",
            
            AspectRatioMode.VerticalLetterbox => 
                $"scale={PreviewVerticalWidth}:-2," +
                $"pad={PreviewVerticalWidth}:{PreviewVerticalHeight}:(ow-iw)/2:(oh-ih)/2:black",
            
            _ => string.Empty // Original - no filter
        };
    }
}