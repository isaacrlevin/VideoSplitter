using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using VideoSplitter.Models;

namespace VideoSplitter.Services.SocialMediaPublishers;

/// <summary>
/// TikTok Content Posting API publisher
/// Docs: https://developers.tiktok.com/doc/content-posting-api-get-started/
/// </summary>
public class TikTokPublisher : SocialMediaPublisherBase
{
    private const string AuthBaseUrl = "https://www.tiktok.com/v2/auth/authorize/";
    private const string TokenUrl = "https://open.tiktokapis.com/v2/oauth/token/";
    private const string CreatorInfoUrl = "https://open.tiktokapis.com/v2/post/publish/creator_info/query/";
    private const string UploadInitUrl = "https://open.tiktokapis.com/v2/post/publish/inbox/video/init/";
    private const string PublishStatusUrl = "https://open.tiktokapis.com/v2/post/publish/status/fetch/";

    private readonly string _clientKey;
    private readonly string _clientSecret;

    // PKCE code verifier - stored temporarily between authorization request and callback
    private string? _codeVerifier;
    // State for CSRF protection - stored temporarily between authorization request and callback
    private string? _state;

    public override string PlatformName => "TikTok";
    public override string PlatformIcon => "fab fa-tiktok";

    // TikTok video constraints
    protected override int MaxDurationSeconds => 600; // 10 minutes for most accounts (60s for some)
    protected override long MaxFileSizeBytes => 287_600_000; // ~287.6 MB
    protected override string[] SupportedFormats => ["mp4", "webm", "mov"];
    protected override double[] SupportedAspectRatios => [9.0 / 16.0, 1.0, 16.0 / 9.0]; // 9:16, 1:1, 16:9
    protected override double AspectRatioTolerance => 0.1;

    public TikTokPublisher(
        HttpClient httpClient,
        ISocialMediaCredentialService credentialService,
        AppSettings appSettings,
        string clientKey,
        string clientSecret)
        : base(httpClient, credentialService, appSettings)
    {
        _clientKey = clientKey;
        _clientSecret = clientSecret;
    }

    public override Task<string> GetAuthorizationUrlAsync(string redirectUri)
    {
        _state = GenerateState();
        var scope = "user.info.basic,video.publish,video.upload";

        // Generate PKCE code verifier and challenge for desktop authorization
        _codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(_codeVerifier);

        var queryParams = new Dictionary<string, string>
        {
            ["client_key"] = _clientKey,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = _state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));


