using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Text;
using VideoSplitter.Models;

namespace VideoSplitter.Services.TranscriptProviders;

/// <summary>
/// Transcript provider using Azure AI Speech Services for cloud-based speech-to-text transcription
/// </summary>
public class AzureSpeechProvider : TranscriptProviderBase
{
    public override bool IsConfigured(AppSettings settings)
    {
        return !string.IsNullOrEmpty(settings.AzureSpeech.AzureSpeechApiKey) &&
               !string.IsNullOrEmpty(settings.AzureSpeech.AzureSpeechRegion);
    }

    public override async Task<(bool Success, string? TranscriptPath, string? Error)> GenerateTranscriptAsync(
        string audioPath,
        string transcriptPath,
        AppSettings settings,
        IProgress<string>? progress = null)
    {
        if (!IsConfigured(settings))
        {
            return (false, null, "Azure Speech API key and region are required");
        }

        progress?.Report("Starting Azure Speech transcription...");

        // Audio should already be extracted by AudioExtractionService
        if (!File.Exists(audioPath))
        {
            return (false, null, "Audio file not found. Audio extraction should be performed before transcription.");
        }

        progress?.Report("Audio file ready. Starting transcription...");

        // Perform Azure Speech transcription
        var transcriptionResult = await TranscribeAudioAsync(
            audioPath, 
            settings.AzureSpeech.AzureSpeechApiKey!, 
            settings.AzureSpeech.AzureSpeechRegion!, 
            progress);

        if (!transcriptionResult.Success)
        {
            return (false, null, transcriptionResult.Error);
        }

        // Write transcript to file
        await File.WriteAllTextAsync(transcriptPath, transcriptionResult.Transcript, Encoding.UTF8);

        progress?.Report("Transcription completed successfully!");
        return (true, transcriptPath, null);
    }

    public override async Task<(bool IsAvailable, bool IsDownloading, string Status)> GetStatusAsync()
    {
        // Azure Speech is always available if configured (cloud service)
        await Task.CompletedTask;
        return (true, false, "Azure Speech Service (cloud-based)");
    }

    /// <summary>
    /// Transcribe audio file using Azure Speech Service with continuous recognition
    /// </summary>
    private async Task<(bool Success, string? Transcript, string? Error)> TranscribeAudioAsync(
        string audioFilePath,
        string apiKey,
        string region,
        IProgress<string>? progress = null)
    {
        try
        {
            // Configure Azure Speech
            var speechConfig = SpeechConfig.FromSubscription(apiKey, region);
            speechConfig.SpeechRecognitionLanguage = "en-US";
            
            // Enable detailed results for timestamps
            speechConfig.OutputFormat = OutputFormat.Detailed;

            using var audioConfig = AudioConfig.FromWavFileInput(audioFilePath);
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

            var transcript = new StringBuilder();
            var recognitionComplete = new TaskCompletionSource<bool>();
            var hasError = false;
            var errorMessage = string.Empty;

            // Subscribe to events for continuous recognition
            recognizer.Recognizing += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizingSpeech)
                {
                    progress?.Report($"Recognizing: {e.Result.Text}");
                }
            };

            recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrWhiteSpace(e.Result.Text))
                {
                    // Get offset and duration for timestamps
                    var offset = e.Result.OffsetInTicks;
                    var duration = e.Result.Duration;
                    
                    var startTime = TimeSpan.FromTicks(offset);
                    var endTime = startTime + duration;
                    
                    // Format with timestamps similar to Whisper output
                    transcript.AppendLine($"[{startTime:hh\\:mm\\:ss} -> {endTime:hh\\:mm\\:ss}] {e.Result.Text}");
                    
                    progress?.Report($"Transcribed: {startTime:hh\\:mm\\:ss} -> {endTime:hh\\:mm\\:ss}");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    progress?.Report("No speech detected in segment");
                }
            };

            recognizer.Canceled += (s, e) =>
            {
                if (e.Reason == CancellationReason.Error)
                {
                    hasError = true;
                    errorMessage = $"Recognition canceled: {e.ErrorCode}. Details: {e.ErrorDetails}";
                    progress?.Report(errorMessage);
                }
                else if (e.Reason == CancellationReason.EndOfStream)
                {
                    progress?.Report("End of audio stream reached");
                }
                
                recognitionComplete.TrySetResult(true);
            };

            recognizer.SessionStopped += (s, e) =>
            {
                progress?.Report("Transcription session stopped");
                recognitionComplete.TrySetResult(true);
            };

            // Start continuous recognition
            progress?.Report("Starting continuous recognition...");
            await recognizer.StartContinuousRecognitionAsync();

            // Wait for recognition to complete
            await recognitionComplete.Task;

            // Stop recognition
            await recognizer.StopContinuousRecognitionAsync();

            if (hasError)
            {
                return (false, null, errorMessage);
            }

            var finalTranscript = transcript.ToString();
            
            if (string.IsNullOrWhiteSpace(finalTranscript))
            {
                return (false, null, "No speech recognized in the audio file");
            }

            // Add header information
            var transcriptWithHeader = $@"[AZURE SPEECH TRANSCRIPT]
Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Language: en-US
Provider: Azure AI Speech Service
Region: {region}

{finalTranscript}

[END OF TRANSCRIPT]";

            return (true, transcriptWithHeader, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Azure Speech transcription error: {ex.Message}");
        }
    }
}
