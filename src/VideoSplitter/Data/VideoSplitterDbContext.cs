using Microsoft.EntityFrameworkCore;
using VideoSplitter.Models;

namespace VideoSplitter.Data;

public class VideoSplitterDbContext : DbContext
{
    public VideoSplitterDbContext(DbContextOptions<VideoSplitterDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects { get; set; }
    public DbSet<Segment> Segments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Project entity
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.VideoPath).HasMaxLength(500);
            entity.Property(e => e.YouTubeUrl).HasMaxLength(500);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(500);
            entity.Property(e => e.TranscriptPath).HasMaxLength(500);
            
            // Configure TimeSpan conversion for Duration property
            entity.Property(e => e.Duration)
                  .HasConversion(
                      v => v.HasValue ? v.Value.Ticks : (long?)null,
                      v => v.HasValue ? new TimeSpan(v.Value) : (TimeSpan?)null);
            
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Status);
        });

        // Configure Segment entity
        modelBuilder.Entity<Segment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TranscriptText).HasMaxLength(2000);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.Reasoning).HasMaxLength(1000);
            entity.Property(e => e.ClipPath).HasMaxLength(500);
            
            // Configure TimeSpan to long conversion for SQLite compatibility
            entity.Property(e => e.StartTime)
                  .HasConversion(
                      v => v.Ticks,
                      v => new TimeSpan(v));
                      
            entity.Property(e => e.EndTime)
                  .HasConversion(
                      v => v.Ticks,
                      v => new TimeSpan(v));
            
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartTime);
            
            // Configure relationship
            entity.HasOne(d => d.Project)
                  .WithMany(p => p.Segments)
                  .HasForeignKey(d => d.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}