        return Task.FromResult($"{AuthBaseUrl}?{queryString}");
    }

    /// <summary>
    /// Performs the complete OAuth authentication flow for TikTok desktop apps.
    /// Includes PKCE and state validation for security.
    /// </summary>
    public override async Task<AuthResult> AuthenticateAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        await using var callbackListener = new OAuthCallbackListener(OAuthCallbackPath);

        try
        {
            // Start the callback listener
            await callbackListener.StartAsync();

            // Generate the authorization URL with the listener's redirect URI
            // This also generates the PKCE code verifier/challenge and state
            var authUrl = await GetAuthorizationUrlAsync(callbackListener.RedirectUri);

            // Perform the OAuth flow (opens browser and waits for callback)
            var callbackResult = await callbackListener.AuthenticateAsync(
                authUrl,
                expectedState: _state, // Validate state for CSRF protection
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

    public override async Task<AuthResult> HandleAuthCallbackAsync(string code, string redirectUri)
    {
        try
        {
            if (string.IsNullOrEmpty(_codeVerifier))
            {
                return new AuthResult
                {
                    Success = false,
                    Error = "PKCE code verifier not found. Please restart the authentication process."
                };
            }

            var requestBody = new Dictionary<string, string>
            {
                ["client_key"] = _clientKey,
                ["client_secret"] = _clientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = _codeVerifier
            };

            var content = new FormUrlEncodedContent(requestBody);
            var response = await HttpClient.PostAsync(TokenUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new AuthResult
                {
                    Success = false,
                    Error = $"Token exchange failed: {responseContent}"
                };
            }

            var tokenResponse = JsonSerializer.Deserialize<TikTokTokenResponse>(responseContent);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                return new AuthResult
                {
                    Success = false,
                    Error = "Invalid token response from TikTok"
                };
            }

            var credentials = new SocialMediaCredentials
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                UserId = tokenResponse.OpenId
            };

            await SaveCredentialsAsync(credentials);

            return new AuthResult
            {
                Success = true,
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = credentials.ExpiresAt,
                UserId = tokenResponse.OpenId
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

        /// <summary>
        /// Validates that the stored TikTok tokens are still valid by querying the creator info API.
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
                var creatorInfo = await QueryCreatorInfoAsync(credentials.AccessToken);
            
                if (!creatorInfo.Success)
                {
                    // Token is invalid - disconnect
                    await DisconnectAsync();
                    return false;
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
                    Error = "Not authenticated with TikTok. Please connect your account first."
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

            // Step 1: Query creator info (required before posting)
            var creatorInfo = await QueryCreatorInfoAsync(credentials.AccessToken);
            if (!creatorInfo.Success)
            {
                // Check if this is a token-related error
                if (IsTokenRelatedError(creatorInfo.Error))
                {
                    return await CreateTokenErrorResultAsync(creatorInfo.Error!);
                }

                return new PublishResult
                {
                    Success = false,
                    Error = creatorInfo.Error
                };
            }

            // Validate video duration against creator's max allowed duration
            if (validation.Metadata!.Duration.TotalSeconds > creatorInfo.MaxVideoDurationSeconds)
            {
                return new PublishResult
                {
                    Success = false,
                    Error = $"Video duration ({validation.Metadata.Duration:mm\\:ss}) exceeds your TikTok account's maximum allowed duration ({TimeSpan.FromSeconds(creatorInfo.MaxVideoDurationSeconds):mm\\:ss})."
                };
            }

            progress?.Report(0.1);

            // Step 2: Initialize upload
            var initResult = await InitializeUploadAsync(credentials.AccessToken, request, validation.Metadata!, creatorInfo);
            if (!initResult.Success)
            {
                // Check if this is a token-related error
                if (IsTokenRelatedError(initResult.Error))
                {
                    return await CreateTokenErrorResultAsync(initResult.Error!);
                }

                return new PublishResult
                {
                    Success = false,
                    Error = initResult.Error
                };
            }

            progress?.Report(0.2);

            // Step 3: Upload video chunks
            var uploadResult = await UploadVideoChunksAsync(
                initResult.UploadUrl!,
                request.VideoPath,
                validation.Metadata!.FileSizeBytes,
                progress);

            if (!uploadResult.Success)
            {
                // Check if this is a token-related error
                if (IsTokenRelatedError(uploadResult.Error))
                {
                    return await CreateTokenErrorResultAsync(uploadResult.Error!);
                }

                return new PublishResult
                {
                    Success = false,
                    Error = uploadResult.Error
                };
            }


            progress?.Report(0.9);

            // Step 3: Check publish status
            var statusResult = await CheckPublishStatusAsync(credentials.AccessToken, initResult.PublishId!);

            progress?.Report(1.0);

            return statusResult;
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
        /// Queries creator info from TikTok API to get available privacy options and account restrictions.
        /// This must be called before initializing an upload.
        /// </summary>
    private async Task<TikTokCreatorInfoResult> QueryCreatorInfoAsync(string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, CreatorInfoUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await HttpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new TikTokCreatorInfoResult
                {
                    Success = false,
                    Error = $"Failed to query creator info: {responseContent}"
                };
            }

            var creatorResponse = JsonSerializer.Deserialize<TikTokCreatorInfoResponse>(responseContent);

            if (creatorResponse?.Error?.Code != "ok" && !string.IsNullOrEmpty(creatorResponse?.Error?.Code))
            {
                var errorMessage = creatorResponse.Error.Code switch
                {
                    "spam_risk_too_many_posts" => "Daily post limit reached for your TikTok account.",
                    "spam_risk_user_banned_from_posting" => "Your TikTok account is currently banned from posting.",
                    "reached_active_user_cap" => "Daily quota for publishing users has been reached. Try again later.",
                    "access_token_invalid" => "TikTok authentication expired. Please reconnect your account.",
                    "scope_not_authorized" => "Video publishing permission not granted. Please reconnect your account.",
                    "rate_limit_exceeded" => "Too many requests. Please wait a moment and try again.",
                    _ => creatorResponse.Error.Message ?? $"TikTok error: {creatorResponse.Error.Code}"
                };

                return new TikTokCreatorInfoResult
                {
                    Success = false,
                    Error = errorMessage
                };
            }

            if (creatorResponse?.Data == null)
            {
                return new TikTokCreatorInfoResult
                {
                    Success = false,
                    Error = "Invalid response from TikTok"
                };
            }

            return new TikTokCreatorInfoResult
            {
                Success = true,
                CreatorAvatarUrl = creatorResponse.Data.CreatorAvatarUrl,
                CreatorUsername = creatorResponse.Data.CreatorUsername,
                CreatorNickname = creatorResponse.Data.CreatorNickname,
                PrivacyLevelOptions = creatorResponse.Data.PrivacyLevelOptions ?? ["SELF_ONLY"],
                CommentDisabled = creatorResponse.Data.CommentDisabled,
                DuetDisabled = creatorResponse.Data.DuetDisabled,
                StitchDisabled = creatorResponse.Data.StitchDisabled,
                MaxVideoDurationSeconds = creatorResponse.Data.MaxVideoPostDurationSec ?? 60
            };
        }
        catch (Exception ex)
        {
            return new TikTokCreatorInfoResult
            {
                Success = false,
                Error = $"Failed to query creator info: {ex.Message}"
            };
        }
    }

    private async Task<TikTokInitResult> InitializeUploadAsync(
        string accessToken,
        PublishRequest request,
        VideoMetadata metadata,
        TikTokCreatorInfoResult creatorInfo)
    {
        try
        {
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Map requested privacy level to one that's available for this creator
            var requestedPrivacy = request.Privacy switch
            {
                PrivacyLevel.Public => "PUBLIC_TO_EVERYONE",
                PrivacyLevel.Private => "SELF_ONLY",
                PrivacyLevel.FriendsOnly => "MUTUAL_FOLLOW_FRIENDS",
                _ => "PUBLIC_TO_EVERYONE"
            };

            // Use the requested privacy level if available, otherwise fall back to SELF_ONLY
            var privacyLevel = creatorInfo.PrivacyLevelOptions.Contains(requestedPrivacy)
                ? requestedPrivacy
                : creatorInfo.PrivacyLevelOptions.FirstOrDefault() ?? "SELF_ONLY";

            // Respect creator's disabled settings
            var disableComment = !request.AllowComments || creatorInfo.CommentDisabled;
            var disableDuet = !request.AllowDuet || creatorInfo.DuetDisabled;
            var disableStitch = !request.AllowStitch || creatorInfo.StitchDisabled;

            // TikTok chunk requirements:
            // - Each chunk must be 5-64 MB, except final chunk can be up to 128 MB
            // - Files < 5 MB must be uploaded as a whole (chunk_size = file_size)
            // - total_chunk_count = floor(video_size / chunk_size), minimum 1
            // - Final chunk contains all remaining bytes
            const long maxChunkSize = 64_000_000;     // 64 MB maximum per chunk

            long chunkSize;
            int totalChunkCount;

            if (metadata.FileSizeBytes <= maxChunkSize)
            {
                // Files up to 64 MB can be uploaded as a single chunk
                chunkSize = metadata.FileSizeBytes;
                totalChunkCount = 1;
            }
            else
            {
                // For larger files, split into evenly-sized chunks (each <= 64 MB)
                totalChunkCount = (int)Math.Ceiling((double)metadata.FileSizeBytes / maxChunkSize);
                chunkSize = metadata.FileSizeBytes / totalChunkCount;
            }

            var initRequest = new
            {
                post_info = new
                {
                    title = request.Title.Length > 150 ? request.Title[..150] : request.Title,
                    privacy_level = privacyLevel,
                    disable_duet = disableDuet,
                    disable_comment = disableComment,
                    disable_stitch = disableStitch
                },
                source_info = new
                {
                    source = "FILE_UPLOAD",
                    video_size = metadata.FileSizeBytes,
                    chunk_size = chunkSize,
                    total_chunk_count = totalChunkCount
                }
            };

            var response = await HttpClient.PostAsJsonAsync(UploadInitUrl, initRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new TikTokInitResult
                {
                    Success = false,
                    Error = $"Upload initialization failed: {responseContent}"
                };
            }

            var initResponse = JsonSerializer.Deserialize<TikTokInitResponse>(responseContent);

            if (initResponse?.Data == null)
            {
                return new TikTokInitResult
                {
                    Success = false,
                    Error = "Invalid response from TikTok"
                };
            }

            return new TikTokInitResult
            {
                Success = true,
                UploadUrl = initResponse.Data.UploadUrl,
                PublishId = initResponse.Data.PublishId
            };
        }
        catch (Exception ex)
        {
            return new TikTokInitResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<TikTokUploadResult> UploadVideoChunksAsync(
        string uploadUrl,
        string videoPath,
        long fileSize,
        IProgress<double>? progress)
    {
        try
        {
            // TikTok chunk requirements:
            // - Each chunk must be 5-64 MB, except final chunk can be up to 128 MB
            // - Files < 5 MB must be uploaded as a whole
            // - total_chunk_count = floor(video_size / chunk_size), minimum 1
            // - Chunks must be uploaded sequentially
            const long maxChunkSize = 64_000_000;     // 64 MB maximum per chunk

            long chunkSize;
            int totalChunks;

            if (fileSize <= maxChunkSize)
            {
                // Files up to 64 MB can be uploaded as a single chunk
                chunkSize = fileSize;
                totalChunks = 1;
            }
            else
            {
                // For larger files, split into evenly-sized chunks (each <= 64 MB)
                totalChunks = (int)Math.Ceiling((double)fileSize / maxChunkSize);
                chunkSize = fileSize / totalChunks;
            }

            await using var fileStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read);

            for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                var startByte = chunkIndex * chunkSize;
                long bytesToRead;

                if (chunkIndex == totalChunks - 1)
                {
                    // Final chunk gets all remaining bytes (can be larger than chunkSize, up to 128 MB)
                    bytesToRead = fileSize - startByte;
                }
                else
                {
                    bytesToRead = chunkSize;
                }

                // Read the chunk data
                var buffer = new byte[bytesToRead];
                var totalBytesRead = 0;
                while (totalBytesRead < bytesToRead)
                {
                    var bytesRead = await fileStream.ReadAsync(buffer.AsMemory(totalBytesRead, (int)(bytesToRead - totalBytesRead)));
                    if (bytesRead == 0) break;
                    totalBytesRead += bytesRead;
                }

                var chunkData = buffer[..totalBytesRead];
                var endByte = startByte + totalBytesRead - 1;

                using var content = new ByteArrayContent(chunkData);
                content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                content.Headers.Add("Content-Range", $"bytes {startByte}-{endByte}/{fileSize}");

                var response = await HttpClient.PutAsync(uploadUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new TikTokUploadResult
                    {
                        Success = false,
                        Error = $"Chunk {chunkIndex + 1}/{totalChunks} upload failed: {errorContent}"
                    };
                }

                progress?.Report(0.2 + (0.7 * (chunkIndex + 1) / totalChunks));
            }

            return new TikTokUploadResult { Success = true };
        }
        catch (Exception ex)
        {
            return new TikTokUploadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<PublishResult> CheckPublishStatusAsync(string accessToken, string publishId)
    {
        try
        {
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var request = new { publish_id = publishId };
            var response = await HttpClient.PostAsJsonAsync(PublishStatusUrl, request);
            var responseContent = await response.Content.ReadAsStringAsync();

            var statusResponse = JsonSerializer.Deserialize<TikTokStatusResponse>(responseContent);

            if (statusResponse?.Data?.Status == "PUBLISH_COMPLETE")
            {
                return new PublishResult
                {
                    Success = true,
                    PostId = publishId,
                    PostUrl = $"https://www.tiktok.com/@{statusResponse.Data.PublicPostId}"
                };
            }

            // Video is still processing
            return new PublishResult
            {
                Success = true,
                PostId = publishId,
                PostUrl = null // URL will be available after processing
            };
        }
        catch (Exception ex)
        {
            return new PublishResult
            {
                Success = false,
                Error = $"Status check failed: {ex.Message}"
            };
        }
    }

    #region PKCE Helpers

    /// <summary>
    /// Generates a cryptographically random code verifier for PKCE.
    /// Uses unreserved characters [A-Z] / [a-z] / [0-9] / "-" / "." / "_" / "~"
    /// with a length between 43 and 128 characters.
    /// </summary>
    private static string GenerateCodeVerifier()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        const int length = 64; // Use 64 characters (between 43 and 128)

        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }

        return new string(result);
    }

    /// <summary>
    /// Generates a code challenge from the code verifier using SHA256 hex encoding.
    /// TikTok requires hex encoding (not base64url) for the code challenge.
    /// </summary>
    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = Encoding.UTF8.GetBytes(codeVerifier);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    #endregion

    #region TikTok Response Models

    private class TikTokTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("open_id")]
        public string? OpenId { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    private class TikTokInitResponse
    {
        [JsonPropertyName("data")]
        public TikTokInitData? Data { get; set; }

        [JsonPropertyName("error")]
        public TikTokError? Error { get; set; }
    }

    private class TikTokInitData
    {
        [JsonPropertyName("publish_id")]
        public string? PublishId { get; set; }

        [JsonPropertyName("upload_url")]
        public string? UploadUrl { get; set; }
    }

    private class TikTokStatusResponse
    {
        [JsonPropertyName("data")]
        public TikTokStatusData? Data { get; set; }
    }

    private class TikTokStatusData
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("public_post_id")]
        public string? PublicPostId { get; set; }
    }

    private class TikTokError
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private class TikTokInitResult
    {
        public bool Success { get; set; }
        public string? UploadUrl { get; set; }
        public string? PublishId { get; set; }
        public string? Error { get; set; }
    }

    private class TikTokUploadResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    private class TikTokCreatorInfoResponse
    {
        [JsonPropertyName("data")]
        public TikTokCreatorInfoData? Data { get; set; }

        [JsonPropertyName("error")]
        public TikTokError? Error { get; set; }
    }

    private class TikTokCreatorInfoData
    {
        [JsonPropertyName("creator_avatar_url")]
        public string? CreatorAvatarUrl { get; set; }

        [JsonPropertyName("creator_username")]
        public string? CreatorUsername { get; set; }

        [JsonPropertyName("creator_nickname")]
        public string? CreatorNickname { get; set; }

        [JsonPropertyName("privacy_level_options")]
        public List<string>? PrivacyLevelOptions { get; set; }

        [JsonPropertyName("comment_disabled")]
        public bool CommentDisabled { get; set; }

        [JsonPropertyName("duet_disabled")]
        public bool DuetDisabled { get; set; }

        [JsonPropertyName("stitch_disabled")]
        public bool StitchDisabled { get; set; }

        [JsonPropertyName("max_video_post_duration_sec")]
        public int? MaxVideoPostDurationSec { get; set; }
    }

    private class TikTokCreatorInfoResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? CreatorAvatarUrl { get; set; }
        public string? CreatorUsername { get; set; }
        public string? CreatorNickname { get; set; }
        public List<string> PrivacyLevelOptions { get; set; } = ["SELF_ONLY"];
        public bool CommentDisabled { get; set; }
        public bool DuetDisabled { get; set; }
        public bool StitchDisabled { get; set; }
        public int MaxVideoDurationSeconds { get; set; } = 60;
    }

    #endregion
}
