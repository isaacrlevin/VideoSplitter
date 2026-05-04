# VideoSplitter - Copilot Instructions

VideoSplitter is a .NET 10 MAUI application that extracts short-form video segments from long videos using AI-powered transcript analysis. It supports multiple transcription providers (local Whisper.NET, Azure Speech), multiple LLM providers (OpenAI, Claude, Gemini, Azure OpenAI, Ollama), and publishes to TikTok, YouTube Shorts, and Instagram Reels.

---

## Build, Test, and Lint Commands

### Building
```bash
# Restore dependencies
dotnet restore

# Build release
dotnet build --configuration Release

# Build specific platform (Windows)
dotnet build -f net10.0-windows10.0.19041.0 --configuration Release
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter SegmentServiceTests

# Run specific test method
dotnet test --filter GetSegmentsByProjectIdAsync_ReturnsSegmentsOrderedByStartTime

# Run with code coverage
dotnet test /p:CollectCoverage=true
```

**Test Project**: `src/VideoSplitter.Tests/`  
**Test Frameworks**: xUnit, Moq, FluentAssertions, Microsoft.EntityFrameworkCore.InMemory, RichardSzalay.MockHttp

**Test Naming Convention**: `MethodName_StateUnderTest_ExpectedBehavior`  
Example: `CreateSegmentAsync_SetsTimestamps`

### CI/CD
- **PR Validation**: `pr-validation.yml` - Runs on PRs (build + test)
- **Continuous Integration**: `ci.yml` - Runs on main branch (build + test + coverage)
- **Release Build**: `release.yml` - Triggered by `v*.*.*` tags (creates MSIX packages)

---

## Architecture Overview

### Service Layer Organization

```
Services/
├── Settings/          → ISettingsService, IProjectService (persistence)
├── Clipping/          → IAiService, IVideoService, SegmentService, PromptService
├── Audio/             → IAudioExtractionService (FFmpeg-based)
├── File/              → IFileStreamService, IPlatformFileService
├── Transcript/        → TranscriptService + provider factory
└── SocialMediaPublishers/ → Multi-platform publishing abstraction
```

**Dependency Injection Lifetimes**:
- **Singletons**: SettingsService, PromptService, AppSettings, FileStreamService, AudioExtractionService, TranscriptService, SubtitleService
- **Scoped**: ProjectService, SegmentService, VideoService, VideoExtractionService, AiService, SocialMediaPublisherService
- **Platform-specific**: IPlatformFileService → `WindowsPlatformFileService` on Windows, `DefaultPlatformFileService` elsewhere

### Data Layer (EF Core + SQLite)

**Models**:
- `Project`: Parent entity with status workflow (`VideoUploaded` → `TranscriptGenerating` → `SegmentsGenerated` → `Completed`)
- `Segment`: Child entity with time ranges, transcripts, AI reasoning, status tracking
- Relationship: One-to-many with cascade delete

**Key EF Core Patterns**:
- TimeSpan stored as Ticks for SQLite compatibility
- Indexed columns: CreatedAt, Status, StartTime, ProjectId
- Database location: `%APPDATA%/VideoSplitter/videosplitter.db`
- Database initialization: `context.Database.EnsureCreated()` in MauiProgram.cs

**AppSettings**: Non-persisted model loaded from JSON at startup via SettingsService

### Provider Abstraction (Factory + Strategy Pattern)

#### LLM Providers
**Interface**: `ILlmProvider`
- `GenerateSegmentsAsync()` - Analyzes transcript, sends to LLM, parses JSON response
- `IsConfigured()` - Validates credentials
- `GetChatClient()` - Returns Microsoft.Extensions.AI.IChatClient

**5 Implementations** (via `LlmProviderFactory`):
1. `OpenAiProvider` - GPT models
2. `AzureOpenAiProvider` - Azure-hosted OpenAI
3. `GoogleGeminiProvider` - Google Gemini
4. `AnthropicProvider` - Claude models (custom HTTP client)
5. `OllamaProvider` - Local models

**Base Class** (`LlmProviderBase`):
- System/User prompt templating with placeholders: `{segmentCount}`, `{segmentLength}`, `{transcript}`
- JSON extraction with fallback strategies
- TimeSpan validation against video duration
- AI response cleaning (removes `<think>` tags, markdown code blocks)
- Local model reinforcement (extra JSON formatting hints for Ollama)

#### Transcription Providers
**Interface**: `ITranscriptProvider`
- `GenerateTranscriptAsync()` - Converts audio to transcript file
- `IsConfigured()` - Validates API keys/setup
- `GetStatusAsync()` - Reports availability/download progress

**2 Implementations** (via `TranscriptProviderFactory`):
1. `WhisperTranscriptProvider` - Local Whisper.NET model (auto-downloads if missing)
2. `AzureSpeechProvider` - Azure Speech Services

#### Social Media Publishers
**Interface**: `ISocialMediaPublisher`
- `AuthenticateAsync()` - OAuth flow with browser
- `ValidateAndRefreshTokensAsync()` - Token lifecycle management
- `PublishVideoAsync()` - Upload video with metadata
- `ValidateVideoAsync()` - Platform-specific constraints
- `DisconnectAsync()` - Revoke tokens

**3 Platform Implementations**:
- `TikTokPublisher`
- `YouTubeShortsPublisher`
- `InstagramReelsPublisher`

