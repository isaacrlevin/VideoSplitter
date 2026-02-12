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

### 2026-02-11: CI/CD Pipeline Implementation for Windows Store Publishing
**By:** Mike  
**What:** Implemented three-tier GitHub Actions pipeline (PR validation, CI, Release) with MSIX packaging for Windows Store submission  
**Why:** Project needed automated build, test, and release infrastructure to ensure quality and streamline Windows Store publishing

**Key Decisions:**

1. **Three-Tier Workflow Strategy**
   - PR validation: Fast feedback (build + test only, ~5-8min)
   - CI builds: Main branch validation with coverage and artifacts
   - Release: Full MSIX packaging with code signing
   - Rationale: Balance speed (PRs), thoroughness (CI), and release readiness

2. **Version Management via Git Tags**
   - Git tags (v*.*.*) as source of truth for releases
   - Automatic release trigger on tag push
   - Manual workflow_dispatch as fallback
   - Rationale: Git tags are immutable, traceable, and follow industry standard

3. **Code Signing Approach**
   - Certificate stored as Base64 in GitHub Secrets
   - Decoded at build time, used for signing, cleaned up after
   - Conditional: builds succeed without certificate (dev), sign when available
   - Rationale: Security (no cert in repo), flexibility (works for contributors)

4. **Test Execution Strategy**
   - All 43 tests run on every build (PR, CI, Release)
   - No categorization/filtering needed yet (tests complete in ~1 second)
   - TRX reports with visual test reporter in Actions UI
   - Rationale: Test suite is fast enough for comprehensive execution

5. **MSIX Package Generation**
   - WindowsPackageType=MSIX only in release workflow
   - Self-contained packages with all dependencies
   - Draft GitHub releases for manual review before publishing
   - Rationale: Regular builds faster without packaging, releases ready for Store

6. **Solution File Addition**
   - Created VideoSplitter.sln to simplify build commands
   - Includes both main project and test project
   - Rationale: Simpler workflow syntax, standard .NET convention

**Files Created:**
- `.github/workflows/pr-validation.yml`
- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- `docs/CI-CD.md`
- `VideoSplitter.sln`

**Required Secrets:**
- `WINDOWS_CERTIFICATE_BASE64`: PFX certificate for code signing
- `CERTIFICATE_PASSWORD`: Certificate password
- `CODECOV_TOKEN`: (Optional) For code coverage uploads

**Future Enhancements:**
- Automated Windows Store API submission
- Build caching for faster restore
- Multi-platform builds (iOS, Android, macOS)
- Performance regression testing in CI

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
