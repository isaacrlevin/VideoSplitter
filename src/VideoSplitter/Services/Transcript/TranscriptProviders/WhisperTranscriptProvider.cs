using System.Buffers.Binary;
using System.Text;
using VideoSplitter.Models;
using Whisper.net;

namespace VideoSplitter.Services.TranscriptProviders;

/// <summary>
/// Transcript provider using Whisper.NET for local speech-to-text transcription
/// </summary>
public class WhisperTranscriptProvider : TranscriptProviderBase, IDisposable
{
    private readonly HttpClient _httpClient;
    private WhisperFactory? _whisperFactory;
    private bool _disposed = false;
    private bool _isDownloading = false;
    private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);

    private readonly Dictionary<string, string> _modelUrls = new()
    {
        ["ggml-base.bin"] = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
        ["ggml-small.bin"] = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
        ["ggml-tiny.bin"] = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin"
    };

    public WhisperTranscriptProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override bool IsConfigured(AppSettings settings)
    {
        // Whisper doesn't need API keys, just check if model is available or downloadable
        return true;
    }

    public override async Task<(bool Success, string? TranscriptPath, string? Error)> GenerateTranscriptAsync(
        string audioPath,
        string transcriptPath,
        AppSettings settings,
        IProgress<string>? progress = null)
    {
        progress?.Report("Starting local Whisper.NET transcript generation...");

        // Check if Whisper model is available, initialize if needed
        var modelStatus = await GetStatusAsync();
        if (!modelStatus.IsAvailable)
        {
            return (false, null, "Whisper model not found. Please download and initialize the model in Settings.");
        }

        // Audio should already be extracted by AudioExtractionService
        if (!File.Exists(audioPath))
        {
            return (false, null, "Audio file not found. Audio extraction should be performed before transcription.");
        }

        progress?.Report("Generating transcript with Whisper.NET...");

        var whisperResult = await RunWhisperAsync(audioPath, transcriptPath, progress);
        return whisperResult;
    }

    public override async Task<(bool IsAvailable, bool IsDownloading, string Status)> GetStatusAsync()
    {
        var isAvailable = await IsWhisperModelAvailableAsync();

        string status;
        if (_isDownloading)
        {
            status = "Downloading Whisper model...";
        }
        else if (isAvailable)
        {
            status = _whisperFactory != null ? "Model loaded and ready" : "Model file available";
        }
        else
        {
            status = "Model not available - download required";
        }

        return (isAvailable, _isDownloading, status);
    }

    /// <summary>
    /// Downloads the Whisper model if it's not present
    /// </summary>
    public async Task<(bool Success, string? Error)> DownloadWhisperModelAsync(IProgress<string>? progress = null)
    {
        await _downloadSemaphore.WaitAsync();
        try
        {
            var modelPath = GetExpectedModelPath();

            if (File.Exists(modelPath))
            {
                progress?.Report("Whisper model already exists.");
                return (true, null);
            }

            _isDownloading = true;
            progress?.Report("Starting Whisper model download...");

            // Create directory if it doesn't exist
            var modelDirectory = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrEmpty(modelDirectory) && !Directory.Exists(modelDirectory))
            {
                Directory.CreateDirectory(modelDirectory);
            }

            // Download the base model (around 142MB)
            var modelFileName = "ggml-base.bin";
            var downloadUrl = _modelUrls[modelFileName];

            progress?.Report($"Downloading {modelFileName} from Hugging Face...");

            try
            {
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percentage = (downloadedBytes * 100) / totalBytes;
                        progress?.Report($"Downloading: {percentage}% ({FormatBytes(downloadedBytes)}/{FormatBytes(totalBytes)})");
                    }
                    else
                    {
                        progress?.Report($"Downloading: {FormatBytes(downloadedBytes)}");
                    }
                }

                progress?.Report("Download completed. Verifying model file...");

                // Verify the downloaded file
                if (File.Exists(modelPath) && new FileInfo(modelPath).Length > 1024 * 1024) // At least 1MB
                {
                    progress?.Report("Whisper model downloaded and verified successfully!");
                    return (true, null);
                }
                else
                {
                    return (false, "Downloaded model file appears to be corrupted or incomplete");
                }
            }
            catch (HttpRequestException ex)
            {
                return (false, $"Network error while downloading model: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                return (false, $"Download timeout: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to download Whisper model: {ex.Message}");
        }
        finally
        {
            _isDownloading = false;
            _downloadSemaphore.Release();
        }
    }

    /// <summary>
    /// Initializes and loads the Whisper model, downloading it if necessary
    /// </summary>
    public async Task<(bool Success, string? Error)> InitializeWhisperModelAsync(IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Checking Whisper model availability...");

            // Check if model is already loaded
            if (_whisperFactory != null)
            {
                progress?.Report("Whisper model is already loaded and ready.");
                return (true, null);
            }

            // Check if model file exists
            var modelAvailable = await IsWhisperModelAvailableAsync();
            if (!modelAvailable)
            {
                return (false, "Whisper model not found. Please download the model first.");
            }

            // Load the model
            progress?.Report("Loading Whisper model...");
            await GetWhisperFactoryAsync();

            progress?.Report("Whisper model loaded successfully!");
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to initialize Whisper model: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the Whisper model is available for local transcription
    /// </summary>
    public async Task<bool> IsWhisperModelAvailableAsync()
    {
        try
        {
            var modelPath = GetExpectedModelPath();
            if (!File.Exists(modelPath))
            {
                // Try alternative locations
                var alternativePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ggml-base.bin"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "whisper", "ggml-base.bin"),
                    "ggml-base.bin" // Current directory
                };

                foreach (var altPath in alternativePaths)
                {
                    if (File.Exists(altPath))
                    {
                        return true;
                    }
                }
                return false;
            }

            // Verify the file is not empty/corrupted
            var fileInfo = new FileInfo(modelPath);
            return fileInfo.Length > 1024 * 1024; // At least 1MB
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the expected path for the Whisper model file
    /// </summary>
    public string GetExpectedModelPath()
    {
        return Path.Combine(FileSystem.Current.AppDataDirectory, "ggml-base.bin");
    }

    private async Task<WhisperFactory> GetWhisperFactoryAsync()
    {
        if (_whisperFactory != null)
            return _whisperFactory;

        try
        {
            // Check for model in app data directory first
            var modelPath = Path.Combine(FileSystem.Current.AppDataDirectory, "ggml-base.bin");

            if (!File.Exists(modelPath))
            {
                // Try common model locations
                var alternativePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ggml-base.bin"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "whisper", "ggml-base.bin"),
                    "ggml-base.bin" // Current directory
                };

                foreach (var altPath in alternativePaths)
                {
                    if (File.Exists(altPath))
                    {
                        modelPath = altPath;
                        break;
                    }
                }

                if (!File.Exists(modelPath))
                {
                    throw new FileNotFoundException(
                        "Whisper model not found. Please use the Settings page to download the model. " +
                        $"Expected location: {Path.Combine(FileSystem.Current.AppDataDirectory, "ggml-base.bin")}");
                }
            }

            _whisperFactory = WhisperFactory.FromPath(modelPath);
            return _whisperFactory;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize Whisper factory: {ex.Message}", ex);
        }
    }


    private async Task<(bool Success, string? TranscriptPath, string? Error)> RunWhisperAsync(
        string audioPath,
        string transcriptPath,
        IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Initializing Whisper.NET...");

            var whisperFactory = await GetWhisperFactoryAsync();

            progress?.Report("Loading and converting audio file...");

            // Read WAV file and convert to float samples that Whisper expects
            var samples = await ReadWavFileAsFloatSamplesAsync(audioPath);
            
            progress?.Report($"Audio loaded: {samples.Length} samples, running Whisper.NET transcription...");

            var transcript = new StringBuilder();
            var srtContent = new StringBuilder();
            var subtitleIndex = 1;

            // Build processor with proper configuration for speech recognition
            using var whisperProcessor = whisperFactory.CreateBuilder()
                .WithLanguage("en")
                .Build();

            // Process the audio samples directly (not as a stream)
            await foreach (var segment in whisperProcessor.ProcessAsync(samples))
            {
                // Write to TXT format
                transcript.AppendLine($"[{segment.Start:hh\\:mm\\:ss} -> {segment.End:hh\\:mm\\:ss}] {segment.Text}");

                // Write to SRT format
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    srtContent.AppendLine(subtitleIndex.ToString());
                    srtContent.AppendLine($"{FormatSrtTimestamp(segment.Start)} --> {FormatSrtTimestamp(segment.End)}");
                    srtContent.AppendLine(segment.Text.Trim());
                    srtContent.AppendLine();
                    subtitleIndex++;
                }

                // Progress display
                var displayText = $"{segment.Start:hh\\:mm\\:ss} -> {segment.End:hh\\:mm\\:ss}";
                progress?.Report($"Transcribing: {displayText}");
            }

            // Write TXT transcript
            await File.WriteAllTextAsync(transcriptPath, transcript.ToString(), Encoding.UTF8);

            // Write SRT file alongside the TXT file
            var srtPath = Path.ChangeExtension(transcriptPath, ".srt");
            await File.WriteAllTextAsync(srtPath, srtContent.ToString(), Encoding.UTF8);

            progress?.Report("Transcript and SRT file generated successfully!");
            return (true, transcriptPath, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Whisper.NET transcription failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads a WAV file and converts it to float samples normalized to [-1, 1] range.
    /// Whisper expects 16kHz mono float samples.
    /// </summary>
    private static async Task<float[]> ReadWavFileAsFloatSamplesAsync(string wavPath)
    {
        var fileBytes = await File.ReadAllBytesAsync(wavPath);
        
        // Parse WAV header to find data chunk
        // WAV format: RIFF header (12 bytes) + fmt chunk + data chunk
        
        if (fileBytes.Length < 44)
            throw new InvalidOperationException("WAV file is too small to be valid");

        // Verify RIFF header
        if (fileBytes[0] != 'R' || fileBytes[1] != 'I' || fileBytes[2] != 'F' || fileBytes[3] != 'F')
            throw new InvalidOperationException("Not a valid WAV file (missing RIFF header)");

        if (fileBytes[8] != 'W' || fileBytes[9] != 'A' || fileBytes[10] != 'V' || fileBytes[11] != 'E')
            throw new InvalidOperationException("Not a valid WAV file (missing WAVE format)");

        // Find the data chunk by scanning through chunks
        int position = 12; // Start after RIFF header
        int dataStart = -1;
        int dataSize = -1;

        while (position < fileBytes.Length - 8)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(fileBytes, position, 4);
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(fileBytes.AsSpan(position + 4));

            if (chunkId == "data")
            {
                dataStart = position + 8;
                dataSize = chunkSize;
                break;
            }

            // Move to next chunk (chunk header is 8 bytes + chunk data)
            position += 8 + chunkSize;
            
            // Handle odd chunk sizes (chunks are word-aligned)
            if (chunkSize % 2 == 1 && position < fileBytes.Length)
                position++;
        }

        if (dataStart < 0 || dataSize < 0)
            throw new InvalidOperationException("Could not find data chunk in WAV file");

        // Ensure we don't read beyond the file
        if (dataStart + dataSize > fileBytes.Length)
            dataSize = fileBytes.Length - dataStart;

        // Convert 16-bit PCM samples to float [-1, 1]
        // Each sample is 2 bytes (16-bit)
        var sampleCount = dataSize / 2;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            var sampleIndex = dataStart + (i * 2);
            if (sampleIndex + 1 < fileBytes.Length)
            {
                var sample16 = BinaryPrimitives.ReadInt16LittleEndian(fileBytes.AsSpan(sampleIndex));
                samples[i] = sample16 / 32768f; // Normalize to [-1, 1]
            }
        }

        return samples;
    }



    /// <summary>
    /// Formats a TimeSpan as an SRT timestamp (HH:MM:SS,mmm).
    /// </summary>
    private static string FormatSrtTimestamp(TimeSpan time)
    {
        return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _whisperFactory?.Dispose();
            _downloadSemaphore?.Dispose();
            _disposed = true;
        }
    }
}
