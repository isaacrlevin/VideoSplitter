using System.Data;
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
            entity.Property(e => e.PreviewVideoPath).HasMaxLength(500);
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
            entity.Property(e => e.PostTitle).HasMaxLength(150);
            entity.Property(e => e.PostDescription).HasMaxLength(2200);
            entity.Property(e => e.PostHashtags).HasMaxLength(500);

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

    // Columns added to existing tables after release, keyed by table name. EnsureCreated() only
    // builds the schema when the database file is missing, so databases created by an earlier
    // version never pick these up on their own.
    private static readonly (string Table, string Column, string Definition)[] AddedColumns =
    [
        ("Segments", "PostTitle", "TEXT NULL"),
        ("Segments", "PostDescription", "TEXT NULL"),
        ("Segments", "PostHashtags", "TEXT NULL")
    ];

    /// <summary>
    /// Adds any columns missing from an existing database. Safe to call on every startup and on a
    /// freshly created database - each column is only added when it isn't already there.
    /// </summary>
    public void EnsureAddedColumnsExist()
    {
        var connection = Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            connection.Open();
        }

        try
        {
            foreach (var group in AddedColumns.GroupBy(c => c.Table))
            {
                var existing = GetColumnNames(connection, group.Key);

                // Table isn't there at all - EnsureCreated() will build it with every column.
                if (existing.Count == 0)
                {
                    continue;
                }

                foreach (var (table, column, definition) in group)
                {
                    if (existing.Contains(column))
                    {
                        continue;
                    }

                    using var alter = connection.CreateCommand();
                    alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
                    alter.ExecuteNonQuery();
                }
            }
        }
        finally
        {
            if (openedHere)
            {
                connection.Close();
            }
        }
    }

    private static HashSet<string> GetColumnNames(System.Data.Common.DbConnection connection, string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info($table)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$table";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }
}