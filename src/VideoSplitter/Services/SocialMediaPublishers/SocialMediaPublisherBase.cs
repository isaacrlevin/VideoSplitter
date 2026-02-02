using FFMpegCore;
using VideoSplitter.Models;

namespace VideoSplitter.Services.SocialMediaPublishers;

/// <summary>
/// Base class for social media publishers with common functionality
/// </summary>
public abstract class SocialMediaPublisherBase : ISocialMediaPublisher
{
    protected readonly HttpClient HttpClient;
    protected readonly ISocialMediaCredentialService CredentialService;
    protected readonly AppSettings AppSettings;

    public abstract string PlatformName { get; }
    public abstract string PlatformIcon { get; }

    // Platform-specific constraints
    protected abstract int MaxDurationSeconds { get; }
    protected abstract long MaxFileSizeBytes { get; }
    protected abstract string[] SupportedFormats { get; }
    protected abstract double[] SupportedAspectRatios { get; }
    protected abstract double AspectRatioTolerance { get; }

    protected SocialMediaPublisherBase(
        HttpClient httpClient,
        ISocialMediaCredentialService credentialService,
        AppSettings appSettings)
    {
        HttpClient = httpClient;
        CredentialService = credentialService;
        AppSettings = appSettings;
    }

    public abstract Task<string> GetAuthorizationUrlAsync(string redirectUri);
    public abstract Task<AuthResult> HandleAuthCallbackAsync(string code, string redirectUri);
    public abstract Task<PublishResult> PublishVideoAsync(PublishRequest request, IProgress<double>? progress = null);

    /// <summary>
    /// Gets the callback path used for OAuth redirects.
    /// Override this in derived classes to customize the callback path.
    /// </summary>
    protected virtual string OAuthCallbackPath => $"/oauth/{PlatformName.ToLowerInvariant().Replace(" ", "-")}/";

    /// <summary>
    /// Performs the complete OAuth authentication flow.
    /// Opens the browser for user authorization and handles the callback automatically.
    /// Override this method in derived classes for platform-specific authentication requirements.
    /// </summary>
    public virtual async Task<AuthResult> AuthenticateAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        await using var callbackListener = new OAuthCallbackListener(OAuthCallbackPath);

