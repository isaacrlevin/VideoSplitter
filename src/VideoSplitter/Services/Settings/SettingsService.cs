using Microsoft.Extensions.AI;
using Mscc.GenerativeAI.Types;
using System.Net;
using System.Text.Json;
using VideoSplitter.Models;
using VideoSplitter.Models.LLM;
using VideoSplitter.Services.LlmProviders;

namespace VideoSplitter.Services;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<ValidationResult> ValidateAzureSpeechSettingsAsync(string apiKey, string region);
    Task<ValidationResult> ValidateOpenAiSettingsAsync(string apiKey, string modelName);
    Task<ValidationResult> ValidateAnthropicSettingsAsync(string apiKey);
    Task<ValidationResult> ValidateAzureOpenAiSettingsAsync(string apiKey, string endpoint, string? deploymentName = null);
    Task<ValidationResult> ValidateGoogleSettingsAsync(string apiKey, string modelName);
    Task<bool> IsOllamaRunningAsync();
    Task<IEnumerable<string>> GetOllamaModelsAsync();
    Task<bool> IsFFmpegAvailableAsync();
    Task<OpenAiModelsResult> GetOpenAiModelsAsync(string apiKey);
    Task<AnthropicModelsResult> GetAnthropicModelsAsync(string apiKey);
    Task<GoogleGeminiModelsResult> GetGoogleGeminiModelsAsync(string apiKey);
}


