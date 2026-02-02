using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using VideoSplitter.Models;

namespace VideoSplitter.Services.SocialMediaPublishers;

/// <summary>
/// Instagram Graph API publisher for Instagram Reels
/// Docs: https://developers.facebook.com/docs/instagram-platform/instagram-graph-api/reference/ig-user/media
/// Note: Requires Facebook Business account and Instagram Professional (Business/Creator) account
/// </summary>
public class InstagramReelsPublisher : SocialMediaPublisherBase
{
    private const string AuthBaseUrl = "https://www.facebook.com/v18.0/dialog/oauth";
    private const string TokenUrl = "https://graph.facebook.com/v18.0/oauth/access_token";
    private const string GraphApiBaseUrl = "https://graph.facebook.com/v18.0";

    private readonly string _appId;
    private readonly string _appSecret;

    public override string PlatformName => "Instagram Reels";
    public override string PlatformIcon => "fab fa-instagram";

    // Instagram Reels constraints
    protected override int MaxDurationSeconds => 90; // Reels can be up to 90 seconds
    protected override long MaxFileSizeBytes => 1_000_000_000; // 1GB
    protected override string[] SupportedFormats => ["mp4", "mov"];
    protected override double[] SupportedAspectRatios => [9.0 / 16.0, 0.8, 1.91]; // 9:16 recommended, 4:5 to 1.91:1 supported
    protected override double AspectRatioTolerance => 0.15;

    public InstagramReelsPublisher(
        HttpClient httpClient,
        ISocialMediaCredentialService credentialService,
        AppSettings appSettings,
        string appId,
        string appSecret)
        : base(httpClient, credentialService, appSettings)
    {
        _appId = appId;
        _appSecret = appSecret;
    }

    public override Task<string> GetAuthorizationUrlAsync(string redirectUri)
    {
        var state = GenerateState();
        // Required scopes for Instagram Reels publishing
        var scope = "instagram_basic,instagram_content_publish,pages_show_list,pages_read_engagement";

        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _appId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = state
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        return Task.FromResult($"{AuthBaseUrl}?{queryString}");
    }

