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

### Test Infrastructure Setup (2026-02-11)
- Created `VideoSplitter.Tests` project using xUnit framework with .NET 10
- Target framework must match main project: `net10.0-windows10.0.19041.0`
- Test dependencies: Moq 4.20.72, FluentAssertions 7.0.0, EF Core InMemory 10.0.2, MockHttp 7.0.0
- Directory structure: `Data/`, `Models/`, `Services/{Audio|Clipping|Transcript|SocialMediaPublishers}/`, `Helpers/`, `Fixtures/`

### Model Patterns Discovered
- `AppSettings` uses enums for providers (LlmProvider, TranscriptProvider) not strings
- Settings are nested: `AppSettings.OpenAi.ApiKey` not `AppSettings.OpenAIApiKey`
- `ValidationResult` has `ErrorMessage` (string) not `Errors` (collection)
- `AiSegmentData` has `Start/End/Duration/Reasoning/Excerpt` properties
- EF Core TimeSpan conversion to Ticks for SQLite compatibility

### Test Helper Patterns
- `TestDbContextFactory.CreateInMemoryContext()` creates isolated in-memory EF Core databases
- Each test gets unique database via `Guid.NewGuid().ToString()` as database name
- `TestDataFixtures` provides consistent test data with parameterized factories
- AAA (Arrange-Act-Assert) pattern consistently applied across all tests

### Key File Locations
- Test project: `src/VideoSplitter.Tests/VideoSplitter.Tests.csproj`
- Test helpers: `src/VideoSplitter.Tests/Helpers/TestDbContextFactory.cs`
- Test fixtures: `src/VideoSplitter.Tests/Fixtures/TestDataFixtures.cs`
- Testing documentation: `docs/TESTING.md`

### Gotchas and Solutions
- EF Core tracking issue: When creating multiple entities with same ID, let database assign IDs instead
- Factory methods take enums not strings (LlmProviderFactory, TranscriptProviderFactory)
- Both factories require HttpClient injection, cache provider instances
- MAUI test projects need image processing dependencies (ResizetizeImages) even without images

### Test Coverage Implemented
- 43 passing tests covering:
  - Data layer: DbContext, entity relationships, cascade deletes, TimeSpan conversion
  - Services: SegmentService CRUD operations, ordering, timestamps
  - Models: Project, Segment, ValidationResult defaults and properties
  - AI Services: AiService error handling, progress reporting, provider validation
  - Factories: LlmProviderFactory and TranscriptProviderFactory provider selection and caching
  - Audio: AudioExtractionService basic validation and progress reporting

