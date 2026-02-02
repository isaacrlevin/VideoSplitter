using VideoSplitter.Models;

namespace VideoSplitter.Services.SocialMediaPublishers;

/// <summary>
/// Manages all social media publisher instances and provides a unified interface
/// </summary>
public interface ISocialMediaPublisherService
{
    /// <summary>
    /// Gets all available publishers
    /// </summary>
    IEnumerable<ISocialMediaPublisher> GetAllPublishers();

    /// <summary>
    /// Gets a specific publisher by platform name
    /// </summary>
    ISocialMediaPublisher? GetPublisher(string platformName);

    /// <summary>
    /// Gets the authentication status of all platforms
    /// </summary>
    Task<Dictionary<string, bool>> GetAuthenticationStatusAsync();

    /// <summary>
    /// Validates tokens for all connected platforms and disconnects any with invalid tokens.
    /// Should be called on app startup.
    /// </summary>
    /// <returns>A dictionary with platform names and their validation results (true = valid, false = disconnected).</returns>
    Task<Dictionary<string, bool>> ValidateAllTokensAsync();

    /// <summary>
    /// Checks if a video is compatible with a specific platform
    /// </summary>
    Task<VideoValidationResult> ValidateVideoForPlatformAsync(string videoPath, string platformName);

    /// <summary>
    /// Validates a video against all platforms
    /// </summary>
    Task<Dictionary<string, VideoValidationResult>> ValidateVideoForAllPlatformsAsync(string videoPath);
}

public class SocialMediaPublisherService : ISocialMediaPublisherService
{
    private readonly List<ISocialMediaPublisher> _publishers = [];
    private readonly ISocialMediaCredentialService _credentialService;
    private readonly AppSettings _appSettings;
    private readonly HttpClient _httpClient;
    private bool _publishersInitialized = false;

    public SocialMediaPublisherService(
        IHttpClientFactory httpClientFactory,
        ISocialMediaCredentialService credentialService,
        AppSettings appSettings)
    {
        _httpClient = httpClientFactory.CreateClient();
        _credentialService = credentialService;
        _appSettings = appSettings;
    }

    public IEnumerable<ISocialMediaPublisher> GetAllPublishers()
    {
        EnsurePublishersInitialized();
        return _publishers.AsReadOnly();
    }

    public ISocialMediaPublisher? GetPublisher(string platformName)
    {
        EnsurePublishersInitialized();
        return _publishers.FirstOrDefault(p =>
            p.PlatformName.Equals(platformName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Dictionary<string, bool>> GetAuthenticationStatusAsync()
    {
        EnsurePublishersInitialized();
        var status = new Dictionary<string, bool>();

        foreach (var publisher in _publishers)
        {
            status[publisher.PlatformName] = await publisher.IsAuthenticatedAsync();
        }

        return status;
    }

    public async Task<Dictionary<string, bool>> ValidateAllTokensAsync()
    {
        EnsurePublishersInitialized();
        var results = new Dictionary<string, bool>();

        foreach (var publisher in _publishers)
        {
            // Only validate if the publisher appears to be authenticated
            var isAuthenticated = await publisher.IsAuthenticatedAsync();
            if (isAuthenticated)
            {
                // Actually validate the tokens with the platform API
                var isValid = await publisher.ValidateAndRefreshTokensAsync();
                results[publisher.PlatformName] = isValid;
            }
            else
            {
                // Not authenticated, no need to validate
                results[publisher.PlatformName] = false;
            }
        }

        return results;
    }

    public async Task<VideoValidationResult> ValidateVideoForPlatformAsync(string videoPath, string platformName)
    {
        var publisher = GetPublisher(platformName);

        if (publisher == null)
        {
            return new VideoValidationResult
            {
                IsValid = false,
                Errors = [$"Publisher for platform '{platformName}' not found or not configured."]
            };
        }

        return await publisher.ValidateVideoAsync(videoPath);
    }

    public async Task<Dictionary<string, VideoValidationResult>> ValidateVideoForAllPlatformsAsync(string videoPath)
    {
        EnsurePublishersInitialized();
        var results = new Dictionary<string, VideoValidationResult>();

        foreach (var publisher in _publishers)
        {
            results[publisher.PlatformName] = await publisher.ValidateVideoAsync(videoPath);
        }

        return results;
    }

    private void EnsurePublishersInitialized()
    {
        if (_publishersInitialized)
            return;

        InitializePublishers();
        _publishersInitialized = true;
    }

    private void InitializePublishers()
    {
        // TikTok Publisher
        if (!string.IsNullOrEmpty(_appSettings.SocialMedia?.TikTok?.ClientKey))
        {
            AddOrUpdatePublisher<TikTokPublisher>(
                "TikTok",
                () => new TikTokPublisher(
                    _httpClient,
                    _credentialService,
                    _appSettings,
                    _appSettings.SocialMedia.TikTok.ClientKey,
                    _appSettings.SocialMedia.TikTok.ClientSecret ?? ""));
        }

        // YouTube Shorts Publisher
        if (!string.IsNullOrEmpty(_appSettings.SocialMedia?.YouTube?.ClientId))
        {
            AddOrUpdatePublisher<YouTubeShortsPublisher>(
                "YouTube Shorts",
                () => new YouTubeShortsPublisher(
                    _httpClient,
                    _credentialService,
                    _appSettings,
                    _appSettings.SocialMedia.YouTube.ClientId,
                    _appSettings.SocialMedia.YouTube.ClientSecret ?? ""));
        }

        // Instagram Reels Publisher
        if (!string.IsNullOrEmpty(_appSettings.SocialMedia?.Instagram?.AppId))
        {
            AddOrUpdatePublisher<InstagramReelsPublisher>(
                "Instagram Reels",
                () => new InstagramReelsPublisher(
                    _httpClient,
                    _credentialService,
                    _appSettings,
                    _appSettings.SocialMedia.Instagram.AppId,
                    _appSettings.SocialMedia.Instagram.AppSecret ?? ""));
        }
    }

    private void AddOrUpdatePublisher<T>(string platformName, Func<T> factory) where T : ISocialMediaPublisher
    {
        var existingIndex = _publishers.FindIndex(p => 
            p.PlatformName.Equals(platformName, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            // Publisher already exists - replace with new instance if credentials may have changed
            _publishers[existingIndex] = factory();
        }
        else
        {
            // Publisher doesn't exist - add new one
            _publishers.Add(factory());
        }
    }
}
