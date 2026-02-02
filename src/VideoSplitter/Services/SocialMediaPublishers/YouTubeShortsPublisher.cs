using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using VideoSplitter.Models;

namespace VideoSplitter.Services.SocialMediaPublishers;

/// <summary>
/// YouTube Data API v3 publisher for YouTube Shorts
/// Uses the official Google.Apis.YouTube.v3 client library
/// Docs: https://developers.google.com/youtube/v3/docs/videos/insert
/// </summary>
public class YouTubeShortsPublisher : SocialMediaPublisherBase
{
    private readonly string _clientId;
    private readonly string _clientSecret;

    public override string PlatformName => "YouTube Shorts";
    public override string PlatformIcon => "fab fa-youtube";

    // YouTube Shorts constraints
    protected override int MaxDurationSeconds => 180; // Shorts must be ?180 seconds
    protected override long MaxFileSizeBytes => 256_000_000_000; // 256GB (YouTube's general limit)
    protected override string[] SupportedFormats => ["mp4", "mov", "avi", "wmv", "flv", "3gp", "webm", "mkv"];
    protected override double[] SupportedAspectRatios => [9.0 / 16.0, 1.0]; // Shorts prefer 9:16 vertical
    protected override double AspectRatioTolerance => 0.1;

    public YouTubeShortsPublisher(
        HttpClient httpClient,
        ISocialMediaCredentialService credentialService,
        AppSettings appSettings,
        string clientId,
        string clientSecret)
        : base(httpClient, credentialService, appSettings)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public override Task<string> GetAuthorizationUrlAsync(string redirectUri)
    {
        var state = GenerateState();

        // Build the Google OAuth URL for the initial redirect
        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = $"{YouTubeService.Scope.YoutubeUpload} {YouTubeService.Scope.Youtube}",
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return Task.FromResult($"https://accounts.google.com/o/oauth2/v2/auth?{queryString}");
    }

