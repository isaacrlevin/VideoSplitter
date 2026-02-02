namespace VideoSplitter.Services.SocialMediaPublishers;

/// <summary>
/// Common interface for all social media publishing services
/// </summary>
public interface ISocialMediaPublisher
{
    /// <summary>
    /// Gets the platform name (e.g., "TikTok", "YouTube Shorts", "Instagram Reels")
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Gets the platform icon class for UI display
    /// </summary>
    string PlatformIcon { get; }

    /// <summary>
    /// Checks if the user is authenticated with the platform
    /// </summary>
    Task<bool> IsAuthenticatedAsync();

    /// <summary>
    /// Validates that the stored tokens are still valid by checking with the platform's API.
    /// If tokens are invalid or expired, the publisher will be disconnected.
    /// </summary>
    /// <returns>True if tokens are valid, false if tokens are invalid or expired (and have been disconnected).</returns>
    Task<bool> ValidateAndRefreshTokensAsync();

    /// <summary>
    /// Performs the complete OAuth authentication flow.
    /// Opens the browser for user authorization and handles the callback automatically.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for authentication.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The authentication result containing tokens or error information.</returns>
    Task<AuthResult> AuthenticateAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the authorization URL for OAuth flow.
    /// Use <see cref="AuthenticateAsync"/> for a complete authentication flow instead.
    /// </summary>
    Task<string> GetAuthorizationUrlAsync(string redirectUri);

    /// <summary>
    /// Handles the OAuth callback and exchanges code for tokens.
    /// Use <see cref="AuthenticateAsync"/> for a complete authentication flow instead.
    /// </summary>
    Task<AuthResult> HandleAuthCallbackAsync(string code, string redirectUri);

    /// <summary>
    /// Validates a video file for platform compatibility
    /// </summary>
    Task<VideoValidationResult> ValidateVideoAsync(string videoPath);

    /// <summary>
    /// Publishes a video to the platform
    /// </summary>
    Task<PublishResult> PublishVideoAsync(PublishRequest request, IProgress<double>? progress = null);

    /// <summary>
    /// Disconnects/logs out from the platform
    /// </summary>
    Task DisconnectAsync();
}

/// <summary>
/// Result of authentication attempt
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result of video validation for a platform
/// </summary>
public class VideoValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public VideoMetadata? Metadata { get; set; }
}

/// <summary>
/// Video metadata extracted during validation
/// </summary>
public class VideoMetadata
{
    public TimeSpan Duration { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSizeBytes { get; set; }
    public string? Format { get; set; }
    public double? AspectRatio => Height > 0 ? (double)Width / Height : null;
}

/// <summary>
/// Request to publish a video
/// </summary>
public class PublishRequest
{
    public required string VideoPath { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? ThumbnailPath { get; set; }
    public PrivacyLevel Privacy { get; set; } = PrivacyLevel.Public;
    public bool AllowComments { get; set; } = true;
    public bool AllowDuet { get; set; } = true;  // TikTok specific
    public bool AllowStitch { get; set; } = true; // TikTok specific
}

/// <summary>
/// Privacy level for published content
/// </summary>
public enum PrivacyLevel
{
    Public,
    Private,
    FriendsOnly,  // TikTok specific
    Unlisted      // YouTube specific
}

/// <summary>
/// Result of publishing a video
/// </summary>
public class PublishResult
{
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? PostUrl { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    
    /// <summary>
    /// Indicates that the error is related to authentication/token issues.
    /// When true, the user should be prompted to reconnect their account.
    /// </summary>
    public bool IsTokenError { get; set; }
}