        try
        {
            // Start the callback listener
            await callbackListener.StartAsync();

            // Generate the authorization URL with the listener's redirect URI
            var authUrl = await GetAuthorizationUrlAsync(callbackListener.RedirectUri);

            // Perform the OAuth flow (opens browser and waits for callback)
            var callbackResult = await callbackListener.AuthenticateAsync(
                authUrl,
                expectedState: null, // State validation is handled by derived classes if needed
                timeout: timeout ?? TimeSpan.FromMinutes(5),
                cancellationToken: cancellationToken);

            if (!callbackResult.Success)
            {
                return new AuthResult
                {
                    Success = false,
                    Error = callbackResult.ErrorDescription ?? callbackResult.Error ?? "Authentication failed"
                };
            }

            // Exchange the authorization code for tokens
            return await HandleAuthCallbackAsync(callbackResult.Code!, callbackListener.RedirectUri);
        }
        catch (Exception ex)
        {
            return new AuthResult
            {
                Success = false,
                Error = $"Authentication failed: {ex.Message}"
            };
        }
    }

    public virtual async Task<bool> IsAuthenticatedAsync()
    {
        return await CredentialService.HasValidCredentialsAsync(PlatformName);
    }

    /// <summary>
    /// Validates that the stored tokens are still valid by checking with the platform's API.
    /// Override this in derived classes for platform-specific token validation.
    /// </summary>
    public virtual async Task<bool> ValidateAndRefreshTokensAsync()
    {
        // Default implementation just checks if credentials exist and haven't expired locally
        // Derived classes should override this to actually validate with the platform API
        var credentials = await GetCredentialsAsync();
        
        if (credentials == null || string.IsNullOrEmpty(credentials.AccessToken))
        {
            return false;
        }
        
        // Check if token is expired
        if (credentials.ExpiresAt.HasValue && credentials.ExpiresAt.Value < DateTime.UtcNow)
        {
            await DisconnectAsync();
            return false;
        }
        
        return true;
    }

    public virtual async Task DisconnectAsync()
    {
        await CredentialService.DeleteCredentialsAsync(PlatformName);
    }

    public virtual async Task<VideoValidationResult> ValidateVideoAsync(string videoPath)
    {
        var result = new VideoValidationResult { IsValid = true };

        try
        {
            if (!File.Exists(videoPath))
            {
                result.IsValid = false;
                result.Errors.Add("Video file not found.");
                return result;
            }

            // Get video metadata using FFProbe
            var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
            var videoStream = mediaInfo.VideoStreams.FirstOrDefault();

            if (videoStream == null)
            {
                result.IsValid = false;
                result.Errors.Add("No video stream found in file.");
                return result;
            }

            result.Metadata = new VideoMetadata
            {
                Duration = mediaInfo.Duration,
                Width = videoStream.Width,
                Height = videoStream.Height,
                FileSizeBytes = new FileInfo(videoPath).Length,
                Format = Path.GetExtension(videoPath).TrimStart('.').ToUpperInvariant()
            };

            // Validate duration
            if (mediaInfo.Duration.TotalSeconds > MaxDurationSeconds)
            {
                result.IsValid = false;
                result.Errors.Add($"Video duration ({mediaInfo.Duration:mm\\:ss}) exceeds maximum allowed ({TimeSpan.FromSeconds(MaxDurationSeconds):mm\\:ss}) for {PlatformName}.");
            }

            // Validate file size
            if (result.Metadata.FileSizeBytes > MaxFileSizeBytes)
            {
                result.IsValid = false;
                result.Errors.Add($"File size ({FormatFileSize(result.Metadata.FileSizeBytes)}) exceeds maximum allowed ({FormatFileSize(MaxFileSizeBytes)}) for {PlatformName}.");
            }

            // Validate format
            var extension = Path.GetExtension(videoPath).TrimStart('.').ToLowerInvariant();
            if (!SupportedFormats.Contains(extension))
            {
                result.IsValid = false;
                result.Errors.Add($"Format '{extension}' is not supported. Supported formats: {string.Join(", ", SupportedFormats)}.");
            }

            // Validate aspect ratio
            if (result.Metadata.AspectRatio.HasValue)
            {
                var aspectRatio = result.Metadata.AspectRatio.Value;
                var isValidAspectRatio = SupportedAspectRatios.Any(ar => 
                    Math.Abs(aspectRatio - ar) <= AspectRatioTolerance);

                if (!isValidAspectRatio)
                {
                    result.Warnings.Add($"Aspect ratio {aspectRatio:F2} may not be optimal for {PlatformName}. Recommended: 9:16 (0.56) for vertical, 16:9 (1.78) for horizontal.");
                }

                // Recommend vertical for shorts
                if (aspectRatio > 1.0)
                {
                    result.Warnings.Add($"Horizontal videos may have reduced reach on {PlatformName}. Consider using vertical (9:16) format for better engagement.");
                }
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Error analyzing video: {ex.Message}");
        }

        return result;
    }

    protected async Task<SocialMediaCredentials?> GetCredentialsAsync()
    {
        return await CredentialService.GetCredentialsAsync(PlatformName);
    }

    protected async Task SaveCredentialsAsync(SocialMediaCredentials credentials)
    {
        await CredentialService.SaveCredentialsAsync(PlatformName, credentials);
    }

    protected static string FormatFileSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int counter = 0;
        double number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    protected static string GenerateState()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Checks if an error message indicates a token-related issue.
        /// </summary>
        protected static bool IsTokenRelatedError(string? errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return false;

            var tokenIndicators = new[]
            {
                "token",
                "unauthorized",
                "authentication",
                "auth",
                "expired",
                "invalid_grant",
                "access_denied",
                "revoked",
                "session",
                "credential"
            };

            return tokenIndicators.Any(indicator => 
                errorMessage.Contains(indicator, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Creates a PublishResult for token-related errors and disconnects the account.
        /// </summary>
        protected async Task<PublishResult> CreateTokenErrorResultAsync(string errorMessage)
        {
            await DisconnectAsync();
        
            return new PublishResult
            {
                Success = false,
                Error = $"{errorMessage} Your account has been disconnected. Please reconnect to continue publishing.",
                IsTokenError = true
            };
        }
    }