    public override async Task<AuthResult> HandleAuthCallbackAsync(string code, string redirectUri)
    {
        try
        {
            // Exchange authorization code for tokens using Google's flow
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _clientId,
                    ClientSecret = _clientSecret
                },
                Scopes = [YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.Youtube]
            });

            var tokenResponse = await flow.ExchangeCodeForTokenAsync(
                userId: "user",
                code: code,
                redirectUri: redirectUri,
                CancellationToken.None);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                return new AuthResult
                {
                    Success = false,
                    Error = "Invalid token response from Google"
                };
            }

            // Create credentials and get channel info
            var credential = new UserCredential(flow, "user", tokenResponse);
            var channelInfo = await GetChannelInfoAsync(credential);

            var credentials = new SocialMediaCredentials
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = tokenResponse.IssuedUtc.AddSeconds(tokenResponse.ExpiresInSeconds ?? 3600),
                UserId = channelInfo?.ChannelId,
                Username = channelInfo?.ChannelTitle
            };

            await SaveCredentialsAsync(credentials);

            return new AuthResult
            {
                Success = true,
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = credentials.ExpiresAt,
                UserId = credentials.UserId,
                Username = credentials.Username
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

        // Additional YouTube Shorts specific validation
        if (result.IsValid && result.Metadata != null)
        {
            // Shorts should be vertical
            if (result.Metadata.AspectRatio > 1.0)
            {
                result.Warnings.Add("YouTube Shorts perform best with vertical (9:16) aspect ratio. Horizontal videos may not be classified as Shorts.");
            }

            // Duration check for Shorts
            if (result.Metadata.Duration.TotalSeconds > 180)
            {
                result.Warnings.Add("Videos longer than 180 seconds will be uploaded as regular YouTube videos, not Shorts.");
            }
        }

        return result;
        }

        /// <summary>
        /// Validates that the stored YouTube tokens are still valid by attempting to create a service.
        /// If tokens are invalid or expired, the publisher will be disconnected.
        /// </summary>
        public override async Task<bool> ValidateAndRefreshTokensAsync()
        {
            var credentials = await GetCredentialsAsync();
        
            if (credentials == null || string.IsNullOrEmpty(credentials.AccessToken))
            {
                return false;
            }

            // Check if token is expired and we don't have a refresh token
            if (credentials.ExpiresAt.HasValue && 
                credentials.ExpiresAt.Value < DateTime.UtcNow && 
                string.IsNullOrEmpty(credentials.RefreshToken))
            {
                await DisconnectAsync();
                return false;
            }

            // Try to create a YouTube service and make a test call
            try
            {
                var youtubeService = await CreateYouTubeServiceAsync(credentials);
            
                if (youtubeService == null)
                {
                    await DisconnectAsync();
                    return false;
                }

                // Make a simple API call to verify the token works
                var channelsRequest = youtubeService.Channels.List("id");
                channelsRequest.Mine = true;
                await channelsRequest.ExecuteAsync();

                return true;
            }
            catch
            {
                // Token is invalid - disconnect
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
                    Error = "Not authenticated with YouTube. Please connect your account first."
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

            progress?.Report(0.05);

            // Create YouTube service with credentials
            var youtubeService = await CreateYouTubeServiceAsync(credentials);
            if (youtubeService == null)
            {
                return await CreateTokenErrorResultAsync("Failed to create YouTube service.");
            }

            progress?.Report(0.1);

            // Prepare title with #Shorts for discovery
            var title = request.Title;
            if (!title.Contains("#Shorts", StringComparison.OrdinalIgnoreCase) &&
                validation.Metadata!.Duration.TotalSeconds <= 60)
            {
                title = $"{title} #Shorts";
            }

            // Build description with tags
            var description = request.Description ?? "";
            if (request.Tags.Count != 0)
            {
                description += "\n\n" + string.Join(" ", request.Tags.Select(t => $"#{t.Replace(" ", "")}"));
            }

            // Create video metadata
            var video = new Video
            {
                Snippet = new VideoSnippet
                {
                    Title = title.Length > 100 ? title[..100] : title,
                    Description = description,
                    Tags = request.Tags.ToArray(),
                    CategoryId = "28" // Science and Technology
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = request.Privacy switch
                    {
                        PrivacyLevel.Public => "public",
                        PrivacyLevel.Private => "private",
                        PrivacyLevel.Unlisted => "unlisted",
                        _ => "private"
                    },
                    SelfDeclaredMadeForKids = false
                }
            };

            // Upload video using the Google API client
            await using var fileStream = new FileStream(request.VideoPath, FileMode.Open, FileAccess.Read);

            var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");

            // Track progress
            videosInsertRequest.ProgressChanged += uploadProgress =>
            {
                switch (uploadProgress.Status)
                {
                    case UploadStatus.Uploading:
                        var percentComplete = (double)uploadProgress.BytesSent / validation.Metadata!.FileSizeBytes;
                        progress?.Report(0.1 + (percentComplete * 0.85)); // 10% to 95%
                        break;
                    case UploadStatus.Failed:
                        // Error will be handled after upload completes
                        break;
                }
            };

            Video? uploadedVideo = null;
            videosInsertRequest.ResponseReceived += v =>
            {
                uploadedVideo = v;
            };

            var uploadResult = await videosInsertRequest.UploadAsync();

            if (uploadResult.Status == UploadStatus.Failed)
            {
                var errorMessage = $"Upload failed: {uploadResult.Exception?.Message ?? "Unknown error"}";
                
                // Check if this is a token-related error
                if (IsTokenRelatedError(uploadResult.Exception?.Message))
                {
                    return await CreateTokenErrorResultAsync(errorMessage);
                }

                return new PublishResult
                {
                    Success = false,
                    Error = errorMessage
                };
            }

            progress?.Report(1.0);

            if (uploadedVideo == null)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = "Upload completed but no video ID was returned"
                };
            }

            // Return the Shorts URL if duration <= 60s, otherwise regular YouTube URL
            var videoUrl = validation.Metadata!.Duration.TotalSeconds <= 60
                ? $"https://www.youtube.com/shorts/{uploadedVideo.Id}"
                : $"https://www.youtube.com/watch?v={uploadedVideo.Id}";

            return new PublishResult
            {
                Success = true,
                PostId = uploadedVideo.Id,
                PostUrl = videoUrl
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

        /// <summary>
        /// Creates a YouTubeService instance with the stored credentials.
        /// Handles token refresh automatically.
        /// </summary>
    private async Task<YouTubeService?> CreateYouTubeServiceAsync(SocialMediaCredentials credentials)
    {
        try
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _clientId,
                    ClientSecret = _clientSecret
                },
                Scopes = [YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.Youtube]
            });

            var tokenResponse = new TokenResponse
            {
                AccessToken = credentials.AccessToken,
                RefreshToken = credentials.RefreshToken,
                IssuedUtc = credentials.ExpiresAt?.AddSeconds(-3600) ?? DateTime.UtcNow,
                ExpiresInSeconds = credentials.ExpiresAt.HasValue
                    ? (long)(credentials.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds
                    : 3600
            };

            var userCredential = new UserCredential(flow, "user", tokenResponse);

            // Check if token needs refresh
            if (credentials.ExpiresAt.HasValue && credentials.ExpiresAt.Value < DateTime.UtcNow.AddMinutes(5))
            {
                if (await userCredential.RefreshTokenAsync(CancellationToken.None))
                {
                    // Save the refreshed token
                    credentials.AccessToken = userCredential.Token.AccessToken;
                    credentials.ExpiresAt = userCredential.Token.IssuedUtc.AddSeconds(
                        userCredential.Token.ExpiresInSeconds ?? 3600);
                    await SaveCredentialsAsync(credentials);
                }
            }

            return new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = userCredential,
                ApplicationName = "VideoSplitter"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating YouTube service: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the authenticated user's YouTube channel information.
    /// </summary>
    private async Task<YouTubeChannelInfo?> GetChannelInfoAsync(UserCredential credential)
    {
        try
        {
            var youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "VideoSplitter"
            });

            var channelsRequest = youtubeService.Channels.List("snippet");
            channelsRequest.Mine = true;

            var channelsResponse = await channelsRequest.ExecuteAsync();
            var channel = channelsResponse.Items?.FirstOrDefault();

            if (channel != null)
            {
                return new YouTubeChannelInfo
                {
                    ChannelId = channel.Id,
                    ChannelTitle = channel.Snippet?.Title
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting channel info: {ex.Message}");
            return null;
        }
    }

    #region Helper Classes

    private class YouTubeChannelInfo
    {
        public string? ChannelId { get; set; }
        public string? ChannelTitle { get; set; }
    }

    #endregion
}