    public override async Task<AuthResult> HandleAuthCallbackAsync(string code, string redirectUri)
    {
        try
        {
            // Step 1: Exchange code for short-lived token
            var queryParams = new Dictionary<string, string>
            {
                ["client_id"] = _appId,
                ["client_secret"] = _appSecret,
                ["redirect_uri"] = redirectUri,
                ["code"] = code
            };

            var queryString = string.Join("&", queryParams.Select(kvp =>
                $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

            var response = await HttpClient.GetAsync($"{TokenUrl}?{queryString}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new AuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {responseContent}"
                };
            }

            var shortLivedToken = JsonSerializer.Deserialize<FacebookTokenResponse>(responseContent);

            if (shortLivedToken == null || string.IsNullOrEmpty(shortLivedToken.AccessToken))
            {
                return new AuthResult
                {
                    Success = false,
                    Error = "Invalid token response from Facebook"
                };
            }

            // Step 2: Exchange for long-lived token
            var longLivedToken = await ExchangeForLongLivedTokenAsync(shortLivedToken.AccessToken);

            // Step 3: Get Instagram Business Account ID
            var instagramAccount = await GetInstagramBusinessAccountAsync(longLivedToken.AccessToken!);

            if (instagramAccount == null)
            {
                return new AuthResult
                {
                    Success = false,
                    Error = "No Instagram Business or Creator account found. Please connect a professional Instagram account to your Facebook Page."
                };
            }

            var credentials = new SocialMediaCredentials
            {
                AccessToken = longLivedToken.AccessToken,
                ExpiresAt = DateTime.UtcNow.AddDays(60), // Long-lived tokens last ~60 days
                UserId = instagramAccount.Id,
                Username = instagramAccount.Username
            };

            await SaveCredentialsAsync(credentials);

            return new AuthResult
            {
                Success = true,
                AccessToken = longLivedToken.AccessToken,
                ExpiresAt = credentials.ExpiresAt,
                UserId = instagramAccount.Id,
                Username = instagramAccount.Username
            };
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

    public override async Task<VideoValidationResult> ValidateVideoAsync(string videoPath)
    {
        var result = await base.ValidateVideoAsync(videoPath);

        // Additional Instagram Reels specific validation
        if (result.IsValid && result.Metadata != null)
        {
            // Minimum duration for Reels
            if (result.Metadata.Duration.TotalSeconds < 3)
            {
                result.IsValid = false;
                result.Errors.Add("Instagram Reels must be at least 3 seconds long.");
            }

            // Recommend vertical
            if (result.Metadata.AspectRatio > 1.0)
            {
                result.Warnings.Add("Instagram Reels perform best with vertical (9:16) aspect ratio.");
            }

            // Check video codec requirements
            var extension = Path.GetExtension(videoPath).TrimStart('.').ToLowerInvariant();
            if (extension != "mp4")
            {
                result.Warnings.Add("MP4 format with H.264 codec is recommended for best compatibility.");
            }
        }

        return result;
        }

        /// <summary>
        /// Validates that the stored Instagram tokens are still valid by making a test API call.
        /// If tokens are invalid or expired, the publisher will be disconnected.
        /// </summary>
        public override async Task<bool> ValidateAndRefreshTokensAsync()
        {
            var credentials = await GetCredentialsAsync();
        
            if (credentials == null || string.IsNullOrEmpty(credentials.AccessToken))
            {
                return false;
            }

            // Check if token is expired based on stored expiration
            if (credentials.ExpiresAt.HasValue && credentials.ExpiresAt.Value < DateTime.UtcNow)
            {
                await DisconnectAsync();
                return false;
            }

            // Validate token by making a test API call
            try
            {
                // Try to get account info to verify the token is valid
                var response = await HttpClient.GetAsync(
                    $"{GraphApiBaseUrl}/{credentials.UserId}?fields=id&access_token={credentials.AccessToken}");
            
                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                
                    // Check for token-related errors
                    if (IsTokenRelatedError(content))
                    {
                        await DisconnectAsync();
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                // If we can't validate, assume the token is invalid
                await DisconnectAsync();
                return false;
            }
        }

        public override async Task<PublishResult> PublishVideoAsync(PublishRequest request, IProgress<double>? progress = null)
    {
        try
        {
            var credentials = await GetCredentialsAsync();
            if (credentials == null || string.IsNullOrEmpty(credentials.AccessToken))
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Not authenticated with Instagram. Please connect your account first."
                };
            }

            // Validate video
            var validation = await ValidateVideoAsync(request.VideoPath);
            if (!validation.IsValid)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = string.Join("; ", validation.Errors)
                };
            }

            progress?.Report(0.1);

            // Build caption with hashtags
            var caption = request.Title;
            if (!string.IsNullOrEmpty(request.Description))
            {
                caption += $"\n\n{request.Description}";
            }
            if (request.Tags.Count != 0)
            {
                caption += "\n\n" + string.Join(" ", request.Tags.Select(t => $"#{t.Replace(" ", "")}"));
            }

            // Step 1: Create media container (upload video)
                    var containerResult = await CreateMediaContainerAsync(
                        credentials.AccessToken,
                        credentials.UserId!,
                        request.VideoPath,
                        caption);

                    if (!containerResult.Success)
                    {
                        // Check if this is a token-related error
                        if (IsTokenRelatedError(containerResult.Error))
                        {
                            return await CreateTokenErrorResultAsync(containerResult.Error!);
                        }

                        return new PublishResult
                        {
                            Success = false,
                            Error = containerResult.Error
                        };
                    }

                    progress?.Report(0.4);

                    // Step 2: Wait for media to be ready
                    var waitResult = await WaitForMediaReadyAsync(
                        credentials.AccessToken,
                        containerResult.ContainerId!,
                        progress);

                    if (!waitResult.Success)
                    {
                        // Check if this is a token-related error
                        if (IsTokenRelatedError(waitResult.Error))
                        {
                            return await CreateTokenErrorResultAsync(waitResult.Error!);
                        }

                        return new PublishResult
                        {
                            Success = false,
                            Error = waitResult.Error
                        };
                    }

                    progress?.Report(0.8);

                    // Step 3: Publish the media
                    var publishResult = await PublishMediaAsync(
                        credentials.AccessToken,
                        credentials.UserId!,
                        containerResult.ContainerId!);

                    progress?.Report(1.0);

                    if (publishResult.Success)
                    {
                        return new PublishResult
                        {
                            Success = true,
                            PostId = publishResult.MediaId,
                            PostUrl = $"https://www.instagram.com/reel/{publishResult.MediaId}/"
                        };
                    }

                    // Check if this is a token-related error
                    if (IsTokenRelatedError(publishResult.Error))
                    {
                        return await CreateTokenErrorResultAsync(publishResult.Error!);
                    }

                    return new PublishResult
                    {
                        Success = false,
                        Error = publishResult.Error
                    };
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Publishing failed: {ex.Message}";
            
                    // Check if this is a token-related error
                    if (IsTokenRelatedError(ex.Message))
                    {
                        return await CreateTokenErrorResultAsync(errorMessage);
                    }

                    return new PublishResult
                    {
                        Success = false,
                        Error = errorMessage
                    };
                }
            }

