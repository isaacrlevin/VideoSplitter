namespace VideoSplitter.Models;

public class AppSettings
{
    // Transcript Settings
    public TranscriptProvider TranscriptProvider { get; set; } = TranscriptProvider.Local;

    public AzureSpeechSettings AzureSpeech { get; set; } = new();

    // LLM Settings
    public LlmProvider LlmProvider { get; set; } = LlmProvider.Local;
    public OllamaSettings Ollama { get; set; } = new();
    public OpenAiSettings OpenAi { get; set; } = new();
    public AnthropicSettings Anthropic { get; set; } = new();
    public AzureOpenAiSettings AzureOpenAi { get; set; } = new();
    public GoogleGeminiSettings GoogleGemini { get; set; } = new();

    // Prompt Settings
    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }

    // Segment Settings
    public int DefaultSegmentLengthSeconds { get; set; } = 60;
    public int DefaultSegmentCount { get; set; } = 5;

    // Paths
    public string? DefaultOutputPath { get; set; }
    public string? FFmpegPath { get; set; }

    // Social Media Publishing Settings
    public SocialMediaSettings SocialMedia { get; set; } = new();
}

/// <summary>
/// Settings for social media platform integrations
/// </summary>
public class SocialMediaSettings
{
    public TikTokSettings TikTok { get; set; } = new();
    public YouTubeSettings YouTube { get; set; } = new();
    public InstagramSettings Instagram { get; set; } = new();
}

/// <summary>
/// TikTok Developer Portal credentials
/// Get these from https://developers.tiktok.com/
/// </summary>
public class TikTokSettings
{
    public string? ClientKey { get; set; }
    public string? ClientSecret { get; set; }
}

/// <summary>
/// Google Cloud Console credentials for YouTube Data API
/// Get these from https://console.cloud.google.com/
/// </summary>
public class YouTubeSettings
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

/// <summary>
/// Meta Developer Portal credentials for Instagram Graph API
/// Get these from https://developers.facebook.com/
/// </summary>
public class InstagramSettings
{
    public string? AppId { get; set; }
    public string? AppSecret { get; set; }
}

public class AzureSpeechSettings
{
    public string? AzureSpeechApiKey { get; set; }
    public string? AzureSpeechRegion { get; set; }
}

public abstract class LlmSettingsBase
{
    public string? ApiKey { get; set; }
}

public class OllamaSettings
{
    public string? Model { get; set; }
}

public class OpenAiSettings : LlmSettingsBase
{
    public string? Model { get; set; } = "gpt-4o-mini";
}

public class AnthropicSettings : LlmSettingsBase
{
    public string? Model { get; set; } = "claude-sonnet-4-20250514";
}

public class AzureOpenAiSettings : LlmSettingsBase
{
    public string? Endpoint { get; set; }
    public string? DeploymentName { get; set; }
}

public class GoogleGeminiSettings : LlmSettingsBase
{
    public string? Model { get; set; } = "gemini-2.5-pro";
}

public enum TranscriptProvider
{
    Local,      // Using Whisper.NET or similar
    Azure       // Azure AI Speech
}

public enum LlmProvider
{
    Local,      // Ollama
    OpenAI,
    Anthropic,
    AzureOpenAI,
    GoogleGemini
}