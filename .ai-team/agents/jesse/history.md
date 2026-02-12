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

### Service Architecture
- Services organized in 6 categories: Audio, Clipping (with LLM sub-providers), Transcript, Settings, File, Social Media
- Mix of interface-based DI (good) and direct instantiation (needs refactoring)
- Factory pattern used for LLM and transcript providers but with `new` operators

### External Dependencies
- FFmpegCore for video/audio processing - wraps FFmpeg/FFProbe CLI tools
- Whisper.NET for local transcription - requires model download
- YouTubeExplode for YouTube downloads with yt-dlp fallback
- Multiple LLM providers: OpenAI, Anthropic, Google Gemini, Azure OpenAI, Ollama
- Entity Framework Core with SQLite for persistence

### Testability Blockers
- Direct file system operations (File.*, Directory.*) throughout codebase
- Direct process execution for FFmpeg, yt-dlp validation
- HttpClient instantiated directly in constructors instead of IHttpClientFactory
- Factories create providers with `new` instead of DI
- FFMpegCore library called directly without abstraction

### Business Logic Locations
- LLM response parsing and segment generation: `LlmProviderBase.cs` lines 44-538
- Video aspect ratio conversion filters: `VideoExtractionService.cs` lines 220-434
- Optimal segment time calculation: `LlmProviderBase.CalculateOptimalStartTimes()`
- Prompt template management: `PromptService.cs`, `LlmProviderBase.CreateSystemPrompt/UserPrompt()`
- API validation: `SettingsService.cs` validation methods (lines 313-514)

### File Paths
- Services: `src/VideoSplitter/Services/` with subdirectories Audio, Clipping, Transcript, Settings, File, SocialMediaPublishers
- LLM Providers: `src/VideoSplitter/Services/Clipping/LlmProviders/`
- Models: `src/VideoSplitter/Models/`
- Database: `src/VideoSplitter/Data/VideoSplitterDbContext.cs`
- No test project exists yet
