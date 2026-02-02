using Microsoft.EntityFrameworkCore;
using VideoSplitter.Data;
using VideoSplitter.Models;

namespace VideoSplitter.Services;

public interface ISegmentService
{
    Task<IEnumerable<Segment>> GetSegmentsByProjectIdAsync(int projectId);
    Task<Segment?> GetSegmentByIdAsync(int id);
    Task<Segment> CreateSegmentAsync(Segment segment);
    Task<Segment> UpdateSegmentAsync(Segment segment);
    Task DeleteSegmentAsync(int id);
    Task<IEnumerable<Segment>> CreateSegmentsAsync(IEnumerable<Segment> segments);
    Task<int> DeleteAllSegmentsAsync();
}

public class SegmentService : ISegmentService
{
    private readonly VideoSplitterDbContext _context;

    public SegmentService(VideoSplitterDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Segment>> GetSegmentsByProjectIdAsync(int projectId)
    {
        return await _context.Segments
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<Segment?> GetSegmentByIdAsync(int id)
    {
        return await _context.Segments
            .Include(s => s.Project)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Segment> CreateSegmentAsync(Segment segment)
    {
        segment.CreatedAt = DateTime.UtcNow;
        segment.UpdatedAt = DateTime.UtcNow;

        _context.Segments.Add(segment);
        await _context.SaveChangesAsync();
        return segment;
    }

    public async Task<Segment> UpdateSegmentAsync(Segment segment)
    {
        segment.UpdatedAt = DateTime.UtcNow;
        _context.Segments.Update(segment);
        await _context.SaveChangesAsync();
        return segment;
    }

    public async Task DeleteSegmentAsync(int id)
    {
        var segment = await _context.Segments.FindAsync(id);
        if (segment != null)
        {
            // Clean up clip file if it exists
            if (!string.IsNullOrEmpty(segment.ClipPath) && File.Exists(segment.ClipPath))
            {
                File.Delete(segment.ClipPath);
            }

            _context.Segments.Remove(segment);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Segment>> CreateSegmentsAsync(IEnumerable<Segment> segments)
    {
        var segmentList = segments.ToList();
        var now = DateTime.UtcNow;

        foreach (var segment in segmentList)
        {
            segment.CreatedAt = now;
            segment.UpdatedAt = now;
        }

        _context.Segments.AddRange(segmentList);
        await _context.SaveChangesAsync();
        return segmentList;
    }

    public async Task<int> DeleteAllSegmentsAsync()
    {
        var segments = await _context.Segments.ToListAsync();
        var count = segments.Count;

        // Clean up clip files
        foreach (var segment in segments)
        {
            if (!string.IsNullOrEmpty(segment.ClipPath) && File.Exists(segment.ClipPath))
            {
                try
                {
                    File.Delete(segment.ClipPath);
                }
                catch
                {
                    // Ignore file deletion errors
                }
            }
        }

        _context.Segments.RemoveRange(segments);
        await _context.SaveChangesAsync();
        return count;
    }
}