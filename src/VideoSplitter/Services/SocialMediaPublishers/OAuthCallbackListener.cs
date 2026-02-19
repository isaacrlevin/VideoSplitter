using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;

namespace VideoSplitter.Services.SocialMediaPublishers;

/// <summary>
/// Handles OAuth callbacks by starting a local HTTP listener on an available port.
/// This is a reusable component for any social media publisher that requires OAuth authentication.
/// </summary>
public sealed class OAuthCallbackListener : IAsyncDisposable, IDisposable
{
    private HttpListener? _listener;
    private readonly string _callbackPath;
    private int _port;
    private bool _disposed;

    /// <summary>
    /// Gets the full redirect URI to use for OAuth authorization.
    /// Only valid after calling <see cref="StartAsync"/>.
    /// </summary>
    public string RedirectUri => $"http://localhost:{_port}{_callbackPath}";

    /// <summary>
    /// Gets the port number the listener is running on.
    /// Only valid after calling <see cref="StartAsync"/>.
    /// </summary>
    public int Port => _port;

    /// <summary>
    /// Gets whether the listener is currently active.
    /// </summary>
    public bool IsListening => _listener?.IsListening ?? false;

    /// <summary>
    /// Creates a new OAuth callback listener.
    /// </summary>
    /// <param name="callbackPath">The path portion of the callback URL (e.g., "/callback/" or "/oauth/tiktok/").</param>
    public OAuthCallbackListener(string callbackPath = "/callback/")
    {
        _callbackPath = NormalizePath(callbackPath);
    }

    /// <summary>
    /// Starts the HTTP listener on an available port.
    /// </summary>
    /// <param name="preferredPort">Optional preferred port. If unavailable or 0, a random available port will be used.</param>
    /// <returns>The port number the listener is running on.</returns>
    public Task<int> StartAsync(int preferredPort = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_listener?.IsListening == true)
        {
            return Task.FromResult(_port);
        }

