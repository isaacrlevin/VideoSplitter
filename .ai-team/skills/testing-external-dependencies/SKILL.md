---
name: "testing-external-dependencies"
description: "Strategies for testing applications with expensive or complex external dependencies (APIs, ML models, media processing tools)"
domain: "testing"
confidence: "medium"
source: "earned"
---

## Context

Applications that integrate with external systems face unique testing challenges:
- **External APIs** may cost money per call, have rate limits, or require authentication
- **ML models** may require large downloads or expensive compute
- **Media processing tools** (FFmpeg, video/audio encoders) may be slow
- **Third-party services** may have unreliable uptime or limited sandbox environments

Traditional "test everything with real dependencies" approaches are impractical. A multi-tiered strategy balances coverage, speed, and cost.

## Patterns

### 1. Three-Tier Test Architecture

**Unit Tests** - Fast, isolated, no external dependencies
- Mock all external services using interfaces
- Test business logic, validation, error handling
- Run on every commit (< 30 seconds total)
- Target: 80%+ coverage of core logic

**Integration Tests** - Real dependencies, controlled scope
- Use real database (SQLite in-memory or temp files)
- Use real tools with small inputs (FFmpeg on 5-second videos)
- Mock expensive/unreliable external APIs (LLMs, payment gateways)
- Tagged for selective execution: `[Trait("Category", "Integration")]`
- Run on PR validation

**E2E Tests** - Full workflows, optional external services
- Test critical user journeys end-to-end
- Use fake providers or mocked APIs to avoid costs
- Run nightly or on-demand
- Tagged: `[Trait("Category", "E2E")]`, `[Trait("Speed", "Slow")]`

### 2. Test Trait Taxonomy for Selective Execution

Use xUnit traits to enable targeted test runs:

```csharp
[Trait("Category", "Unit")]           // Fast, always run
[Trait("Category", "Integration")]    // Requires local resources
[Trait("Speed", "Slow")]              // Takes >5 seconds
[Trait("External", "True")]           // Calls external APIs
[Trait("RequiresAuth", "True")]       // Needs API keys/credentials
[Trait("Expensive", "True")]          // Costs money to run
```

CI pipeline examples:
```bash
# Local dev: fast feedback
dotnet test --filter "Category=Unit"

# PR validation: thorough but free
dotnet test --filter "Category=Unit|Category=Integration" --filter "External!=True"

# Nightly: full coverage with real APIs
dotnet test
```

### 3. Interface Extraction for Testability

Wrap external dependencies behind interfaces for mocking:

```csharp
// Before: Hard to test
public class AudioService
{
    public async Task ExtractAudio(string videoPath)
    {
        await FFMpegArguments.FromFileInput(videoPath)...  // Direct FFmpeg call
    }
}

// After: Testable
public interface IFFmpegService
{
    Task<bool> ExtractAudioAsync(string input, string output);
}

public class AudioService
{
    private readonly IFFmpegService _ffmpeg;
    
    public AudioService(IFFmpegService ffmpeg)
    {
        _ffmpeg = ffmpeg;
    }
    
    public async Task ExtractAudio(string videoPath)
    {
        await _ffmpeg.ExtractAudioAsync(videoPath, outputPath);
    }
}

// Unit test with mock
var mockFFmpeg = new Mock<IFFmpegService>();
mockFFmpeg.Setup(x => x.ExtractAudioAsync(It.IsAny<string>(), It.IsAny<string>()))
    .ReturnsAsync(true);

// Integration test with real FFmpeg
var realFFmpeg = new FFmpegService();  // Wraps FFMpegCore
```

### 4. Fake Providers for Predictable Testing

Create simplified "fake" implementations for complex providers:

```csharp
public class FakeLlmProvider : ILlmProvider
{
    private readonly List<Segment> _predefinedSegments;
    
    public FakeLlmProvider(List<Segment>? segments = null)
    {
        _predefinedSegments = segments ?? new List<Segment>
        {
            new Segment { StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(30) }
        };
    }
    
    public Task<(bool Success, IEnumerable<Segment>? Segments, string? Error)> 
        GenerateSegmentsAsync(...)
    {
        // Return predictable results for E2E testing
        return Task.FromResult<(bool, IEnumerable<Segment>?, string?)>(
            (true, _predefinedSegments, null));
    }
}
```

Benefits:
- Deterministic test results (no AI variance)
- No API costs
- Fast execution
- Can simulate error conditions

### 5. Test Data Management

For media processing applications:

**Small Sample Files** - Check into repository (or Git LFS)
- `short-test.mp4` (5 seconds) - for fast integration tests
- `tiny-audio.wav` (1 second) - for unit tests with real file I/O
- `sample-transcript.json` - known-good output for validation

