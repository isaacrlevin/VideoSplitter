using System.Text.Json;

namespace VideoSplitter.Services.SocialMediaPublishers;

/// <summary>
/// Service to manage social media credentials securely
/// </summary>
public interface ISocialMediaCredentialService
{
    Task SaveCredentialsAsync(string platform, SocialMediaCredentials credentials);
    Task<SocialMediaCredentials?> GetCredentialsAsync(string platform);
    Task DeleteCredentialsAsync(string platform);
    Task<bool> HasValidCredentialsAsync(string platform);
}

/// <summary>
/// Stores OAuth credentials for a social media platform
/// </summary>
public class SocialMediaCredentials
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SocialMediaCredentialService : ISocialMediaCredentialService
{
    private readonly string _credentialsFolder;
    private readonly object _lockObject = new();

    public SocialMediaCredentialService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _credentialsFolder = Path.Combine(appDataPath, "VideoSplitter", "credentials");
        Directory.CreateDirectory(_credentialsFolder);
    }

    public async Task SaveCredentialsAsync(string platform, SocialMediaCredentials credentials)
    {
        var filePath = GetCredentialsFilePath(platform);
        credentials.UpdatedAt = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Simple file-based storage - in production, consider using SecureStorage or Windows Credential Manager
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<SocialMediaCredentials?> GetCredentialsAsync(string platform)
    {
        var filePath = GetCredentialsFilePath(platform);

        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<SocialMediaCredentials>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading credentials for {platform}: {ex.Message}");
            return null;
        }
    }

    public Task DeleteCredentialsAsync(string platform)
    {
        var filePath = GetCredentialsFilePath(platform);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> HasValidCredentialsAsync(string platform)
    {
        var credentials = await GetCredentialsAsync(platform);

        if (credentials == null || string.IsNullOrEmpty(credentials.AccessToken))
            return false;

        // Check if token is expired
        if (credentials.ExpiresAt.HasValue && credentials.ExpiresAt.Value < DateTime.UtcNow)
            return false;

        return true;
    }

    private string GetCredentialsFilePath(string platform)
    {
        // Sanitize platform name for file system
        var safeName = string.Join("_", platform.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_credentialsFolder, $"{safeName.ToLowerInvariant()}.json");
    }
}