        _port = preferredPort > 0 && IsPortAvailable(preferredPort)
            ? preferredPort
            : GetAvailablePort();

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}{_callbackPath}");

        try
        {
            _listener.Start();
        }
        catch (HttpListenerException ex)
        {
            throw new InvalidOperationException(
                $"Failed to start HTTP listener on port {_port}. " +
                "Ensure no other application is using this port and you have the necessary permissions.", ex);
        }

        return Task.FromResult(_port);
    }

    /// <summary>
    /// Waits for an OAuth callback and extracts the authorization parameters.
    /// </summary>
    /// <param name="expectedState">Optional state parameter to validate against CSRF attacks.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the wait operation.</param>
    /// <returns>The result of the OAuth callback.</returns>
    public async Task<OAuthCallbackResult> WaitForCallbackAsync(
        string? expectedState = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_listener is null || !_listener.IsListening)
        {
            throw new InvalidOperationException("Listener not started. Call StartAsync() first.");
        }

        try
        {
            var contextTask = _listener.GetContextAsync();
            var context = await contextTask.WaitAsync(cancellationToken);

            var request = context.Request;
            var response = context.Response;

            // Parse query parameters from the callback URL
            var query = HttpUtility.ParseQueryString(request.Url?.Query ?? string.Empty);

            var result = new OAuthCallbackResult
            {
                Code = query["code"],
                State = query["state"],
                Error = query["error"],
                ErrorDescription = query["error_description"],
                Scopes = query["scopes"]
            };

            // Validate state if provided
            if (!string.IsNullOrEmpty(expectedState) &&
                !string.Equals(result.State, expectedState, StringComparison.Ordinal))
            {
                result.Success = false;
                result.Error = "state_mismatch";
                result.ErrorDescription = "The state parameter does not match. This may indicate a CSRF attack.";
            }
            else
            {
                result.Success = string.IsNullOrEmpty(result.Error) && !string.IsNullOrEmpty(result.Code);
            }

            // Send response HTML to the browser
            await SendBrowserResponseAsync(response, result, cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            return new OAuthCallbackResult
            {
                Success = false,
                Error = "timeout",
                ErrorDescription = "The authentication request timed out. Please try again."
            };
        }
        catch (Exception ex)
        {
            return new OAuthCallbackResult
            {
                Success = false,
                Error = "listener_error",
                ErrorDescription = $"An error occurred while waiting for the callback: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Performs the complete OAuth flow: starts listener, opens browser, waits for callback.
    /// </summary>
    /// <param name="authorizationUrl">The OAuth authorization URL to open in the browser.</param>
    /// <param name="expectedState">Optional state parameter to validate against CSRF attacks.</param>
    /// <param name="timeout">Maximum time to wait for the callback. Defaults to 5 minutes.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the OAuth callback.</returns>
    public async Task<OAuthCallbackResult> AuthenticateAsync(
        string authorizationUrl,
        string? expectedState = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        timeout ??= TimeSpan.FromMinutes(5);

        try
        {
            // Ensure listener is started
            if (!IsListening)
            {
                await StartAsync();
            }

            // Open the authorization URL in the default browser
            var launched = await Launcher.Default.OpenAsync(new Uri(authorizationUrl));
            if (!launched)
            {
                return new OAuthCallbackResult
                {
                    Success = false,
                    Error = "browser_error",
                    ErrorDescription = "Failed to open the authentication page in your browser."
                };
            }

            // Wait for the callback with timeout
            using var timeoutCts = new CancellationTokenSource(timeout.Value);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            return await WaitForCallbackAsync(expectedState, linkedCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new OAuthCallbackResult
            {
                Success = false,
                Error = "timeout",
                ErrorDescription = "The authentication request timed out. Please try again."
            };
        }
    }

    /// <summary>
    /// Stops the HTTP listener.
    /// </summary>
    public void Stop()
    {
        if (_listener?.IsListening == true)
        {
            _listener.Stop();
        }
    }

    private async Task SendBrowserResponseAsync(
        HttpListenerResponse response,
        OAuthCallbackResult result,
        CancellationToken cancellationToken)
    {
        var html = result.Success
            ? GenerateSuccessHtml()
            : GenerateErrorHtml(result.ErrorDescription ?? result.Error ?? "Unknown error");

        var buffer = Encoding.UTF8.GetBytes(html);

        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.StatusCode = result.Success ? 200 : 400;

        try
        {
            await response.OutputStream.WriteAsync(buffer, cancellationToken);
        }
        finally
        {
            response.Close();
        }
    }

    private const string SuccessHtml = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Authentication Successful</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }
        .container { text-align: center; background: white; padding: 3rem; border-radius: 16px; box-shadow: 0 20px 60px rgba(0,0,0,0.3); max-width: 400px; }
        .icon { font-size: 4rem; margin-bottom: 1rem; }
        h1 { color: #22c55e; margin: 0 0 1rem 0; }
        p { color: #666; margin: 0; }
        .countdown { color: #999; font-size: 0.9rem; margin-top: 1rem; }
        .manual-close { display: none; color: #666; margin-top: 1rem; padding: 1rem; background: #f5f5f5; border-radius: 8px; }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon"">&#10004;</div>
        <h1>Authentication Successful!</h1>
        <p>You can return to the application.</p>
        <p class=""countdown"" id=""countdown"">This tab will close in <span id=""seconds"">5</span> seconds...</p>
        <p class=""manual-close"" id=""manual"">Please close this tab manually.</p>
    </div>
    <script>
        (function() {
            var seconds = 5;
            var countdownEl = document.getElementById('seconds');
            var manualEl = document.getElementById('manual');
            var countdownContainer = document.getElementById('countdown');
            
            var timer = setInterval(function() {
                seconds--;
                countdownEl.textContent = seconds;
                if (seconds <= 0) {
                    clearInterval(timer);
                    tryClose();
                }
            }, 1000);
            
            function tryClose() {
                // Attempt 1: Standard close
                window.close();
                
                // Attempt 2: Open self and close (works in some browsers)
                setTimeout(function() {
                    window.open('', '_self', '');
                    window.close();
                }, 100);
                
                // If still open after attempts, show manual message
                setTimeout(function() {
                    countdownContainer.style.display = 'none';
                    manualEl.style.display = 'block';
                }, 500);
            }
        })();
    </script>
</body>
</html>";

    private const string ErrorHtmlTemplate = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Authentication Failed</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); }
        .container { text-align: center; background: white; padding: 3rem; border-radius: 16px; box-shadow: 0 20px 60px rgba(0,0,0,0.3); max-width: 400px; }
        .icon { font-size: 4rem; margin-bottom: 1rem; }
        h1 { color: #ef4444; margin: 0 0 1rem 0; }
        p { color: #666; margin: 0; }
        .error { color: #999; font-size: 0.9rem; margin-top: 1rem; }
        .countdown { color: #999; font-size: 0.9rem; margin-top: 1rem; }
        .manual-close { display: none; color: #666; margin-top: 1rem; padding: 1rem; background: #f5f5f5; border-radius: 8px; }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon"">&#10008;</div>
        <h1>Authentication Failed</h1>
        <p>Something went wrong during authentication.</p>
        <p class=""error"">{{ERROR_MESSAGE}}</p>
        <p class=""countdown"" id=""countdown"">This tab will close in <span id=""seconds"">5</span> seconds...</p>
        <p class=""manual-close"" id=""manual"">Please close this tab manually and try again.</p>
    </div>
    <script>
        (function() {
            var seconds = 5;
            var countdownEl = document.getElementById('seconds');
            var manualEl = document.getElementById('manual');
            var countdownContainer = document.getElementById('countdown');
            
            var timer = setInterval(function() {
                seconds--;
                countdownEl.textContent = seconds;
                if (seconds <= 0) {
                    clearInterval(timer);
                    tryClose();
                }
            }, 1000);
            
            function tryClose() {
                // Attempt 1: Standard close
                window.close();
                
                // Attempt 2: Open self and close (works in some browsers)
                setTimeout(function() {
                    window.open('', '_self', '');
                    window.close();
                }, 100);
                
                // If still open after attempts, show manual message
                setTimeout(function() {
                    countdownContainer.style.display = 'none';
                    manualEl.style.display = 'block';
                }, 500);
            }
        })();
    </script>
</body>
</html>";

    private static string GenerateSuccessHtml() => SuccessHtml;

    private static string GenerateErrorHtml(string errorMessage) =>
        ErrorHtmlTemplate.Replace("{{ERROR_MESSAGE}}", HttpUtility.HtmlEncode(errorMessage));

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            path = "/callback/";

        if (!path.StartsWith('/'))
            path = "/" + path;

        if (!path.EndsWith('/'))
            path += "/";

        return path;
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _listener?.Stop();
        _listener?.Close();
        _listener = null;
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Represents the result of an OAuth callback.
/// </summary>
public sealed class OAuthCallbackResult
{
    /// <summary>
    /// Gets or sets whether the authentication was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the authorization code received from the OAuth provider.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets the state parameter returned by the OAuth provider.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the error code if authentication failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of the error.
    /// </summary>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// Gets or sets the granted scopes (comma-separated), if returned by the provider.
    /// </summary>
    public string? Scopes { get; set; }
}
