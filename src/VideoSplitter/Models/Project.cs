using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoSplitter.Models;

public class Project
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public string VideoPath { get; set; } = string.Empty;
    
    public string? YouTubeUrl { get; set; }
    
    public string? ThumbnailPath { get; set; }
    
    public TimeSpan? Duration { get; set; }
    
    public long? FileSizeBytes { get; set; }
    
    public ProjectStatus Status { get; set; } = ProjectStatus.VideoUploaded;
    
    public string? TranscriptPath { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<Segment> Segments { get; set; } = new List<Segment>();
}

public enum ProjectStatus
{
    VideoUploaded,
    TranscriptGenerating,
    TranscriptGenerated,
    SegmentsGenerating,
    SegmentsGenerated,
    Completed
}