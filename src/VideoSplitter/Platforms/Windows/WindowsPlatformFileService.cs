using System.Diagnostics;
using System.Runtime.Versioning;

namespace VideoSplitter.Services;

/// <summary>
/// Windows-specific implementation of file operations
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsPlatformFileService : IPlatformFileService
{
    public Task<bool> OpenFolderAndSelectFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return Task.FromResult(false);
            }

            // Use Windows Explorer to open the folder and select the file
            // /select, parameter opens the folder and highlights the file
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            };

            Process.Start(processStartInfo);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening folder and selecting file: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> OpenFolderAsync(string folderPath)
    {
        try
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                return Task.FromResult(false);
            }

            // Open the folder in Windows Explorer
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            };

            Process.Start(processStartInfo);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening folder: {ex.Message}");
            return Task.FromResult(false);
        }
    }
}
