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

---

### 2026-02-12: Windows Store Submission Process Documentation
**By:** Mike
**What:** Created comprehensive Windows Store submission guide (`docs/WINDOWS-STORE-SUBMISSION.md`) covering prerequisites, packaging, submission, certification, and post-launch processes. Updated CI-CD documentation to reference the guide.
**Why:** The project is ready for Store submission, but developers lack clear guidance on the complex, multi-step process. Documenting prevents mistakes, speeds up future submissions, and provides reference material for handling rejections and updates.

**Key Decisions:**

1. **Manual submission approach** (current)
   - Developers create releases via git tags
   - GitHub Actions builds MSIX automatically
   - Developers manually upload to Partner Center
   - Rationale: Full control over timing, can verify MSIX before submission, easier to handle rejection/resubmit cycles

2. **Comprehensive prerequisite documentation**
   - Documented developer account setup, app reservation, age ratings questionnaire, privacy policy requirements
   - Included asset dimensions and creation guidelines
   - Provided certificate validation checklist
   - Rationale: Most Store rejections come from incomplete prerequisites; upfront documentation prevents failures

3. **Detailed certification issue troubleshooting**
   - Documented common rejection reasons and solutions
   - Included code/manifest examples for common fixes
   - Explained certification timeline and expected durations
   - Rationale: Developers need to quickly diagnose and fix submission failures

4. **Optional automated submission path**
   - Documented Azure AD service principal approach for future Store API integration
   - Not yet implemented due to complexity and risk
   - Provided as reference for future enhancement
   - Rationale: Manual approach sufficient for current needs; automation can be added later with lower risk

5. **Post-submission monitoring guidance**
   - Included Analytics monitoring in Partner Center
   - Provided crash reporting setup (Application Insights) as optional future enhancement
   - Documented update process for published apps
   - Rationale: Post-launch monitoring ensures app health and user satisfaction

**Files Created/Modified:**
- `docs/WINDOWS-STORE-SUBMISSION.md` (new): 25,920 characters, 500+ line comprehensive guide
- `docs/CI-CD.md`: Updated with Store submission section and link to detailed guide

**Related Infrastructure:**
- Existing workflow: `.github/workflows/release.yml` already packages MSIX correctly
- Required secrets already documented: `WINDOWS_CERTIFICATE_BASE64`, `CERTIFICATE_PASSWORD`
- MSIX validation via automated GitHub Actions tests

**Future Enhancements:**
- Integrate Store Submission API for automated uploads (requires Azure AD setup)
- Add Application Insights telemetry for crash reporting
- Automated Store version numbering from git tags
- Scheduled Store health monitoring (analytics dashboard)

---

### 2026-02-12: Windows Store Submission Automation
**By:** Mike
**What:** Implemented automated Windows Store submission pipeline (`.github/workflows/store-publish.yml`) integrated with release workflow, with safe opt-in design and comprehensive Azure AD service principal setup guide.
**Why:** Isaac requested automated Store submission to eliminate manual upload steps and reduce time-to-market. The solution prioritizes safety and control through opt-in design, dry-run capability, and graceful degradation.

**Key Design Decisions:**

1. **Opt-in by default**
   - No auto-submission on every release; requires explicit `auto-submit-store=true` flag
   - Rationale: Full control, prevents accidental submissions

2. **Dry-run capability**
   - Test entire workflow without touching Partner Center API
   - Rationale: Validate setup and troubleshoot safely

3. **Service principal model**
   - Uses Azure AD apps instead of user credentials
   - Rationale: Audit trail + secure credential rotation

4. **Validation first**
   - Validates MSIX integrity, manifest, and secrets BEFORE API calls
   - Rationale: Prevent invalid submissions

5. **Manual fallback**
   - MSIX always available for manual submission via Partner Center UI
   - Rationale: Graceful degradation if automation fails

6. **Postponed full API integration**
   - Workflow has placeholders for StoreBroker/REST API but not wired yet
   - Rationale: Allows incremental enablement as confidence grows

**Implementation Details:**
- Release workflow (`release.yml`): Added `auto-submit-store` input (default: false)
- Store-publish workflow (`store-publish.yml`): 400-line workflow with validation, dry-run, and submission phases
- Azure service principal guide: Detailed docs in `WINDOWS-STORE-AUTOMATION.md`
- Secrets documentation: Updated `SECRETS-SETUP.md` with new Azure/Store secrets

**Files Changed:**
- Created: `.github/workflows/store-publish.yml`
- Created: `docs/WINDOWS-STORE-AUTOMATION.md`
- Modified: `.github/workflows/release.yml` (added auto-submit input)
- Modified: `docs/SECRETS-SETUP.md` (added Store API secrets documentation)
- Modified: `.ai-team/agents/mike/history.md` (added learnings)
