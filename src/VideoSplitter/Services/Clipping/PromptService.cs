namespace VideoSplitter.Services;

/// <summary>
/// Service for loading and managing AI prompt templates
/// </summary>
public interface IPromptService
{
    Task<string> LoadDefaultSystemPromptAsync();
    Task<string> LoadDefaultUserPromptAsync();
}

public class PromptService : IPromptService
{
    public async Task<string> LoadDefaultSystemPromptAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("Prompts/SystemPrompt.md");
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading system prompt: {ex.Message}");
            return GetFallbackSystemPrompt();
        }
    }

    public async Task<string> LoadDefaultUserPromptAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("Prompts/UserPrompt.md");
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading user prompt: {ex.Message}");
            return GetFallbackUserPrompt();
        }
    }

    private static string GetFallbackSystemPrompt()
    {
        return """
            You are an expert technical content editor for software developers.
            Analyze video transcripts and extract the most valuable standalone segments for a developer or engineering audience.
            Output format should be in JSON.
            """;
    }

    private static string GetFallbackUserPrompt()
    {
        return """
            Below is a video transcript. Identify up to {segmentCount} best segments for a technical
            or developer audience using the required format.
            Each segment must be {segmentLength} seconds or less.
            
            Transcript:
            {transcript}
            """;
    }
}
