# CI/CD Pipeline Documentation

This document describes the CI/CD pipeline for VideoSplitter, including build processes, testing, and Windows Store packaging.

## Overview

The VideoSplitter project uses GitHub Actions for automated builds, testing, and packaging. The pipeline consists of three main workflows:

1. **PR Validation** - Fast feedback on pull requests
2. **Continuous Integration** - Build and test on main branch
3. **Release Build** - Create MSIX packages for Windows Store submission

## Workflows

### 1. PR Validation (`pr-validation.yml`)

**Trigger:** Pull requests to `main` or `develop` branches

**Purpose:** Ensure code quality before merging

**Steps:**
- Checkout code
- Setup .NET 10
- Restore dependencies
- Build solution (Release configuration)
- Run all tests (43 unit/integration tests)
- Publish test results

**Duration:** ~5-8 minutes

**What it validates:**
- Code compiles without errors
- All tests pass
- No regressions introduced

### 2. Continuous Integration (`ci.yml`)

**Trigger:** 
- Push to `main` branch
- Manual workflow dispatch

**Purpose:** Validate and build main branch after merge

**Steps:**
- All PR validation steps
- Code coverage collection
- Upload coverage to Codecov (optional)
- Upload build artifacts (14-day retention)

**Duration:** ~6-10 minutes

**Artifacts:**
- Test results (30-day retention)
- Code coverage reports
- Build outputs (14-day retention)

### 3. Release Build (`release.yml`)

**Trigger:**
- Git tags matching `v*.*.*` (e.g., `v1.0.0`)
- Manual workflow dispatch with version input

**Purpose:** Create MSIX packages for Windows Store submission

**Steps:**
- Determine version from tag or input
- Setup .NET 10 and MAUI workloads
- Restore and build
- Run full test suite
- Decode signing certificate (if configured)
- Build MSIX package with code signing
- Upload MSIX artifacts (90-day retention)
- Create GitHub release (draft)

**Duration:** ~10-15 minutes

**Artifacts:**
- MSIX packages signed for Windows Store
- Test results
- Draft GitHub release

## Version Management

### Versioning Strategy

The project uses **Semantic Versioning (SemVer)**: `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking changes or major feature releases
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes

### Version Sources

1. **Git Tags** (Recommended for releases)
   ```bash
   git tag v1.2.0
   git push origin v1.2.0
   ```
   This automatically triggers the release workflow.

2. **Manual Dispatch**
   - Go to Actions → Release Build → Run workflow
   - Enter version number (e.g., `1.2.0`)

3. **Project File** (Default: `1.0`)
   - Edit `ApplicationDisplayVersion` in `VideoSplitter.csproj`
   - Only used as fallback display version

### Updating Version

To create a new release:

```bash
# 1. Update version in project file (optional, for display)
# Edit src/VideoSplitter/VideoSplitter.csproj
# <ApplicationDisplayVersion>1.2.0</ApplicationDisplayVersion>

# 2. Commit any changes
git add .
git commit -m "Prepare release v1.2.0"
git push

# 3. Create and push tag
git tag v1.2.0
git push origin v1.2.0

# 4. GitHub Actions will automatically:
#    - Build the application
#    - Run tests
#    - Create MSIX package
#    - Create draft GitHub release
```

## Required Secrets

Configure these in GitHub Settings → Secrets and variables → Actions:

### Signing Certificate (Required for Windows Store)

**`WINDOWS_CERTIFICATE_BASE64`**
- Base64-encoded PFX certificate file
- Used to sign the MSIX package

**`CERTIFICATE_PASSWORD`**
- Password for the PFX certificate
- Used to unlock the certificate during signing

**For detailed certificate creation and setup instructions, see [CERTIFICATE-SETUP.md](CERTIFICATE-SETUP.md)**

### Optional Secrets

**`CODECOV_TOKEN`**
- Token for uploading code coverage to Codecov
- Only needed if using Codecov integration

## Windows Store Publishing

### Package Generation

The release workflow creates an MSIX package suitable for Windows Store submission. The package includes:

- Application binaries
- Dependencies (self-contained)
- App manifest
- Digital signature (if certificate configured)
- Version metadata

### Manual Store Submission

1. **Download MSIX Package**
   - Go to GitHub Actions → Release Build → Latest run
   - Download the MSIX artifact

2. **Submit to Partner Center**
   - Go to [Windows Partner Center](https://partner.microsoft.com/dashboard)
   - Create or update app submission
   - Upload MSIX package
   - Fill out store listing details
   - Submit for certification

### Automated Store Submission (Future)

For fully automated publishing, you can integrate with the Windows Store API:

```yaml
# Future enhancement - requires Store API credentials
- name: Publish to Windows Store
  uses: microsoft/windows-appconsult-tools-store-submission@v1
  with:
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    client-secret: ${{ secrets.AZURE_CLIENT_SECRET }}
    app-id: ${{ secrets.STORE_APP_ID }}
    package-path: 'AppPackages/*.msix'
