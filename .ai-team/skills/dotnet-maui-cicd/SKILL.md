---
name: "dotnet-maui-cicd"
description: "GitHub Actions CI/CD patterns for .NET MAUI Windows applications with MSIX packaging"
domain: "ci-cd, deployment, windows-store"
confidence: "low"
source: "earned"
---

## Context

When building CI/CD pipelines for .NET MAUI Windows applications destined for the Windows Store, you need to handle:
- .NET preview versions (e.g., .NET 10)
- MAUI workload installation
- MSIX package generation
- Code signing for store submission
- Multi-tier workflow strategy (PR validation, CI, releases)

This skill captures patterns for GitHub Actions workflows that handle these requirements efficiently.

## Patterns

### 1. Three-Tier Workflow Strategy

**PR Validation** - Fast feedback loop
- Build + test only
- No packaging or signing
- Target time: 5-8 minutes
- Fail fast on test failures

**Continuous Integration** - Extended validation
- Build + test + code coverage
- Artifact retention for debugging
- Run on main branch only
- Target time: 6-10 minutes

**Release** - Production packaging
- Build + test + MSIX packaging
- Code signing with certificates
- Artifact retention for store submission
- Triggered by Git tags or manual dispatch
- Target time: 10-15 minutes

### 2. .NET Preview Setup

For preview .NET versions (like .NET 10), use:

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '10.0.x'
    dotnet-quality: 'preview'  # Critical for preview versions
```

### 3. MAUI Workload Installation

MSIX packaging requires MAUI workloads:

```yaml
- name: Install MAUI Workloads
  run: dotnet workload install maui
```

Only needed in release workflow, not PR validation (speeds up PR builds).

### 4. Conditional MSIX Packaging

Use MSBuild property to control packaging:

```yaml
# PR/CI builds - fast, no packaging
- name: Build solution
  run: dotnet build --configuration Release /p:WindowsPackageType=None

# Release builds - with MSIX
- name: Build MSIX Package
  run: dotnet publish -c Release -f net10.0-windows10.0.19041.0 /p:WindowsPackageType=MSIX
```

### 5. Secure Code Signing

Store certificate as Base64, decode at runtime, clean up after:

```yaml
- name: Decode signing certificate
  if: ${{ secrets.WINDOWS_CERTIFICATE_BASE64 != '' }}
  shell: pwsh
  run: |
    $certBytes = [Convert]::FromBase64String("${{ secrets.WINDOWS_CERTIFICATE_BASE64 }}")
    $certPath = Join-Path $env:RUNNER_TEMP "certificate.pfx"
    [IO.File]::WriteAllBytes($certPath, $certBytes)
    echo "CERTIFICATE_PATH=$certPath" >> $env:GITHUB_ENV

- name: Build with signing
  run: |
    dotnet publish /p:PackageCertificateKeyFile="$env:CERTIFICATE_PATH" \
                   /p:PackageCertificatePassword="${{ secrets.CERTIFICATE_PASSWORD }}"

- name: Cleanup certificate
  if: always() && env.CERTIFICATE_PATH != ''
  shell: pwsh
  run: |
    if (Test-Path $env:CERTIFICATE_PATH) {
      Remove-Item $env:CERTIFICATE_PATH -Force
    }
```

**Key points:**
- Use `$env:RUNNER_TEMP` (auto-cleaned by GitHub)
- Conditional check prevents failure when cert not configured
- Always cleanup in finally-style step
- Never commit certificates to repository

### 6. Version from Git Tags

Extract version from Git tags or workflow inputs:

```yaml
- name: Determine Version
  id: version
  shell: pwsh
  run: |
    if ("${{ github.event_name }}" -eq "workflow_dispatch") {
      $version = "${{ github.event.inputs.version }}"
    } elseif ("${{ github.ref }}" -like "refs/tags/v*") {
      $version = "${{ github.ref }}".Replace("refs/tags/v", "")
    } else {
      $version = "1.0.0"
    }
    echo "VERSION=$version" >> $env:GITHUB_OUTPUT

- name: Use version
  run: dotnet publish /p:ApplicationVersion=${{ steps.version.outputs.VERSION }}.0
```

### 7. Test Execution with Reporting

Use TRX format and upload for visualization:

```yaml
- name: Run tests
  run: dotnet test --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage"

- name: Publish test results
  uses: dorny/test-reporter@v1
  if: always()
  with:
    name: Test Results
    path: '**/test-results.trx'
    reporter: dotnet-trx
    fail-on-error: true
```

### 8. Tiered Artifact Retention

Different retention for different artifact types:

```yaml
# PR test results - short retention
- uses: actions/upload-artifact@v4
  with:
    retention-days: 7

# CI build outputs - medium retention
- uses: actions/upload-artifact@v4
  with:
    retention-days: 14

# Release packages - long retention
- uses: actions/upload-artifact@v4
  with:
    retention-days: 90
```

### 9. Path Ignore Patterns

Skip CI for documentation changes:

```yaml
on:
  pull_request:
    paths-ignore:
      - '**.md'
      - 'docs/**'
      - '.ai-team/**'
      - 'LICENSE'
```

## Examples

### Complete PR Validation Workflow

```yaml
name: PR Validation
on:
  pull_request:
    branches: [ main ]
    paths-ignore: ['**.md', 'docs/**']

jobs:
  build-and-test:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
        dotnet-quality: 'preview'
    - run: dotnet restore
    - run: dotnet build --configuration Release --no-restore /p:WindowsPackageType=None
    - run: dotnet test --configuration Release --no-build --logger "trx"
    - uses: dorny/test-reporter@v1
      if: always()
      with:
        name: Test Results
        path: '**/test-results.trx'
        reporter: dotnet-trx
```

### Complete Release Workflow

See `.github/workflows/release.yml` in VideoSplitter project for full example.

## Anti-Patterns

❌ **Don't build MSIX on every PR**
- Slow (adds MAUI workload installation time)
- Unnecessary (not testing packaging in PRs)
- Use WindowsPackageType=None for PR/CI builds

❌ **Don't store certificates in repository**
- Security risk
- Use GitHub Secrets with Base64 encoding
- Decode at runtime, clean up after

❌ **Don't hardcode versions**
- Use Git tags as source of truth
- Allow manual override with workflow_dispatch
- Inject version at build time

❌ **Don't skip tests in release builds**
- Always run tests before packaging
- Fail the release if tests fail
- Test results = quality gate

❌ **Don't use same retention for all artifacts**
- PR artifacts: 7 days (debug only)
- CI artifacts: 14 days (troubleshooting)
- Release packages: 90 days (store submission)

❌ **Don't forget to clean up sensitive files**
- Use `if: always()` for cleanup steps
- Delete certificates after use
- Use $env:RUNNER_TEMP for temp files

## Related Skills

- `dotnet-testing-setup` - Setting up xUnit test infrastructure
- `testing-external-dependencies` - Mocking for testable code
