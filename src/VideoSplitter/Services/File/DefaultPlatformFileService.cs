namespace VideoSplitter.Services;

public interface IPlatformFileService
{
    /// <summary>
    /// Opens the folder containing the specified file path in the system's file explorer
    /// </summary>
    /// <param name="filePath">The path to the file whose folder should be opened</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> OpenFolderAndSelectFileAsync(string filePath);

    /// <summary>
    /// Opens the specified folder in the system's file explorer
    /// </summary>
    /// <param name="folderPath">The path to the folder to open</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> OpenFolderAsync(string folderPath);
}


/// <summary>
/// Default implementation of platform file service (used when platform-specific implementation is not available)
/// </summary>
public class DefaultPlatformFileService : IPlatformFileService
{
    public Task<bool> OpenFolderAndSelectFileAsync(string filePath)
    {
        // Not implemented for this platform
        Console.WriteLine($"OpenFolderAndSelectFile not implemented for this platform: {filePath}");
        return Task.FromResult(false);
    }

    public Task<bool> OpenFolderAsync(string folderPath)
    {
        // Not implemented for this platform
        Console.WriteLine($"OpenFolder not implemented for this platform: {folderPath}");
        return Task.FromResult(false);
    }
}