**Programmatic Generation** - Create test data in code
```csharp
public static class TestData
{
    public static byte[] CreateSilentWavFile(TimeSpan duration)
    {
        // Generate minimal valid WAV file for testing
    }
    
    public static Project CreateTestProject(string name = "Test") =>
        new Project { Name = name, /* sensible defaults */ };
}
```

**Snapshot Testing** - For complex outputs (Verify library)
```csharp
[Fact]
public async Task GenerateSegments_ProducesExpectedStructure()
{
    var result = await service.GenerateSegmentsAsync(...);
    
    // Verify JSON structure matches snapshot
    await Verify(result.Segments);
}
```

### 6. Environment-Based Test Configuration

Use environment variables for optional integration tests:

```csharp
public class OpenAiIntegrationTests
{
    [Fact]
    public async Task CallRealOpenAI_ReturnsValidResponse()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        if (string.IsNullOrEmpty(apiKey))
        {
            // Skip if not configured - don't fail the test
            Assert.Skip("OPENAI_API_KEY not set - skipping integration test");
        }
        
        // Run real API test
    }
}
```

Or use Traits + test configuration:
```csharp
[Trait("External", "True")]
[Trait("RequiresAuth", "True")]
public async Task CallRealAPI_Works()
{
    // Only run when explicitly enabled in CI with secrets
}
```

## Examples

### Example: Testing Video Transcription Service

```csharp
// Unit Test - Mock the transcription provider
[Fact]
public async Task TranscribeAsync_WhenProviderReturnsError_PropagatesError()
{
    // Arrange
    var mockProvider = new Mock<ITranscriptProvider>();
    mockProvider.Setup(x => x.TranscribeAsync(It.IsAny<string>()))
        .ReturnsAsync((false, null, "API Error"));
    
    var service = new TranscriptService(mockProvider.Object);
    
    // Act
    var result = await service.TranscribeAsync("test.wav");
    
    // Assert
    result.Success.Should().BeFalse();
    result.Error.Should().Contain("API Error");
}

// Integration Test - Real Whisper.NET with tiny file
[Trait("Category", "Integration")]
[Trait("RequiresModel", "True")]  // Needs Whisper model downloaded
[Trait("Speed", "Slow")]
public async Task TranscribeAsync_WithRealWhisper_ProducesTranscript()
{
    // Arrange
    var provider = new WhisperTranscriptProvider();
    var testAudioPath = "Fixtures/tiny-audio.wav";  // 1-second file
    
    // Act
    var result = await provider.TranscribeAsync(testAudioPath);
    
    // Assert
    result.Success.Should().BeTrue();
    result.Transcript.Should().NotBeNullOrEmpty();
}

// E2E Test - Full workflow with fake provider
[Trait("Category", "E2E")]
public async Task CompleteTranscriptionWorkflow_Succeeds()
{
    // Use fake provider to avoid model download
    var fakeProvider = new FakeTranscriptProvider("Test transcript");
    var service = new TranscriptService(fakeProvider);
    
    // Test full workflow without external dependencies
}
```

## Anti-Patterns

### Don't: Make all tests depend on real external services
❌ Every test calls OpenAI API → expensive, slow, flaky

✅ Unit tests mock APIs, integration tests are tagged and optional

### Don't: Skip integration tests entirely
❌ Only unit tests with mocks → may not work with real services

✅ Selective integration tests with real dependencies on small inputs

### Don't: Check secrets into tests
❌ `var apiKey = "sk-abc123..."` in test code

✅ Use environment variables, user secrets, or CI secret management

### Don't: Make slow tests mandatory for local dev
❌ `dotnet test` requires downloading 5GB model and takes 10 minutes

✅ Fast unit tests by default, slow tests tagged and optional

### Don't: Over-mock everything
❌ Mock database with in-memory fake that doesn't match real DB behavior

✅ Use real SQLite in-memory or temp files for integration tests

### Don't: Hardcode test data in tests
❌ Every test creates `new Project { Name = "Test", VideoPath = "test.mp4", ... }`

✅ Use test data builders: `TestData.CreateProject()`

## When This Skill Applies

Use this multi-tiered strategy when your application has:
- External API dependencies (especially paid APIs)
- Large file processing (video, audio, images)
- ML model inference (local or cloud)
- Third-party service integrations (payment, social media)
- Long-running operations (>5 seconds)

Indicators you need this approach:
- "Our tests cost $50/month to run"
- "Tests take 30 minutes on CI"
- "Tests fail randomly due to API rate limits"
- "Can't run tests without internet/API keys"
- "Downloaded 10GB of models just to run tests"