public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private AppSettings? _cachedSettings;
    private readonly LlmProviderFactory _llmProviderFactory;
    private readonly IPromptService _promptService;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "VideoSplitter");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
        _llmProviderFactory = new LlmProviderFactory(new HttpClient());
        _promptService = new PromptService();
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                _cachedSettings = new AppSettings();
            }
        }
        else
        {
            _cachedSettings = new AppSettings();
        }

        // Initialize prompts if they are empty
        if (string.IsNullOrWhiteSpace(_cachedSettings.SystemPrompt))
        {
            _cachedSettings.SystemPrompt = await _promptService.LoadDefaultSystemPromptAsync();
        }

        if (string.IsNullOrWhiteSpace(_cachedSettings.UserPrompt))
        {
            _cachedSettings.UserPrompt = await _promptService.LoadDefaultUserPromptAsync();
        }

        return _cachedSettings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        _cachedSettings = settings;
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsPath, json);
    }

    public async Task<OpenAiModelsResult> GetOpenAiModelsAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return OpenAiModelsResult.Failure("API key is required");

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var response = await httpClient.GetAsync("https://api.openai.com/v1/models");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return OpenAiModelsResult.Failure($"OpenAI API returned {response.StatusCode}: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var modelsResponse = JsonSerializer.Deserialize<OpenAiModelsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (modelsResponse?.Data == null)
                return OpenAiModelsResult.Failure("Failed to parse models response");

            // Filter to only include chat-capable models (gpt models, o1, o3, o4, etc.)
            var chatModels = modelsResponse.Data
                .Where(m => m.Id != null && IsChatModel(m.Id))
                .Select(m => m.Id!)
                .OrderByDescending(m => m) // Sort newest first (higher version numbers)
                .ToList();

            return OpenAiModelsResult.Ok(chatModels);
        }
        catch (Exception ex)
        {
            return OpenAiModelsResult.Failure($"Connection error: {ex.Message}");
        }
    }

    private static bool IsChatModel(string modelId)
    {
        // Include GPT models (gpt-3.5, gpt-4, gpt-4o, gpt-5, etc.)
        if (modelId.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
        {
            // Exclude non-chat models
            if (modelId.Contains("instruct", StringComparison.OrdinalIgnoreCase) ||
                modelId.Contains("embedding", StringComparison.OrdinalIgnoreCase) ||
                modelId.Contains("tts", StringComparison.OrdinalIgnoreCase) ||
                modelId.Contains("whisper", StringComparison.OrdinalIgnoreCase) ||
                modelId.Contains("dall-e", StringComparison.OrdinalIgnoreCase) ||
                modelId.Contains("image", StringComparison.OrdinalIgnoreCase) ||
                modelId.Contains("transcribe", StringComparison.OrdinalIgnoreCase) ||
                modelId.Contains("realtime", StringComparison.OrdinalIgnoreCase) ||
                modelId.Contains("audio", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        // Include o-series reasoning models (o1, o3, o4, etc.)
        if (modelId.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            modelId.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Include chatgpt models
        if (modelId.StartsWith("chatgpt-", StringComparison.OrdinalIgnoreCase) &&
            !modelId.Contains("image", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public async Task<AnthropicModelsResult> GetAnthropicModelsAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return AnthropicModelsResult.Failure("API key is required");

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var allModels = new List<AnthropicModelInfo>();
            string? afterId = null;
            bool hasMore = true;

            // Paginate through all models
            while (hasMore)
            {
                var url = "https://api.anthropic.com/v1/models";
                if (!string.IsNullOrEmpty(afterId))
                {
                    url += $"?after_id={afterId}";
                }

                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return AnthropicModelsResult.Failure($"Anthropic API returned {response.StatusCode}: {errorContent}");
                }

                var content = await response.Content.ReadAsStringAsync();
                var modelsResponse = JsonSerializer.Deserialize<AnthropicModelsResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (modelsResponse?.Data == null)
                    break;

                allModels.AddRange(modelsResponse.Data);
                hasMore = modelsResponse.Has_More;

                if (hasMore && modelsResponse.Data.Count > 0)
                {
                    afterId = modelsResponse.Data.Last().Id;
                }
                else
                {
                    hasMore = false;
                }
            }

            if (allModels.Count == 0)
                return AnthropicModelsResult.Failure("No models found");

            // Filter and sort models
            var chatModels = allModels
                .Where(m => m.Id != null && m.Type == "model")
                .Select(m => m.Id!)
                .OrderByDescending(m => m) // Sort newest first
                .ToList();

            return AnthropicModelsResult.Ok(chatModels);
        }
        catch (Exception ex)
        {
            return AnthropicModelsResult.Failure($"Connection error: {ex.Message}");
        }
    }

    public async Task<GoogleGeminiModelsResult> GetGoogleGeminiModelsAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return GoogleGeminiModelsResult.Failure("API key is required");

        try
        {
            using var httpClient = new HttpClient();

            var response = await httpClient.GetAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return GoogleGeminiModelsResult.Failure($"Google Gemini API returned {response.StatusCode}: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var modelsResponse = JsonSerializer.Deserialize<GoogleGeminiModelsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (modelsResponse?.Models == null)
                return GoogleGeminiModelsResult.Failure("Failed to parse models response");

            // Filter to only include models that support generateContent (chat-capable models)
            // Exclude embedding models, image generation models, video models, etc.
            var chatModels = modelsResponse.Models
                .Where(m => m.Name != null && 
                           m.SupportedGenerationMethods.Contains("generateContent") &&
                           IsGeminiChatModel(m.Name))
                .Select(m => m.Name!.Replace("models/", "")) // Remove "models/" prefix
                .OrderByDescending(m => m) // Sort newest first
                .ToList();

            return GoogleGeminiModelsResult.Ok(chatModels);
        }
        catch (Exception ex)
        {
            return GoogleGeminiModelsResult.Failure($"Connection error: {ex.Message}");
        }
    }

    private static bool IsGeminiChatModel(string modelName)
    {
        var name = modelName.ToLowerInvariant();
        
        // Exclude non-chat models
        if (name.Contains("embedding") ||
            name.Contains("imagen") ||
            name.Contains("veo") ||
            name.Contains("aqa") ||
            name.Contains("tts") ||
            name.Contains("native-audio") ||
            name.Contains("robotics") ||
            name.Contains("computer-use") ||
            name.Contains("-image"))
        {
            return false;
        }

        // Include Gemini and Gemma models
        if (name.Contains("gemini") || name.Contains("gemma"))
        {
            return true;
        }

        return false;
    }

    public async Task<ValidationResult> ValidateAzureSpeechSettingsAsync(string apiKey, string region)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(region))
            return ValidationResult.Failure("API key and region are required");

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            var response = await httpClient.GetAsync($"https://{region}.api.cognitive.microsoft.com/speechtotext/v3.0/models");
            
            if (response.IsSuccessStatusCode)
                return ValidationResult.Success();
            
            var errorContent = await response.Content.ReadAsStringAsync();
            return ValidationResult.Failure($"Azure Speech API returned {response.StatusCode}: {errorContent}");
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Connection error: {ex.Message}");
        }
    }

    public async Task<ValidationResult> ValidateOpenAiSettingsAsync(string apiKey, string modelName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return ValidationResult.Failure("API key is required");

        try
        {
            var testSettings = new AppSettings
            {
                OpenAi = new OpenAiSettings
                {
                    ApiKey = apiKey,
                    Model = modelName ?? "gpt-4.1-mini"
                }
            };
            var provider = _llmProviderFactory.GetProvider(LlmProvider.OpenAI);
            var chatClient = provider.GetChatClient(testSettings);

            if (chatClient == null)
                return ValidationResult.Failure("Failed to create OpenAI chat client");

            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(ChatRole.User, "Hi")
            };

            var options = new ChatOptions
            {
                MaxOutputTokens = 10
            };

            var response = await chatClient.GetResponseAsync(messages, options);
            
            if (response?.Messages?.Count > 0)
                return ValidationResult.Success();
            
            return ValidationResult.Failure("No response received from OpenAI");
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"OpenAI API error: {ex.Message}");
        }
    }

    public async Task<ValidationResult> ValidateAnthropicSettingsAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return ValidationResult.Failure("API key is required");

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var testContent = new StringContent(JsonSerializer.Serialize(new
            {
                model = "claude-3-haiku-20240307",
                max_tokens = 10,
                messages = new[] { new { role = "user", content = "Hi" } }
            }), System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://api.anthropic.com/v1/messages", testContent);
            
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                return ValidationResult.Success();
            
            var errorContent = await response.Content.ReadAsStringAsync();
            return ValidationResult.Failure($"Anthropic API returned {response.StatusCode}: {errorContent}");
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Connection error: {ex.Message}");
        }
    }

    public async Task<ValidationResult> ValidateAzureOpenAiSettingsAsync(string apiKey, string endpoint, string? deploymentName = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
            return ValidationResult.Failure("API key and endpoint are required");

        try
        {
            var testSettings = new AppSettings
            {
                AzureOpenAi = new AzureOpenAiSettings
                {
                    ApiKey = apiKey,
                    Endpoint = endpoint,
                    DeploymentName = deploymentName ?? "gpt-35-turbo"
                }
            };

            var provider = _llmProviderFactory.GetProvider(LlmProvider.AzureOpenAI);
            var chatClient = provider.GetChatClient(testSettings);

            if (chatClient == null)
                return ValidationResult.Failure("Failed to create Azure OpenAI chat client");

            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(ChatRole.User, "Hi")
            };

            var options = new ChatOptions
            {
                MaxOutputTokens = 10
            };

            var response = await chatClient.GetResponseAsync(messages, options);
            
            if (response?.Messages?.Count > 0)
                return ValidationResult.Success();
            
            return ValidationResult.Failure("No response received from Azure OpenAI");
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Azure OpenAI API error: {ex.Message}");
        }
    }

    public async Task<ValidationResult> ValidateGoogleSettingsAsync(string apiKey, string modelName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return ValidationResult.Failure("API key is required");

        try
        {
            var testSettings = new AppSettings
            {
                GoogleGemini = new GoogleGeminiSettings
                {
                    ApiKey = apiKey,
                    Model = modelName ?? "gemini-2.5-pro"
                }
            };

            var provider = _llmProviderFactory.GetProvider(LlmProvider.GoogleGemini);
            var chatClient = provider.GetChatClient(testSettings);

            if (chatClient == null)
                return ValidationResult.Failure("Failed to create Google Gemini chat client");

            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(ChatRole.User, "Hi")
            };

            var options = new ChatOptions
            {
                MaxOutputTokens = 10                 
            };

            var response = await chatClient.GetResponseAsync(messages, options);

            if (response?.Messages?.Count > 0)
                return ValidationResult.Success();

            return ValidationResult.Failure("No response received from Google Gemini");
        }
        catch (GeminiApiTimeoutException ex)
        {
            return ValidationResult.Failure($"Google Gemini API error: {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            return ValidationResult.Failure($"Google Gemini API error: {ex.Message}");
        }
        catch (GeminiApiException ex)
        {
            return ValidationResult.Failure($"Google Gemini API error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Google Gemini API error: {ex.Message}");
        }
    }

    public async Task<bool> IsOllamaRunningAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var response = await httpClient.GetAsync("http://localhost:11434/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetOllamaModelsAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var response = await httpClient.GetAsync("http://localhost:11434/api/tags");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                if (data.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
                {
                    return models.EnumerateArray()
                        .Where(m => m.TryGetProperty("name", out var name))
                        .Select(m => m.GetProperty("name").GetString()!)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToList();
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return new List<string>();
    }

    public async Task<bool> IsFFmpegAvailableAsync()
    {
        try
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = processInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 && !string.IsNullOrEmpty(output);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            // File not found error (FFMpeg not in PATH)
            return false;
        }
        catch
        {
            return false;
        }
    }
}