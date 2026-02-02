using Microsoft.EntityFrameworkCore;
using VideoSplitter.Data;
using VideoSplitter.Models;

namespace VideoSplitter.Services;

public interface IProjectService
{
    Task<IEnumerable<Project>> GetAllProjectsAsync();
    Task<Project?> GetProjectByIdAsync(int id);
    Task<Project> CreateProjectAsync(Project project);
    Task<Project> UpdateProjectAsync(Project project);
    Task DeleteProjectAsync(int id);
    Task<string> CreateProjectFolderAsync(int projectId);
}

public class ProjectService : IProjectService
{
    private readonly VideoSplitterDbContext _context;

    public ProjectService(VideoSplitterDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Project>> GetAllProjectsAsync()
    {
        return await _context.Projects
            .Include(p => p.Segments)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Project?> GetProjectByIdAsync(int id)
    {
        return await _context.Projects
            .Include(p => p.Segments)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project> CreateProjectAsync(Project project)
    {
        project.CreatedAt = DateTime.UtcNow;
        project.UpdatedAt = DateTime.UtcNow;

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        
        // Create project folder
        await CreateProjectFolderAsync(project.Id);
        
        return project;
    }

    public async Task<Project> UpdateProjectAsync(Project project)
    {
        project.UpdatedAt = DateTime.UtcNow;
        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
        return project;
    }

    public async Task DeleteProjectAsync(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project != null)
        {
            // Clean up files
            var projectFolder = GetProjectFolderPath(id);
            if (Directory.Exists(projectFolder))
            {
                Directory.Delete(projectFolder, true);
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<string> CreateProjectFolderAsync(int projectId)
    {
        var projectFolder = GetProjectFolderPath(projectId);
        
        if (!Directory.Exists(projectFolder))
        {
            Directory.CreateDirectory(projectFolder);
        }

        // Create subfolders
        var transcriptFolder = Path.Combine(projectFolder, "transcripts");
        var clipsFolder = Path.Combine(projectFolder, "clips");
        var tempFolder = Path.Combine(projectFolder, "temp");

        Directory.CreateDirectory(transcriptFolder);
        Directory.CreateDirectory(clipsFolder);
        Directory.CreateDirectory(tempFolder);

        return projectFolder;
    }

    private static string GetProjectFolderPath(int projectId)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "VideoSplitter");
        return Path.Combine(appFolder, "Projects", $"Project_{projectId}");
    }
}