            private async Task<FacebookTokenResponse> ExchangeForLongLivedTokenAsync(string shortLivedToken)
            {
                var queryParams = new Dictionary<string, string>
                {
            ["grant_type"] = "fb_exchange_token",
            ["client_id"] = _appId,
            ["client_secret"] = _appSecret,
            ["fb_exchange_token"] = shortLivedToken
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        var response = await HttpClient.GetAsync($"{TokenUrl}?{queryString}");
        var content = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<FacebookTokenResponse>(content) ?? new FacebookTokenResponse();
    }

    private async Task<InstagramAccount?> GetInstagramBusinessAccountAsync(string accessToken)
    {
        try
        {
            // Get user's Facebook Pages
            var pagesResponse = await HttpClient.GetAsync(
                $"{GraphApiBaseUrl}/me/accounts?access_token={accessToken}");
            var pagesContent = await pagesResponse.Content.ReadAsStringAsync();
            var pages = JsonSerializer.Deserialize<FacebookPagesResponse>(pagesContent);

            if (pages?.Data == null || pages.Data.Count == 0)
            {
                return null;
            }

            // Find page with connected Instagram Business Account
            foreach (var page in pages.Data)
            {
                var igResponse = await HttpClient.GetAsync(
                    $"{GraphApiBaseUrl}/{page.Id}?fields=instagram_business_account&access_token={accessToken}");
                var igContent = await igResponse.Content.ReadAsStringAsync();
                var igData = JsonSerializer.Deserialize<FacebookPageWithInstagram>(igContent);

                if (igData?.InstagramBusinessAccount != null)
                {
                    // Get Instagram account details
                    var igDetailsResponse = await HttpClient.GetAsync(
                        $"{GraphApiBaseUrl}/{igData.InstagramBusinessAccount.Id}?fields=id,username&access_token={accessToken}");
                    var igDetailsContent = await igDetailsResponse.Content.ReadAsStringAsync();
                    var igDetails = JsonSerializer.Deserialize<InstagramAccount>(igDetailsContent);

                    return igDetails;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<InstagramContainerResult> CreateMediaContainerAsync(
        string accessToken,
        string igUserId,
        string videoPath,
        string caption)
    {
        try
        {
            // Instagram requires video to be hosted at a public URL
            // For local files, you would need to upload to a hosting service first
            // This implementation assumes the video is already at a public URL
            // In a real implementation, you'd upload to a CDN or use a video hosting service

            // For development/testing, we'll use the video path directly
            // In production, this should be a publicly accessible URL
            var videoUrl = videoPath;

            // Note: In production, you would need to:
            // 1. Upload the video to a publicly accessible server (S3, Azure Blob, etc.)
            // 2. Use that public URL here

            var createUrl = $"{GraphApiBaseUrl}/{igUserId}/media";

            var formData = new Dictionary<string, string>
            {
                ["media_type"] = "REELS",
                ["video_url"] = videoUrl,
                ["caption"] = caption.Length > 2200 ? caption[..2200] : caption,
                ["share_to_feed"] = "true",
                ["access_token"] = accessToken
            };

            var response = await HttpClient.PostAsync(createUrl, new FormUrlEncodedContent(formData));
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = JsonSerializer.Deserialize<FacebookError>(responseContent);
                return new InstagramContainerResult
                {
                    Success = false,
                    Error = error?.Error?.Message ?? responseContent
                };
            }

            var containerResponse = JsonSerializer.Deserialize<InstagramContainerResponse>(responseContent);

            return new InstagramContainerResult
            {
                Success = true,
                ContainerId = containerResponse?.Id
            };
        }
        catch (Exception ex)
        {
            return new InstagramContainerResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<InstagramWaitResult> WaitForMediaReadyAsync(
        string accessToken,
        string containerId,
        IProgress<double>? progress)
    {
        const int maxAttempts = 30;
        const int delayMs = 5000;

        for (int i = 0; i < maxAttempts; i++)
        {
            var statusUrl = $"{GraphApiBaseUrl}/{containerId}?fields=status_code&access_token={accessToken}";
            var response = await HttpClient.GetAsync(statusUrl);
            var content = await response.Content.ReadAsStringAsync();

            var status = JsonSerializer.Deserialize<InstagramMediaStatus>(content);

            switch (status?.StatusCode)
            {
                case "FINISHED":
                    return new InstagramWaitResult { Success = true };
                case "ERROR":
                    return new InstagramWaitResult
                    {
                        Success = false,
                        Error = "Media processing failed on Instagram's servers."
                    };
                case "IN_PROGRESS":
                    progress?.Report(0.4 + (0.4 * i / maxAttempts));
                    await Task.Delay(delayMs);
                    break;
                default:
                    await Task.Delay(delayMs);
                    break;
            }
        }

        return new InstagramWaitResult
        {
            Success = false,
            Error = "Timeout waiting for media to be processed."
        };
    }

    private async Task<InstagramPublishResult> PublishMediaAsync(
        string accessToken,
        string igUserId,
        string containerId)
    {
        try
        {
            var publishUrl = $"{GraphApiBaseUrl}/{igUserId}/media_publish";

            var formData = new Dictionary<string, string>
            {
                ["creation_id"] = containerId,
                ["access_token"] = accessToken
            };

            var response = await HttpClient.PostAsync(publishUrl, new FormUrlEncodedContent(formData));
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var error = JsonSerializer.Deserialize<FacebookError>(responseContent);
                return new InstagramPublishResult
                {
                    Success = false,
                    Error = error?.Error?.Message ?? responseContent
                };
            }

            var publishResponse = JsonSerializer.Deserialize<InstagramPublishResponse>(responseContent);

            return new InstagramPublishResult
            {
                Success = true,
                MediaId = publishResponse?.Id
            };
        }
        catch (Exception ex)
        {
            return new InstagramPublishResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    #region Instagram/Facebook Response Models

    private class FacebookTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private class FacebookPagesResponse
    {
        [JsonPropertyName("data")]
        public List<FacebookPage>? Data { get; set; }
    }

    private class FacebookPage
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private class FacebookPageWithInstagram
    {
        [JsonPropertyName("instagram_business_account")]
        public InstagramBusinessAccount? InstagramBusinessAccount { get; set; }
    }

    private class InstagramBusinessAccount
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private class InstagramAccount
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    private class InstagramContainerResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private class InstagramMediaStatus
    {
        [JsonPropertyName("status_code")]
        public string? StatusCode { get; set; }
    }

    private class InstagramPublishResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private class FacebookError
    {
        [JsonPropertyName("error")]
        public FacebookErrorDetail? Error { get; set; }
    }

    private class FacebookErrorDetail
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("code")]
        public int Code { get; set; }
    }

    private class InstagramContainerResult
    {
        public bool Success { get; set; }
        public string? ContainerId { get; set; }
        public string? Error { get; set; }
    }

    private class InstagramWaitResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    private class InstagramPublishResult
    {
        public bool Success { get; set; }
        public string? MediaId { get; set; }
        public string? Error { get; set; }
    }

    #endregion
}
