using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using VideoSplitter.Models;

namespace VideoSplitter.Services.LlmProviders;

/// <summary>
/// Interface for LLM providers that can generate video segments from transcripts
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Generate video segments from a transcript using the LLM
    /// </summary>
    Task<(bool Success, IEnumerable<Segment>? Segments, string? Error)> GenerateSegmentsAsync(
        Project project,
        string transcriptContent,
        AppSettings settings,
        IProgress<string>? progress = null);

    /// <summary>
    /// Indicates whether this provider is configured and ready to use
    /// </summary>
    bool IsConfigured(AppSettings settings);

    /// <summary>
    /// Gets the configured chat client for this provider
    /// </summary>
    IChatClient? GetChatClient(AppSettings settings);
}

/// <summary>
/// Base class providing common functionality for all LLM providers using Microsoft.Extensions.AI
/// </summary>
public abstract class LlmProviderBase : ILlmProvider
{
    public abstract IChatClient? GetChatClient(AppSettings settings);
    public abstract bool IsConfigured(AppSettings settings);

    // Add this property to get the provider name
    protected abstract string ProviderName { get; }

    public virtual async Task<(bool Success, IEnumerable<Segment>? Segments, string? Error)> GenerateSegmentsAsync(
        Project project,
        string transcriptContent,
        AppSettings settings,
        IProgress<string>? progress = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(transcriptContent))
            {
                return (false, null, "Transcript content is empty");
            }

            if (!IsConfigured(settings))
            {
                return (false, null, $"{GetType().Name} is not properly configured");
            }

            var chatClient = GetChatClient(settings);
            if (chatClient == null)
            {
                return (false, null, "Failed to create chat client");
            }

            progress?.Report($"[{ProviderName}] Analyzing transcript for segment generation...");

            var systemPrompt = CreateSystemPrompt(settings);
            var userPrompt = CreateUserPrompt(transcriptContent, settings);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            progress?.Report($"[{ProviderName}] Sending request to AI provider...");

            var chatOptions = new ChatOptions
            {
                Temperature = 0.1f,
                //Temperature = 0.0f,
                TopP = 0.9f,
                MaxOutputTokens = 4000                
            };


