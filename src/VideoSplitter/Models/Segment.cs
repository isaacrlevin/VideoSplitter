using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoSplitter.Models;

public class Segment
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ProjectId { get; set; }
    
    [Required]
    public TimeSpan StartTime { get; set; }
    
    [Required]
    public TimeSpan EndTime { get; set; }
    
    [Required]
    public string TranscriptText { get; set; } = string.Empty;
    
    [Required]
    public string Summary { get; set; } = string.Empty;
    
    public string? Reasoning { get; set; }
    
    public SegmentStatus Status { get; set; } = SegmentStatus.Generated;
    
    public string? ClipPath { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey(nameof(ProjectId))]
    public virtual Project Project { get; set; } = null!;
}

public enum SegmentStatus
{
    Generated,      // LLM has identified this segment
    Approved,       // User has approved this segment for extraction
    Extracting,     // Currently extracting the clip
    Extracted,      // Clip has been successfully extracted
    Failed          // Extraction failed
}