# Team Decisions

This file is the authoritative decision ledger. All scope, architecture, and process decisions are recorded here.

---

### 2026-02-11: Team initialization
**By:** Squad (Coordinator)
**What:** Initialized VideoSplitter team with Breaking Bad universe casting
**Why:** Project needs testing infrastructure, CI/CD, and developer experience improvements

**Team:**
- Hank (Lead) — Architecture, decisions, code review
- Jesse (Backend Dev) — C# services, video processing, AI integration
- Gale (Tester) — Tests, quality, edge cases
- Mike (DevOps) — CI/CD, releases, automation
- Scribe — Memory, decisions, session logs
- Ralph — Work queue, backlog, keep-alive

---

### 2026-02-11: Comprehensive Test Strategy for VideoSplitter
**By:** Hank  
**What:** Defined multi-layered testing architecture including unit, integration, and E2E tests with clear frameworks, patterns, and coverage targets  
**Why:** Application has complex external dependencies (FFmpeg, Whisper, LLM providers, social media APIs) and critical workflows (transcription, segment generation, publishing) that require systematic test coverage to ensure reliability

**Key Decisions:**
- **Frameworks**: xUnit (async support), Moq (mocking), FluentAssertions (readability), EF Core InMemory (database testing)
- **Structure**: Mirror source organization (Unit/Integration/E2E folders)
- **Test traits**: Category, Speed, External, RequiresAuth, RequiresModel for selective execution
- **Coverage targets**: >80% for core business logic, >60% for infrastructure services
- **Test patterns**: Mocks for external APIs, real dependencies for database/FFmpeg integration
- **CI execution**: Unit tests required for PR merge, integration tests advisory

**Testability improvements needed:**
- Extract FFmpeg operations behind `IFFmpegService` interface
- Create `IFileSystem` abstraction for file operations
- Inject `ITimeProvider` for time-dependent logic
- Make provider factories injectable (`ILlmProviderFactory`, `ITranscriptProviderFactory`)

---

### 2026-02-11: Comprehensive Testability Analysis for VideoSplitter
**By:** Jesse  
**What:** Analyzed the codebase for testability and identified what needs testing  
**Why:** To provide Gale with a complete roadmap for building comprehensive test coverage

**Key Findings:**
- 37 service files across 6 service categories (Audio, Clipping, Transcript, Settings, File, Social Media)
- Mix of good DI practices (interfaces exist) and tight coupling (direct instantiation)
- Heavy external dependencies: FFmpeg, Whisper.NET, HTTP APIs, file system, database
- Critical business logic: LLM response parsing, segment time calculation, FFmpeg filter generation, settings validation

**HIGH PRIORITY Refactoring:**
- Direct HttpClient instantiation in service constructors (AiService, TranscriptService, SettingsService)
- Factory pattern with `new` operators (LlmProviderFactory, TranscriptProviderFactory untestable)
- Static file system operations (File.Exists, Directory.CreateDirectory throughout)
- Static process execution for FFmpeg and yt-dlp
- Concrete FFMpegCore usage blocking tests without FFmpeg installed
- Whisper provider with model download (heavy initialization)

**Medium/Low Priority:** DbContext usage, SocialMediaPublisher lazy initialization, Console.WriteLine logging

**Testability score:** 5/10 (some good DI, but many tight couplings)

---

### 2026-02-11: Test infrastructure implementation
**By:** Gale
**What:** Implemented comprehensive xUnit test infrastructure with 43 passing tests covering data layer, services, models, and AI integration.
**Why:** Project lacked testing infrastructure. Tests ensure code quality, catch regressions, and provide documentation through examples.

**Implementation Details:**
- **Framework choice**: xUnit for async support and industry adoption
- **Test structure**: Mirrors source organization (Services/Clipping/SegmentServiceTests.cs → Services/Clipping/SegmentService.cs)
- **Coverage**: Unit tests across data layer, services, models; Integration tests for factory patterns and provider selection
- **Test helpers**: In-memory database factory, test data fixtures, comprehensive TESTING.md documentation
- **43 passing tests**: Validated against core business logic, data models, service orchestration

**Future phases:**
- E2E tests for complete workflows
- Performance/load tests for large video files  
- UI/Component tests for MAUI interface
- Contract tests for external API integrations