```

## Test Execution

### Test Categories

The project has 43 tests organized by type:

- **Unit Tests**: Fast, isolated component tests
- **Integration Tests**: Multi-component interaction tests
- **Data Layer Tests**: Database operations with in-memory EF Core

### Test Execution Strategy

**PR Validation & CI:**
- Run all tests on every build
- Tests are fast (~30-60 seconds total)
- Fail the build on any test failure

**Future Optimization:**
If test suite grows large, consider:
- Test categorization with traits
- Parallel test execution
- Separate fast/slow test jobs

### Test Reports

Test results are published as:
- TRX files (uploaded as artifacts)
- Visual test report in Actions tab
- Build status badges (configurable)

### Code Coverage

Code coverage is collected in CI builds using Coverlet:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports are uploaded to Codecov (if token configured).

**Coverage Goals:**
- Critical Services: 80%+
- Data Layer: 90%+
- Overall Project: 75%+

See [TESTING.md](../docs/TESTING.md) for more details.

## Troubleshooting

### Build Failures

**"Could not find .NET 10"**
- The .NET 10 SDK is in preview
- Ensure the workflow uses `dotnet-quality: 'preview'`
- GitHub hosted runners are updated weekly

**"WindowsPackageType not recognized"**
- MAUI workloads not installed
- Add step: `dotnet workload install maui`

**"Certificate not valid"**
- Check certificate expiration date
- Verify Base64 encoding is correct
- Ensure password matches certificate

### Test Failures

**"Tests timeout or hang"**
- Check for async/await issues
- Look for database locking (ensure each test has own context)
- Review test logs in artifacts

**"Flaky tests"**
- Tests with timing dependencies
- Shared state between tests
- External dependencies not properly mocked

### Package Creation Failures

**"MSIX generation failed"**
- Verify `WindowsPackageType=MSIX` is set
- Check manifest file validity
- Ensure all dependencies are included

**"Code signing failed"**
- Certificate must be valid for code signing
- Password must match certificate
- Certificate must not be expired

## Configuration Files

### Project Configuration

**`VideoSplitter.csproj`** - Key settings:
```xml
<ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
<ApplicationVersion>1</ApplicationVersion>
<WindowsPackageType>MSIX</WindowsPackageType>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
```

### Workflow Configuration

All workflows are in `.github/workflows/`:
- `pr-validation.yml` - PR checks
- `ci.yml` - Main branch CI
- `release.yml` - Release packaging

## Performance Optimization

### Build Times

Current typical durations:
- PR Validation: 5-8 minutes
- CI Build: 6-10 minutes
- Release Build: 10-15 minutes

### Future Optimizations

1. **Caching**
   ```yaml
   - uses: actions/cache@v4
     with:
       path: ~/.nuget/packages
       key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
   ```

2. **Matrix Builds** (if supporting multiple platforms)
   ```yaml
   strategy:
     matrix:
       platform: [windows, macos, linux]
   ```

3. **Parallel Testing**
   ```bash
   dotnet test --parallel
   ```

## Best Practices

### For Developers

✅ **DO:**
- Run tests locally before pushing
- Use descriptive commit messages
- Tag releases with semantic versions
- Review build logs for warnings

❌ **DON'T:**
- Commit secrets or credentials
- Merge PRs with failing tests
- Skip CI checks
- Force-push to protected branches

### For Releases

1. **Pre-Release Checklist:**
   - [ ] All tests passing
   - [ ] Documentation updated
   - [ ] Version number decided
   - [ ] Release notes drafted
   - [ ] Certificate valid and available

2. **Release Process:**
   - Create and push tag
   - Monitor GitHub Actions
   - Download and verify MSIX
   - Submit to Windows Store
   - Update GitHub release from draft to published

3. **Post-Release:**
   - Monitor Store certification
   - Update documentation
   - Announce release

## Monitoring and Alerts

### GitHub Actions Notifications

Configure notifications in GitHub Settings:
- Email on workflow failure
- Slack/Discord webhooks (optional)
- GitHub mobile app notifications

### Build Badges

Add to README.md:

```markdown
![PR Validation](https://github.com/YOUR_USERNAME/VideoSplitter/actions/workflows/pr-validation.yml/badge.svg)
![CI Build](https://github.com/YOUR_USERNAME/VideoSplitter/actions/workflows/ci.yml/badge.svg)
```

## Cost and Resource Usage

### GitHub Actions Minutes

Free tier includes:
- 2,000 minutes/month for public repos
- 500 minutes/month for private repos (Windows runners use 2x multiplier)

Typical usage:
- PR builds: ~8 minutes × 2 (Windows) = 16 minutes
- CI builds: ~10 minutes × 2 = 20 minutes
- Release builds: ~15 minutes × 2 = 30 minutes

### Artifact Storage

- PR test results: 7-day retention (minimal storage)
- CI artifacts: 14-day retention (~100MB per build)
- Release packages: 90-day retention (~50MB per release)

Total expected: <5GB with typical development pace

## Future Enhancements

- [ ] Automated Windows Store submission via API
- [ ] Branch deployment environments (dev, staging, prod)
- [ ] Performance regression testing
- [ ] Visual regression testing for UI
- [ ] Automated changelog generation
- [ ] Multi-platform builds (iOS, Android, macOS)
- [ ] Container-based builds with Docker
- [ ] Blue/green deployment strategy

## Resources

- [GitHub Actions Documentation](https://docs.github.com/actions)
- [.NET MAUI Deployment](https://learn.microsoft.com/dotnet/maui/deployment/)
- [Windows Store Submission](https://learn.microsoft.com/windows/apps/publish/)
- [Code Signing for Windows Apps](https://learn.microsoft.com/windows/msix/package/sign-app-package-using-signtool)
- [Semantic Versioning](https://semver.org/)

## Support

For issues with the CI/CD pipeline:
1. Check workflow run logs in GitHub Actions
2. Review this documentation
3. Open an issue on GitHub
4. Contact: isaac.r.levin@gmail.com
