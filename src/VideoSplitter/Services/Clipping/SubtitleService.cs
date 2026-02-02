using System.Text;
using System.Text.RegularExpressions;

namespace VideoSplitter.Services;

/// <summary>
/// Service for handling SRT subtitle files - generation, parsing, and clipping.
/// </summary>
public interface ISubtitleService
{
    /// <summary>
    /// Generates an SRT file from Whisper transcript data.
    /// </summary>
    Task<(bool Success, string? SrtPath, string? Error)> GenerateSrtFromWhisperAsync(
        string whisperTranscriptPath,
        string outputSrtPath);

    /// <summary>
    /// Clips an SRT file to match a specific time segment.
    /// </summary>
    Task<(bool Success, string? ClippedSrtPath, string? Error)> ClipSrtAsync(
        string srtPath,
        TimeSpan startTime,
        TimeSpan endTime,
        string outputPath);

    /// <summary>
    /// Parses an SRT file into subtitle entries.
    /// </summary>
    Task<List<SubtitleEntry>> ParseSrtAsync(string srtPath);

    /// <summary>
    /// Writes subtitle entries to an SRT file.
    /// </summary>
    Task WriteSrtAsync(string srtPath, List<SubtitleEntry> entries);
}

/// <summary>
/// Represents a single subtitle entry in an SRT file.
/// </summary>
public class SubtitleEntry
{
    public int Index { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Text { get; set; } = string.Empty;
}

public partial class SubtitleService : ISubtitleService
{
    // Regex to parse Whisper.NET transcript format: [HH:MM:SS -> HH:MM:SS] Text
    [GeneratedRegex(@"\[(\d{2}:\d{2}:\d{2})\s*->\s*(\d{2}:\d{2}:\d{2})\]\s*(.+)", RegexOptions.Compiled)]
    private static partial Regex WhisperLineRegex();

    // Regex to parse SRT timestamp format: HH:MM:SS,mmm --> HH:MM:SS,mmm
    [GeneratedRegex(@"(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})", RegexOptions.Compiled)]
    private static partial Regex SrtTimestampRegex();

    public async Task<(bool Success, string? SrtPath, string? Error)> GenerateSrtFromWhisperAsync(
        string whisperTranscriptPath,
        string outputSrtPath)
    {
        try
        {
            if (!File.Exists(whisperTranscriptPath))
            {
                return (false, null, "Whisper transcript file not found.");
            }

            var lines = await File.ReadAllLinesAsync(whisperTranscriptPath);
            var entries = new List<SubtitleEntry>();
            var index = 1;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = WhisperLineRegex().Match(line);
                if (match.Success)
                {
                    var startTime = ParseTimeSpan(match.Groups[1].Value);
                    var endTime = ParseTimeSpan(match.Groups[2].Value);
                    var text = match.Groups[3].Value.Trim();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        entries.Add(new SubtitleEntry
                        {
                            Index = index++,
                            StartTime = startTime,
                            EndTime = endTime,
                            Text = text
                        });
                    }
                }
            }

            if (entries.Count == 0)
            {
                return (false, null, "No valid subtitle entries found in transcript.");
            }

            await WriteSrtAsync(outputSrtPath, entries);

            return (true, outputSrtPath, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Failed to generate SRT: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? ClippedSrtPath, string? Error)> ClipSrtAsync(
        string srtPath,
        TimeSpan startTime,
        TimeSpan endTime,
        string outputPath)
    {
        try
        {
            if (!File.Exists(srtPath))
            {
                return (false, null, "SRT file not found.");
            }

            var entries = await ParseSrtAsync(srtPath);
            var clippedEntries = new List<SubtitleEntry>();
            var index = 1;

            foreach (var entry in entries)
            {
                // Check if this entry overlaps with the clip range
                if (entry.EndTime <= startTime || entry.StartTime >= endTime)
                    continue;

                // Adjust times relative to the clip start
                var adjustedStart = entry.StartTime - startTime;
                var adjustedEnd = entry.EndTime - startTime;

                // Clamp to valid range
                if (adjustedStart < TimeSpan.Zero)
                    adjustedStart = TimeSpan.Zero;
                if (adjustedEnd > (endTime - startTime))
                    adjustedEnd = endTime - startTime;

                clippedEntries.Add(new SubtitleEntry
                {
                    Index = index++,
                    StartTime = adjustedStart,
                    EndTime = adjustedEnd,
                    Text = entry.Text
                });
            }

            if (clippedEntries.Count == 0)
            {
                // Create an empty SRT file (no subtitles for this segment)
                await File.WriteAllTextAsync(outputPath, string.Empty);
                return (true, outputPath, null);
            }

            await WriteSrtAsync(outputPath, clippedEntries);

            return (true, outputPath, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Failed to clip SRT: {ex.Message}");
        }
    }

    public async Task<List<SubtitleEntry>> ParseSrtAsync(string srtPath)
    {
        var entries = new List<SubtitleEntry>();

        if (!File.Exists(srtPath))
            return entries;

        var content = await File.ReadAllTextAsync(srtPath);
        var blocks = content.Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split(["\r\n", "\n"], StringSplitOptions.None);
            if (lines.Length < 3)
                continue;

            if (!int.TryParse(lines[0].Trim(), out var index))
                continue;

            var timestampMatch = SrtTimestampRegex().Match(lines[1]);
            if (!timestampMatch.Success)
                continue;

            var startTime = ParseSrtTimeSpan(timestampMatch.Groups[1].Value);
            var endTime = ParseSrtTimeSpan(timestampMatch.Groups[2].Value);
            var text = string.Join("\n", lines.Skip(2)).Trim();

            entries.Add(new SubtitleEntry
            {
                Index = index,
                StartTime = startTime,
                EndTime = endTime,
                Text = text
            });
        }

        return entries;
    }

    public async Task WriteSrtAsync(string srtPath, List<SubtitleEntry> entries)
    {
        var sb = new StringBuilder();

        foreach (var entry in entries.OrderBy(e => e.StartTime))
        {
            sb.AppendLine(entry.Index.ToString());
            sb.AppendLine($"{FormatSrtTimeSpan(entry.StartTime)} --> {FormatSrtTimeSpan(entry.EndTime)}");
            sb.AppendLine(entry.Text);
            sb.AppendLine();
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(srtPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(srtPath, sb.ToString(), Encoding.UTF8);
    }

    private static TimeSpan ParseTimeSpan(string timeString)
    {
        // Parse HH:MM:SS format
        var parts = timeString.Split(':');
        if (parts.Length == 3 &&
            int.TryParse(parts[0], out var hours) &&
            int.TryParse(parts[1], out var minutes) &&
            int.TryParse(parts[2], out var seconds))
        {
            return new TimeSpan(hours, minutes, seconds);
        }

        return TimeSpan.Zero;
    }

    private static TimeSpan ParseSrtTimeSpan(string timeString)
    {
        // Parse HH:MM:SS,mmm format
        var parts = timeString.Replace(',', '.').Split(':');
        if (parts.Length == 3 &&
            int.TryParse(parts[0], out var hours) &&
            int.TryParse(parts[1], out var minutes) &&
            double.TryParse(parts[2], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            return new TimeSpan(0, hours, minutes, (int)seconds, (int)((seconds % 1) * 1000));
        }

        return TimeSpan.Zero;
    }

    private static string FormatSrtTimeSpan(TimeSpan time)
    {
        // Format as HH:MM:SS,mmm
        return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
    }
}
