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

### CI/CD Pipeline Architecture
- **Workflow structure**: Three-tier approach (PR validation, CI, Release) provides optimal balance of fast feedback and comprehensive validation
- **PR validation**: Lightweight builds (~5-8min) focus on quick feedback - build + test only
- **CI builds**: Extended validation on main with code coverage and artifact retention
- **Release builds**: Full MSIX packaging with code signing for Windows Store submission
- **.NET 10 preview**: Must use `dotnet-quality: 'preview'` in setup-dotnet action
- **MAUI workloads**: Required for MSIX packaging - `dotnet workload install maui`

### Windows Store Packaging
- **WindowsPackageType**: Set to `None` for regular builds, `MSIX` for store packages
- **Code signing**: Certificate stored as Base64 in secrets, decoded at build time, cleaned up after
- **Self-contained packages**: `WindowsAppSDKSelfContained=true` includes all dependencies
- **Version management**: Semantic versioning via Git tags (v1.0.0) or manual workflow dispatch
- **MSIX artifacts**: Retained 90 days for releases, uploaded to GitHub releases as drafts

### Test Integration
- **Test execution**: All 43 tests run in ~1 second, fast enough for every build
- **Test reporting**: TRX format for GitHub Actions with dorny/test-reporter for visualization
- **Code coverage**: Coverlet collector integrated for CI builds, optional Codecov upload
- **No categorization needed**: Test suite is fast, no need for trait-based filtering yet

### Versioning Strategy
- **Git tags as source of truth**: `v*.*.*` tags trigger release workflow automatically
- **Manual fallback**: workflow_dispatch with version input for ad-hoc releases
- **ApplicationVersion**: MSBuild property injected at build time from tag/input
- **Draft releases**: GitHub releases created as drafts for manual review before publishing

### Security and Secrets
- **Certificate management**: PFX stored as Base64, password separate, file cleaned after use
- **Conditional signing**: Build succeeds without certificate (for development), signs when available
- **No hardcoded secrets**: All sensitive data in GitHub Secrets, never in repository

### Key File Paths
- `.github/workflows/pr-validation.yml`: Fast PR checks (build + test)
- `.github/workflows/ci.yml`: Main branch CI with coverage
- `.github/workflows/release.yml`: MSIX packaging for Windows Store
- `docs/CI-CD.md`: Comprehensive pipeline documentation
- `VideoSplitter.sln`: Solution file for simplified build commands
- `src/VideoSplitter/VideoSplitter.csproj`: Main app project with MAUI configuration
- `src/VideoSplitter.Tests/VideoSplitter.Tests.csproj`: Test project with xUnit

### Build Optimizations
- **Path ignore patterns**: Skip CI for markdown, docs, and .ai-team changes
- **Artifact retention**: Tiered retention (7/14/90 days) based on artifact importance
- **Build caching**: Not yet implemented but documented for future optimization
- **Parallel testing**: Not needed yet (tests are fast), but ready for future scaling

### Windows Store Submission Process
- **Comprehensive guide created**: `docs/WINDOWS-STORE-SUBMISSION.md` covers full lifecycle from prerequisites through post-submission
- **Store prerequisites**: Must complete developer account setup, app reservation, age ratings, and privacy policy before submission
- **MSIX packaging**: Store packages require code signing, self-contained dependencies, and strict manifest validation
- **Asset requirements**: Store listings require logos, screenshots, promotional images in specific dimensions
- **Certification timeline**: Typical 24-48 hours; automated validation 1-4 hours, manual review 1-3 days
- **Common issues**: Most certification failures are due to unsigned packages, missing privacy policy, or manifest mismatches
- **Manual vs automated**: Current approach is manual submission (download MSIX, upload to Partner Center) for full control; API-based automation documented for future
- **Update strategy**: Each Store update requires version increment (MAJOR.MINOR.PATCH.BUILD); can resubmit unlimited times if rejected
- **Analytics integration**: Store analytics available in Partner Center; crash reporting can be added via Application Insights (not yet implemented)