            var response = await chatClient.GetResponseAsync(messages, chatOptions);
            var aiResponse = response.Messages[0].Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return (false, null, "Empty response from AI provider");
            }

            progress?.Report($"[{ProviderName}] Processing AI response...");

            aiResponse = CleanAiResponse(aiResponse);
            aiResponse = ExtractJsonArray(aiResponse, progress);

            return ParseSegmentsFromAiResponse(project, aiResponse, transcriptContent, settings);
        }
        catch (Exception ex)
        {
            return (false, null, $"Segment generation failed: {ex.Message}");
        }
    }

    protected string CreateSystemPrompt(AppSettings settings)
    {
        var prompt = settings.SystemPrompt ?? string.Empty;

        // Replace placeholders
        prompt = prompt.Replace("{segmentCount}", settings.DefaultSegmentCount.ToString());
        prompt = prompt.Replace("{segmentLength}", settings.DefaultSegmentLengthSeconds.ToString());

        return prompt;
    }

    protected string CreateUserPrompt(string transcriptContent, AppSettings settings)
    {
        var prompt = settings.UserPrompt ?? string.Empty;

        // Replace placeholders
        prompt = prompt.Replace("{segmentCount}", settings.DefaultSegmentCount.ToString());
        prompt = prompt.Replace("{segmentLength}", settings.DefaultSegmentLengthSeconds.ToString());
        prompt = prompt.Replace("{transcript}", transcriptContent);

        // For local models (Ollama), add extra JSON reinforcement
        if (settings.LlmProvider == LlmProvider.Local)
        {
            var jsonFormat = "\"{Start\": \"00:00:30\", \"End\": \"00:01:15\", \"Duration\": 45, \"Reasoning\": \"reason here\", \"Excerpt\": \"text here\"}";
            //prompt = $"""
            //    {prompt}                

            //    REMEMBER: Respond with ONLY a JSON array. Example:
            //    {jsonFormat}

            //    Your JSON response:
            //    """;
            prompt = $"""
            {prompt}

            CRITICAL: You MUST return EXACTLY {settings.DefaultSegmentCount} segments.
            Your response MUST be a JSON ARRAY starting with [ and ending with ]
            
            Example of correct format with {settings.DefaultSegmentCount} segments:
            [
              {jsonFormat},
              {jsonFormat},
              {jsonFormat},
            ]
            
            START YOUR RESPONSE WITH [ CHARACTER:
            """;
        }

        return prompt;
    }

    protected string CreateSegmentationPrompt(string transcriptContent, AppSettings settings)
    {
        // This method is now deprecated but kept for backward compatibility
        return CreateSystemPrompt(settings);
    }

    protected (bool Success, IEnumerable<Segment>? Segments, string? Error) ParseSegmentsFromAiResponse(
        Project project,
        string aiResponse,
        string fullTranscript,
        AppSettings settings)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"AI Response (first 500 chars): {aiResponse?.Substring(0, Math.Min(500, aiResponse?.Length ?? 0))}");

            var jsonStart = aiResponse.IndexOf('[');
            var jsonEnd = aiResponse.LastIndexOf(']');

            if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
            {
                System.Diagnostics.Debug.WriteLine("No valid JSON array found in response, using fallback");
                return ParseSegmentsSimple(project, aiResponse, fullTranscript, settings);
            }

            var jsonContent = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var totalDuration = project.Duration?.TotalSeconds ?? 300;

            // Try to deserialize as array of AiSegmentResponse objects (strongly-typed)
            try
            {
                var segmentResponses = JsonSerializer.Deserialize<List<AiSegmentData>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    AllowTrailingCommas = true
                });

                if (segmentResponses != null && segmentResponses.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully deserialized {segmentResponses.Count} segment responses");

                    var parsedSegments = new List<Segment>();

                    //foreach (var response in segmentResponses)
                    //{
                    // Extract all non-null segments from the response
                    //var aiSegments = new[] 
                    //{ 
                    //    response.Segment1, 
                    //    response.Segment2, 
                    //    response.Segment3, 
                    //    response.Segment4, 
                    //    response.Segment5 
                    //}.Where(s => s != null).ToList();

                    foreach (var aiSegment in segmentResponses)
                    {
                        if (aiSegment == null) continue;

                        // Parse start and end times
                        if (!TimeSpan.TryParse(aiSegment.Start, out var startTime))
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to parse start time: {aiSegment.Start}");
                            continue;
                        }

                        if (!TimeSpan.TryParse(aiSegment.End, out var endTime))
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to parse end time: {aiSegment.End}");
                            continue;
                        }

                        // Validate segment doesn't exceed video duration
                        if (startTime.TotalSeconds >= totalDuration)
                        {
                            System.Diagnostics.Debug.WriteLine($"Segment start time {startTime} exceeds video duration {totalDuration}s");
                            continue;
                        }

                        endTime = TimeSpan.FromSeconds(Math.Min(endTime.TotalSeconds, totalDuration));

                        var segment = new Segment
                        {
                            ProjectId = project.Id,
                            StartTime = startTime,
                            EndTime = endTime,
                            TranscriptText = aiSegment.Excerpt ?? "",
                            Summary = $"{aiSegment.Excerpt?.Substring(0, Math.Min(50, aiSegment.Excerpt?.Length ?? 0))}...",
                            Reasoning = aiSegment.Reasoning ?? "",
                            Status = SegmentStatus.Generated
                        };

                        parsedSegments.Add(segment);
                    }
                    //}

                    if (parsedSegments.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Successfully created {parsedSegments.Count} segments from strongly-typed deserialization");
                        return (true, parsedSegments, null);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No valid segments extracted from strongly-typed response, using fallback");
                    }
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Strongly-typed deserialization failed: {ex.Message}, trying JsonElement fallback");
            }

            // Fallback to JsonElement parsing
            JsonElement[] segmentData;
            try
            {
                segmentData = JsonSerializer.Deserialize<JsonElement[]>(jsonContent) ?? Array.Empty<JsonElement>();
                System.Diagnostics.Debug.WriteLine($"Successfully parsed {segmentData.Length} segments from JSON");
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON parsing failed: {ex.Message}, using fallback");
                return ParseSegmentsSimple(project, aiResponse, fullTranscript, settings);
            }

            var segments = new List<Segment>();
            var segmentLengthSeconds = settings.DefaultSegmentLengthSeconds;

            var availableStartTimes = CalculateOptimalStartTimes(totalDuration, segmentLengthSeconds, segmentData.Length);

            for (int i = 0; i < segmentData.Length && i < availableStartTimes.Count; i++)
            {
                var data = segmentData[i];

                var summary = data.TryGetProperty("summary", out var summaryProp) ? summaryProp.GetString() : $"Segment {i + 1}";
                var reasoning = data.TryGetProperty("reasoning", out var reasoningProp) ? reasoningProp.GetString() : "";
                var transcriptText = data.TryGetProperty("transcriptText", out var transcriptProp) ? transcriptProp.GetString() : "";

                if (string.IsNullOrWhiteSpace(transcriptText))
                {
                    transcriptText = ExtractTranscriptForTimeRange(fullTranscript, availableStartTimes[i], segmentLengthSeconds, totalDuration);
                }

                var startTime = TimeSpan.FromSeconds(availableStartTimes[i]);
                var endTime = TimeSpan.FromSeconds(Math.Min(availableStartTimes[i] + segmentLengthSeconds, totalDuration));

                var segment = new Segment
                {
                    ProjectId = project.Id,
                    StartTime = startTime,
                    EndTime = endTime,
                    TranscriptText = transcriptText ?? "",
                    Summary = summary ?? $"Segment {i + 1}",
                    Reasoning = reasoning ?? "",
                    Status = SegmentStatus.Generated
                };

                segments.Add(segment);
            }

            return (true, segments, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Parsing exception: {ex.Message}, using fallback");
            return ParseSegmentsSimple(project, aiResponse, fullTranscript, settings);
        }
    }

    protected (bool Success, IEnumerable<Segment>? Segments, string? Error) ParseSegmentsSimple(
        Project project,
        string aiResponse,
        string fullTranscript,
        AppSettings settings)
    {
        try
        {
            var segments = new List<Segment>();
            var segmentCount = settings.DefaultSegmentCount;
            var totalDuration = project.Duration?.TotalSeconds ?? 300;
            var segmentLengthSeconds = settings.DefaultSegmentLengthSeconds;

            var startTimes = CalculateOptimalStartTimes(totalDuration, segmentLengthSeconds, segmentCount);

            for (int i = 0; i < segmentCount && i < startTimes.Count; i++)
            {
                var segmentText = ExtractTranscriptForTimeRange(fullTranscript, startTimes[i], segmentLengthSeconds, totalDuration);

                var startTime = TimeSpan.FromSeconds(startTimes[i]);
                var endTime = TimeSpan.FromSeconds(Math.Min(startTimes[i] + segmentLengthSeconds, totalDuration));

                var segment = new Segment
                {
                    ProjectId = project.Id,
                    StartTime = startTime,
                    EndTime = endTime,
                    TranscriptText = segmentText,
                    Summary = $"Segment {i + 1}: {segmentText.Substring(0, Math.Min(50, segmentText.Length))}...",
                    Reasoning = "Auto-generated segment distributed across video duration from preserved transcript content",
                    Status = SegmentStatus.Generated
                };

                segments.Add(segment);
            }

            return (true, segments, null);
        }
        catch (Exception ex)
        {
            return (false, null, $"Simple parsing failed: {ex.Message}");
        }
    }

    protected List<double> CalculateOptimalStartTimes(double totalDurationSeconds, int segmentLengthSeconds, int segmentCount)
    {
        var startTimes = new List<double>();
        var totalSegmentTime = segmentCount * segmentLengthSeconds;

        if (totalSegmentTime <= totalDurationSeconds)
        {
            var spacing = (totalDurationSeconds - segmentLengthSeconds) / Math.Max(1, segmentCount - 1);

            for (int i = 0; i < segmentCount; i++)
            {
                var startTime = i * spacing;
                startTime = Math.Min(startTime, totalDurationSeconds - segmentLengthSeconds);
                startTimes.Add(startTime);
            }
        }
        else
        {
            var maxPossibleSpacing = Math.Max(segmentLengthSeconds * 0.1, (totalDurationSeconds - segmentLengthSeconds) / Math.Max(1, segmentCount - 1));

            for (int i = 0; i < segmentCount; i++)
            {
                var startTime = i * maxPossibleSpacing;
                startTime = Math.Min(startTime, totalDurationSeconds - segmentLengthSeconds);
                startTimes.Add(startTime);
            }
        }

        return startTimes;
    }

    protected string ExtractTranscriptForTimeRange(string fullTranscript, double startTimeSeconds, int segmentLengthSeconds, double totalDurationSeconds)
    {
        try
        {
            var words = fullTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var totalWords = words.Length;

            var startRatio = startTimeSeconds / totalDurationSeconds;
            var endRatio = Math.Min((startTimeSeconds + segmentLengthSeconds) / totalDurationSeconds, 1.0);

            var startWordIndex = (int)(startRatio * totalWords);
            var endWordIndex = (int)(endRatio * totalWords);

            startWordIndex = Math.Max(0, Math.Min(startWordIndex, totalWords - 1));
            endWordIndex = Math.Max(startWordIndex + 10, Math.Min(endWordIndex, totalWords));

            var segmentWords = words.Skip(startWordIndex).Take(endWordIndex - startWordIndex);
            return string.Join(" ", segmentWords);
        }
        catch
        {
            var words = fullTranscript.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var wordsPerSegment = Math.Max(50, words.Length / 5);
            var segmentIndex = (int)(startTimeSeconds / segmentLengthSeconds);
            var startWord = Math.Min(segmentIndex * wordsPerSegment, words.Length - 50);
            return string.Join(" ", words.Skip(startWord).Take(Math.Min(wordsPerSegment, words.Length - startWord)));
        }
    }

    protected string CleanAiResponse(string aiResponse)
    {
        if (string.IsNullOrWhiteSpace(aiResponse))
            return aiResponse;

        aiResponse = aiResponse.Trim();

        while (aiResponse.Contains("<think>"))
        {
            var thinkStart = aiResponse.IndexOf("<think>");
            var thinkEnd = aiResponse.IndexOf("</think>", thinkStart);
            if (thinkStart >= 0 && thinkEnd > thinkStart)
            {
                aiResponse = aiResponse.Remove(thinkStart, thinkEnd - thinkStart + 8);
                aiResponse = aiResponse.Trim();
            }
            else
            {
                break;
            }
        }

        if (aiResponse.StartsWith("```"))
        {
            var lines = aiResponse.Split('\n');
            aiResponse = string.Join('\n', lines.Skip(1).Reverse().Skip(1).Reverse());
            aiResponse = aiResponse.Trim();
        }

        return aiResponse;
    }

    protected string ExtractJsonArray(string aiResponse, IProgress<string>? progress)
    {
        // Trim whitespace first
        aiResponse = aiResponse.Trim();

        // Check if response is a single object (starts with { but not [)
        // This handles Llama models that return one object instead of an array
        if (aiResponse.StartsWith("{") && !aiResponse.TrimStart().StartsWith("["))
        {
            System.Diagnostics.Debug.WriteLine("Response is a single JSON object, wrapping in array");
            progress?.Report("Note: Model returned single segment, wrapping in array...");
            aiResponse = "[" + aiResponse + "]";
        }

        var arrayStart = aiResponse.IndexOf('[');
        var arrayEnd = aiResponse.LastIndexOf(']');

        if (arrayStart >= 0 && arrayEnd > arrayStart)
        {
            if (aiResponse.Contains("\"responses\"") ||
                aiResponse.Contains("\"content\"") ||
                aiResponse.Contains("\"question_"))
            {
                try
                {
                    var wrapped = JsonSerializer.Deserialize<JsonElement>(aiResponse);

                    if (wrapped.TryGetProperty("responses", out var responsesArray) &&
                        responsesArray.ValueKind == JsonValueKind.Array)
                    {
                        if (responsesArray.GetArrayLength() > 0)
                        {
                            var firstResponse = responsesArray[0];
                            if (firstResponse.TryGetProperty("content", out var contentProp))
                            {
                                var extractedContent = contentProp.GetString();
                                if (!string.IsNullOrWhiteSpace(extractedContent))
                                {
                                    var contentArrayStart = extractedContent.IndexOf('[');
                                    var contentArrayEnd = extractedContent.LastIndexOf(']');
                                    if (contentArrayStart >= 0 && contentArrayEnd > contentArrayStart)
                                    {
                                        return extractedContent.Substring(contentArrayStart,
                                            contentArrayEnd - contentArrayStart + 1);
                                    }
                                }
                            }
                        }
                    }
                    else if (aiResponse.Contains("\"question_") && aiResponse.Contains("\"answer_"))
                    {
                        progress?.Report("Model returned Q&A format instead of segments");
                        throw new InvalidOperationException("Model misunderstood the task. Please try again or use a different model.");
                    }
                }
                catch (JsonException)
                {
                }
            }

            arrayStart = aiResponse.IndexOf('[');
            arrayEnd = aiResponse.LastIndexOf(']');
            if (arrayStart >= 0 && arrayEnd > arrayStart)
            {
                aiResponse = aiResponse.Substring(arrayStart, arrayEnd - arrayStart + 1);
            }
        }

        return aiResponse;
    }
}
