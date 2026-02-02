namespace VideoSplitter.Models;

/// <summary>
/// Configuration options for burned-in subtitles.
/// </summary>
public class SubtitleOptions
{
    /// <summary>
    /// Whether to burn subtitles into the extracted video.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Font name to use for subtitles.
    /// </summary>
    public string FontName { get; set; } = "Arial";

    /// <summary>
    /// Font size in points.
    /// </summary>
    public int FontSize { get; set; } = 24;

    /// <summary>
    /// Primary text color in hex format (e.g., "FFFFFF" for white).
    /// </summary>
    public string PrimaryColor { get; set; } = "FFFFFF";

    /// <summary>
    /// Outline/border color in hex format.
    /// </summary>
    public string OutlineColor { get; set; } = "000000";

    /// <summary>
    /// Background/shadow color in hex format.
    /// </summary>
    public string BackgroundColor { get; set; } = "000000";

    /// <summary>
    /// Outline thickness (0-4).
    /// </summary>
    public int OutlineWidth { get; set; } = 2;

    /// <summary>
    /// Shadow depth (0-4).
    /// </summary>
    public int ShadowDepth { get; set; } = 1;

    /// <summary>
    /// Vertical position of subtitles.
    /// </summary>
    public SubtitlePosition Position { get; set; } = SubtitlePosition.Bottom;

    /// <summary>
    /// Vertical margin from the edge (in pixels for 1080p, scaled proportionally).
    /// </summary>
    public int MarginVertical { get; set; } = 50;

    /// <summary>
    /// Whether to display subtitles in all caps.
    /// </summary>
    public bool AllCaps { get; set; } = false;

    /// <summary>
    /// Maximum number of characters per line before wrapping.
    /// </summary>
    public int MaxCharsPerLine { get; set; } = 40;

    /// <summary>
    /// Gets the FFmpeg subtitle style string for ASS/SSA format.
    /// </summary>
    public string GetFFmpegStyleString()
    {
        // FFmpeg uses ABGR format for colors (opposite of RGB)
        var primaryAbgr = ConvertRgbToAbgr(PrimaryColor);
        var outlineAbgr = ConvertRgbToAbgr(OutlineColor);
        var backgroundAbgr = ConvertRgbToAbgr(BackgroundColor);

        // ASS v4+ uses numpad-style alignment:
        // 5=top-left,    6=top-center,    7=top-right
        // 9=middle-left, 10=middle-center, 11=middle-right
        // 1=bottom-left, 2=bottom-center, 3=bottom-right
        var alignment = Position switch
        {
            SubtitlePosition.Top => 6,
            SubtitlePosition.Middle => 10,
            SubtitlePosition.Bottom => 2,
            _ => 2
        };

        // Build the style string for FFmpeg subtitles filter
        return $"FontName={FontName},FontSize={FontSize}," +
               $"PrimaryColour=&H{primaryAbgr}," +
               $"OutlineColour=&H{outlineAbgr}," +
               $"BackColour=&H{backgroundAbgr}," +
               $"Outline={OutlineWidth}," +
               $"Shadow={ShadowDepth}," +
               $"Alignment={alignment}," +
               $"MarginV={MarginVertical}";
    }

    /// <summary>
    /// Converts RGB hex to ABGR hex format (with alpha = 00 for fully opaque).
    /// </summary>
    private static string ConvertRgbToAbgr(string rgb)
    {
        if (rgb.Length != 6)
            return "00FFFFFF"; // Default to white

        var r = rgb[..2];
        var g = rgb[2..4];
        var b = rgb[4..6];

        return $"00{b}{g}{r}"; // Alpha + BGR
    }
}

/// <summary>
/// Vertical position for subtitles.
/// </summary>
public enum SubtitlePosition
{
    /// <summary>Display subtitles at the top of the video.</summary>
    Top,

    /// <summary>Display subtitles in the middle of the video.</summary>
    Middle,

    /// <summary>Display subtitles at the bottom of the video.</summary>
    Bottom
}