**Base Class** (`SocialMediaPublisherBase`):
- OAuth callback listener (local HTTP server catches redirects)
- Credential storage via `SocialMediaCredentialService` (encrypted)
- Platform constraints: max duration, file size, format, aspect ratios

---

## Key Conventions

### Prompt System
- Prompts located in: `src/VideoSplitter/Resources/Raw/Prompts/`
  - `SystemPrompt.md` - Defines AI role, rules, output format
  - `UserPrompt.md` - Contains request and transcript
- Placeholders: `{segmentCount}`, `{segmentLength}`, `{transcript}`
- Managed by `IPromptService` (loads at startup)

### Configuration Flow
1. User selects provider via enum (e.g., `LlmProvider.OpenAI`)
2. Factory creates provider instance: `_providerFactory.GetProvider(settings.SelectedLlmProvider)`
3. Provider validates configuration: `provider.IsConfigured()`
4. Provider executes task (transcription/segment generation/publishing)

### Testing Patterns
- **Test Helpers**: `TestDbContextFactory` (in-memory EF Core), `TestDataFixtures` (consistent test data)
- **AAA Pattern**: Arrange-Act-Assert structure
- **Theory Tests**: Use `[Theory]` with `[InlineData]` for parameterized tests
- **Mocking HTTP**: Use `RichardSzalay.MockHttp.MockHttpMessageHandler`
- **Disposable Contexts**: Tests implement `IDisposable` to clean up database contexts

### Video Processing Pipeline
1. Upload Video → Extract Audio (WAV, 16KHz, mono via FFmpeg)
2. Transcribe Audio → Generate segments via AI
3. Extract video clips → Optionally publish to social media

### Platform-Specific Code
- Use conditional compilation: `#if WINDOWS`
- Abstract platform differences behind interfaces (e.g., `IPlatformFileService`)
- Windows-specific: MSIX packaging, code signing

### Error Handling
- Services return `Result<T>` or throw exceptions caught at UI layer
- Progress reporting via `IProgress<string>` for long-running operations
- Validation: Platform constraints checked before publishing

---

## Documentation References

- **[Testing Guide](../docs/TESTING.md)** - Comprehensive testing patterns and best practices
- **[CI/CD Pipeline](../docs/CI-CD.md)** - Build automation and release process
- **[Setup Transcription](../docs/SetupTranscription.md)** - Configure speech-to-text providers
- **[Setup Segment Generation](../docs/SetupSegmentGeneration.md)** - Configure AI providers
- **[Setup Social Media Publishing](../docs/SetupSocialMediaPublishing.md)** - OAuth setup for platforms
- **[Customizing Prompts](../docs/CustomizingPrompts.md)** - Tailor AI behavior

---

## Development Notes

### Adding a New LLM Provider
1. Create class implementing `ILlmProvider` (or extend `LlmProviderBase`)
2. Add enum value to `LlmProvider` enum
3. Register in `LlmProviderFactory.GetProvider()` switch
4. Add configuration section to `AppSettings.LlmSettings`

### Adding a New Transcription Provider
1. Create class implementing `ITranscriptProvider` (or extend `TranscriptProviderBase`)
2. Add enum value to `TranscriptProvider` enum
3. Register in `TranscriptProviderFactory.GetProvider()` switch
4. Add configuration section to `AppSettings.TranscriptSettings`

### Adding a New Social Media Platform
1. Create class extending `SocialMediaPublisherBase`
2. Implement OAuth flow methods
3. Define platform constraints (max duration, file size, etc.)
4. Add enum value to `SocialMediaPlatform` enum
5. Register in DI container or factory

### Database Migrations
- EF Core migrations not currently used
- Database created via `EnsureCreated()` on startup
- For schema changes: Update models → Delete DB → Restart app
- Future: Migrate to EF Core migrations for production

### FFmpeg Dependency
- Required for video/audio processing
- Must be available in system PATH
- FFMpegCore library handles FFmpeg execution
- Audio extraction: Converts to WAV (16KHz, mono) for transcription

---

## Common Gotchas

### TimeSpan in SQLite
- SQLite doesn't have native TimeSpan type
- Stored as Ticks (long) via `HasConversion()`
- Example: `.HasConversion(v => v.Ticks, v => new TimeSpan(v))`

### AppSettings Loading
- AppSettings loaded synchronously in `MauiProgram.cs` using `Task.Run().GetAwaiter().GetResult()`
- Required to avoid deadlock on UI thread
- Settings persisted as JSON via `ISettingsService`

### Provider Factory Caching
- `LlmProviderFactory` caches provider instances in `_providerCache`
- Single instance per provider type to reuse HTTP clients
- Cache key: provider enum value

### OAuth Callback Listener
- Local HTTP server runs on `http://localhost:8080/callback`
- Must be registered in OAuth app redirect URIs
- Port configurable via `OAuthCallbackListener` constructor
- Server stops after receiving callback

### JSON Parsing Fallbacks
- AI responses may include markdown code blocks (```json ... ```)
- `LlmProviderBase` strips markdown, `<think>` tags, and retries parsing
- Fallback: Even distribution of segments if JSON parsing fails

### Test Isolation
- Each test gets fresh `InMemoryDatabase` via `TestDbContextFactory`
- Unique database name per test to prevent conflicts
- Always dispose contexts in tests via `IDisposable`
