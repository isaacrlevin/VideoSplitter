# Project Context

**Project:** VideoSplitter  
**Owner:** Isaac Levin (isaac.r.levin@gmail.com)  
**Tech Stack:** .NET 10, C#, MAUI, FFMpegCore, Whisper.NET, Azure AI, multiple LLM providers  
**Purpose:** AI-powered extraction of engaging short-form video segments from longer videos

**What it does:**
- Transcribes video using local (Whisper.NET) or cloud (Azure Speech) options
- Uses LLMs (Ollama, OpenAI, Claude, Azure OpenAI, Gemini) to identify engaging segments
- Exports clips or publishes directly to TikTok, YouTube Shorts, Instagram Reels

**Current gaps (why this team exists):**
- Testing infrastructure (unit, integration, E2E)
- CI/CD pipeline and release automation
- Developer experience improvements
- Contributing guides and documentation

---

## Learnings

### Architecture Patterns
- Application uses Factory Pattern extensively: `LlmProviderFactory`, `TranscriptProviderFactory` create appropriate providers based on settings
- Provider pattern with base classes: `LlmProviderBase`, `TranscriptProviderBase` define contracts, concrete providers implement specific APIs
- Service layer follows interface-based design: All services have `I{ServiceName}` interfaces for DI
- Dependency injection configured in `MauiProgram.cs` - mix of Singleton, Scoped lifetimes based on statefulness
- Settings loaded synchronously at startup from `ISettingsService` to avoid async DI issues

### Key File Paths
- **Services**: `src/VideoSplitter/Services/{Audio,Clipping,Settings,SocialMediaPublishers,Transcript}/`
- **LLM Providers**: `src/VideoSplitter/Services/Clipping/LlmProviders/` - 5 providers (Ollama, OpenAI, Anthropic, Azure, Gemini)
- **Transcript Providers**: `src/VideoSplitter/Services/Transcript/TranscriptProviders/` - Whisper.NET and Azure Speech
- **Models**: `src/VideoSplitter/Models/` - Core domain objects (Project, Segment, AppSettings)
- **Database**: `src/VideoSplitter/Data/VideoSplitterDbContext.cs` - EF Core with SQLite
- **DI Configuration**: `src/VideoSplitter/MauiProgram.cs` - all service registrations

### External Dependencies
- **FFMpegCore** - video/audio processing (requires FFmpeg binaries)
- **Whisper.NET** - local speech-to-text (requires ~1.5GB model download)
- **Microsoft.CognitiveServices.Speech** - Azure cloud transcription
- **Microsoft.Extensions.AI** - unified LLM abstraction layer
- **Multiple LLM SDKs** - Azure.AI.OpenAI, OllamaSharp, Mscc.GenerativeAI (Gemini)
- **Social Media SDKs** - Google.Apis.YouTube.v3, custom TikTok/Instagram implementations

### Testing Challenges Identified
- External API dependencies need mocking strategy (LLM providers cost money)
- FFmpeg operations are slow - need test video fixtures
- Whisper.NET model download large - integration tests need to be optional
- Social media OAuth flows difficult to test - need fake providers
- File system operations throughout - need abstraction or temp directory strategy
- Services directly instantiate dependencies (e.g., `new LlmProviderFactory()`) - harder to test, should use DI

### Testability Improvements Recommended
- Extract `IFFmpegService` interface to abstract FFMpegCore calls
- Make provider factories injectable (`ILlmProviderFactory`, `ITranscriptProviderFactory`)
- Consider `IFileSystem` abstraction for file operations
- Consider `ITimeProvider` for testable time-dependent logic
- Settings validation could be more explicit with dedicated validator classes
