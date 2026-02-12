# Windows Store Submission Guide for VideoSplitter

This document covers the complete process for submitting VideoSplitter to the Microsoft Store, from preparation through post-submission monitoring.

## Prerequisites

Before you can submit to the Windows Store, ensure you have completed all prerequisite steps.

### 1. Windows Store Developer Account

- **Create account**: Visit [developer.microsoft.com](https://developer.microsoft.com) and sign up for a developer account
- **Cost**: $19 USD one-time registration fee
- **Duration**: Account setup typically completes within minutes; publisher verification may take a few days
- **Verification**: Microsoft may request business documentation or identity verification
- **Tip**: Use a consistent email address that will remain stable; this becomes your app publisher identity

### 2. App Registration and Identity Reservation

#### Reserved App Name
1. Log in to [Partner Center](https://partner.microsoft.com/dashboard)
2. Go to **Apps and games** → **Overview**
3. Click **Create a new app**
4. Reserve an app name (e.g., "VideoSplitter")
   - Names must be unique in the Store
   - You can reserve up to 100 names
   - Once published, you cannot change the reserved name
5. Note the **Application ID** and **Publisher ID** — these are required for code signing

#### Package Identity
The MSIX package must match the reserved app identity:
- **Publisher**: Must match the certificate subject
- **Application ID**: Matches the reserved name
- **Version**: Must increment with each submission

These are configured in the `Package.appxmanifest` file within the MAUI project. The CI/CD pipeline injects the version at build time.

### 3. Age Ratings and Content Descriptors

The Store requires content ratings to determine availability by region and age group.

#### Questionnaire
1. In Partner Center, navigate to **Pricing and availability** → **Age ratings**
2. Complete the **IAMAI** (Interactive Software Self-Classification System) form:
   - Violence
   - Language
   - Sexual content
   - Discrimination
   - Drugs/alcohol
   - Gambling
3. Save your responses
4. Rating boards (ESRB, PEGI, ClassInd, etc.) are automatically assigned based on your answers

#### Expected Ratings
For VideoSplitter (a productivity/education tool):
- **ESRB**: Generally rates as "E for Everyone" or "T for Teen" depending on LLM content
- **PEGI**: Typically PEGI 3 or PEGI 7
- **Recommendation**: Conservative approach—rate higher if uncertain

### 4. Privacy Policy

**Required**: A publicly accessible privacy policy URL

#### What to Include
- What data the app collects (none locally, but API interactions)
- How data is used (LLM API calls, video transcription)
- Third-party services (OpenAI, Anthropic, Google, Azure, etc.)
- User rights and data retention
- Contact information for privacy inquiries
- Cookie usage (if applicable)
- GDPR compliance (for EU users)

#### Hosting Options
- GitHub Pages (free, if you have a `docs/privacy-policy.md` file)
- Your website
- A markdown file rendered via a service

#### Privacy Policy Checklist
- [ ] Policy is publicly accessible and doesn't require login
- [ ] Policy clearly lists all external APIs used (OpenAI, Anthropic, Azure, etc.)
- [ ] Policy explains how video files are handled
- [ ] Policy mentions any crash reporting or telemetry (if enabled)
- [ ] Policy includes contact information for privacy requests
- [ ] Policy is updated to match your current feature set

### 5. Code Signing Certificate

The MSIX package must be signed with a valid certificate.

#### Certificate Requirements
- **Type**: Authenticode certificate (SHA-256)
- **Validity**: Must be valid at submission time and remain valid for 10+ years
- **Subject**: Must match the Publisher identity in Package.appxmanifest
- **EKU**: Code Signing and Time Stamping

#### Obtaining a Certificate
- **Self-signed** (for Store): Works if you own the publisher identity
  ```powershell
  New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=YourPublisher" `
    -TextExtension "2.5.29.37={text}1.3.6.1.5.5.7.3.3" `
    -KeyUsage DigitalSignature -HashAlgorithm sha256 -NotAfter (Get-Date).AddYears(10)
  ```
- **Third-party CA** (e.g., DigiCert, Sectigo): ~$200-400/year, provides broader trust chain
- **Tip**: Start with self-signed; upgrade to CA-signed if Store rejects it

#### Storing the Certificate
See [CERTIFICATE-SETUP.md](./CERTIFICATE-SETUP.md) for detailed certificate creation and GitHub Secrets configuration.

---

## Packaging for the Windows Store

### Store-Specific Package Requirements

The Windows Store has stricter requirements than dev builds:

| Requirement | Dev Build | Store Package |
|-------------|-----------|---------------|
| **WindowsPackageType** | None | MSIX |
| **Code Signing** | Optional | Required |
| **Self-Contained** | Optional | **Required** |
| **Architecture** | Any | x64 (recommended) or x86 |
| **Windows SDK** | Any | 19041 or higher |
| **MSIX Validation** | None | Full Microsoft validation |

### App Identity Configuration

The MAUI project manifest must include:

```xml
<!-- Package.appxmanifest -->
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
  
  <Properties>
    <DisplayName>VideoSplitter</DisplayName>
    <PublisherDisplayName>Your Publisher Name</PublisherDisplayName>
  </Properties>
  
  <Applications>
    <Application Id="App" StartPage="$StartupPage$">
      <VisualElements DisplayName="VideoSplitter" .../>
    </Application>
  </Applications>
</Package>
```

**Key identifiers:**
- **DisplayName**: User-facing app name (can be localized)
- **PublisherDisplayName**: Your name or company name (matches certificate subject)
- **Package Identity**: Must be reserved in Partner Center

### Asset Requirements

The Store listing requires visual assets:

#### Required Assets
| Asset | Dimensions | Format | Notes |
|-------|-----------|--------|-------|
| **App Logo** | 1:1 (e.g., 300×300) | PNG | Displays in Store listings, start menu |
| **Store Logo** | 1:1 (e.g., 120×120) | PNG | Smaller version for Store tiles |
| **Tile Image** | 1:1 (e.g., 310×310) | PNG | Live tile on Windows |
| **Wide Tile** | 31:14 (e.g., 620×300) | PNG | Optional, for live updates |
| **Splash Screen** | 16:9 (e.g., 1240×690) | PNG | Loading screen |

#### Store Listing Images
| Image | Dimensions | Format | Quantity |
|-------|-----------|--------|----------|
| **Screenshots** | 16:9 (e.g., 1920×1080) or 9:16 | PNG/JPG | 1-9 (recommended 3-5) |
| **Promotional Image** | 2:3 (e.g., 1000×1500) | PNG/JPG | Optional but recommended |
| **Hero Image** | 16:9 (e.g., 1920×1080) | PNG/JPG | Displays on Store main page |

#### Asset Creation Tips
- Use branding colors and fonts consistent with your app
- Screenshots should show key features: video upload, AI analysis, segment generation, publishing
- Include captions on screenshots explaining features
- Keep assets professional; blurry or low-quality images reduce downloads

### MSIX Package Structure

The release workflow generates an MSIX package containing:

```
VideoSplitter_1.0.0.0_x64.msix
├── [AppxManifest.xml]      (Package metadata)
├── [AppxBlockMap.xml]      (Integrity verification)
├── [AppxSignature.p7x]     (Code signature)
├── [Content_Types].xml     (MIME types)
└── [Resources]
    ├── System.dll
    ├── FFMpegCore.dll
    ├── Whisper.NET.dll
    └── (all dependencies)
```

**Self-contained approach:**
- All .NET and app dependencies bundled in the package
- No need for user to install .NET runtime separately
- Larger download (~200-300MB), but better user experience
- Configured via `/p:WindowsAppSDKSelfContained=true` in release workflow

### Package Validation

Before submission, validate the MSIX locally:

```powershell
# Install Windows App Certification Kit (free, via Microsoft Store)
# Or use dotnet CLI validation:

# 1. Build locally with Store settings
dotnet publish src/VideoSplitter/VideoSplitter.csproj `
  -c Release `
  -f net10.0-windows10.0.19041.0 `
  /p:WindowsPackageType=MSIX `
  /p:WindowsAppSDKSelfContained=true `
  /p:GenerateAppxPackageOnBuild=true

# 2. Verify package signature
Get-ChildItem AppPackages\*.msix | ForEach-Object {
  $file = $_.FullName
  Write-Host "Validating $file..."
  # Microsoft.Windows.ApplicationModel.Resources.dll checks signature
}

# 3. Test install locally
Add-AppxPackage -Path AppPackages\VideoSplitter_*.msix
```

---

## Submission Process

### Step 1: Prepare in Partner Center

1. **Go to Partner Center** → [https://partner.microsoft.com/dashboard](https://partner.microsoft.com/dashboard)

2. **Select your app** (or create new if not yet reserved)

3. **Navigate to Submissions** → **Start your submission**

4. **Choose submission type:**
   - ✅ **New app** (first release)
   - ✅ **Update** (subsequent releases)

### Step 2: Fill Out Store Listing

#### Product Information
- **Product name**: VideoSplitter (must match reserved name)
- **Subtitle** (optional): "AI-powered video segment extraction"
- **Description**: 
  ```
  VideoSplitter automatically identifies engaging short-form video segments 
  using AI analysis. Perfect for content creators who want to extract clips 
  from long-form videos for TikTok, YouTube Shorts, and Instagram Reels.
  
  Features:
  - Automatic segment detection using AI
  - Video transcription (local Whisper or cloud options)
  - Multi-LLM support (OpenAI, Anthropic, Ollama)
  - Direct publishing to social media
  - Customizable segment criteria
  ```

#### Keywords
- Add relevant search keywords: "video", "editing", "AI", "clips", "shorts", "content creation"
- Maximum 7 keywords, 30 characters each

#### Age Ratings
- **Rating** (completed in prerequisites)
- **Content descriptors**: None (unless your privacy/content policies require)

#### Pricing and Availability
- **Price**: Free (recommended for indie projects)
- **Regions**: Select all regions (or specific ones)
- **Markets**: Language support (English at minimum)
- **Visibility**: Public (recommended) or Hidden

#### Screenshots
1. Click **Add screenshot**
2. Upload 3-5 high-quality 16:9 images (1920×1080 recommended)
3. Add captions:
   - "Choose video and upload"
   - "AI analyzes content and extracts segments"
   - "Preview and customize clips"
   - "Publish directly to social media"
4. Ensure all screenshots are in English or your target language

#### Icons and Images
- **Logo** (300×300): Branding icon
- **Store Logo** (120×120): Small tile icon
- **Tile Image** (310×310): Start menu tile
- **Promotional image** (optional): 1000×1500

### Step 3: Upload MSIX Package

1. **Go to Packages** section in your submission

2. **Download the MSIX from GitHub Actions:**
   - Navigate to `.github/workflows/release.yml` latest run
   - Download the artifact `msix-package-v1.x.x`
   - File is named `VideoSplitter_1.x.x.0_x64.msix`

3. **Upload to Partner Center:**
   - Click **Upload package**
   - Select the MSIX file
   - Wait for Microsoft's automated validation (5-10 minutes)

4. **Automated Validation Results:**
   - ✅ **Passed**: Package is valid and ready for submission
   - ❌ **Failed**: Review error details and repackage
   - ⚠️ **Warnings**: Non-critical issues, usually OK to proceed

### Step 4: Age Ratings Certification

1. **Verify age ratings** from prerequisites are loaded
2. **Review rating descriptors** for accuracy
3. **If needed, update via IAMAI questionnaire** in Pricing and availability

### Step 5: Declare Capabilities

Partner Center auto-detects capabilities from the manifest. Review:

- **Camera**: Only if actually used
- **Microphone**: Only if audio input needed
- **Location**: Uncheck if not used
- **Contacts**: Only if integrating with user contacts
- **Files**: Check if accessing user's Documents/Videos

VideoSplitter likely needs:
- ✅ **File system**: Access to Documents, Videos, temp storage
- ❌ **Camera/Microphone**: Not needed (FFmpeg handles playback)
- ❌ **Contacts/Location**: Not needed

### Step 6: Notes for Certification

Add helpful notes to speed up Store review:

```
VideoSplitter is a content creation tool that analyzes videos using AI.

Key points for reviewers:
- Uses local Whisper.NET or cloud transcription APIs for audio-to-text
- Integrates with multiple LLM providers (OpenAI, Anthropic, Google, Azure)
- Exports MPEG-4 video segments
- Optional: Direct publishing to TikTok, YouTube, Instagram APIs
- Privacy policy: [URL to your policy]
- External API calls are required for core functionality

All external APIs are called via HTTPS with encrypted credentials.
No user data is stored locally beyond cache files.
```

### Step 7: Publish Submission

1. **Review checklist:**
   - [ ] All required fields filled
   - [ ] MSIX package uploaded and validated
   - [ ] Screenshots and assets uploaded
   - [ ] Privacy policy URL provided
   - [ ] Age ratings completed
   - [ ] No placeholder content
   - [ ] Accurate description

2. **Click Submit for certification**

3. **Confirmation:**
   - Submission ID generated
   - Status changes to "In certification"
   - You receive email confirmation

---

## Certification Process

### Timeline

Typical Windows Store certification timeline:

| Stage | Duration | Status |
|-------|----------|--------|
| **Submission** | 0 | "In certification" |
| **Automated scanning** | 1-4 hours | "Processing" |
| **Manual review** | 1-3 days | "In manual review" |
| **Approval/Rejection** | See below | "Approved" or "Held" |

Average total: **24-48 hours**, up to **7 days** for edge cases.

### Common Certification Issues

#### Package-Related Issues

**"Invalid package signature"**
- ❌ Certificate expired or mismatched with Publisher ID
- ✅ Solution: Reissue certificate and rebuild MSIX

**"Code not signed"**
- ❌ No code signing certificate provided
- ✅ Solution: Provide valid certificate in GitHub Secrets

**"AppxManifest.xml validation failed"**
- ❌ Package identity doesn't match reserved app name
- ✅ Solution: Update manifest and repackage

**"Self-contained package incomplete"**
- ❌ Missing required .NET runtime or dependencies
- ✅ Solution: Ensure `/p:WindowsAppSDKSelfContained=true` in build

#### Content-Related Issues

**"Privacy policy missing or inaccessible"**
- ❌ URL doesn't work or requires login
- ✅ Solution: Ensure policy is public and always available

**"Undeclared external capabilities"**
- ❌ App calls external APIs but doesn't declare them
- ✅ Solution: Document API usage in Store listing and certification notes

**"Misleading store listing"**
- ❌ Description doesn't match actual functionality
- ✅ Solution: Ensure description accurately represents features

**"Suspicious API integration"**
- ❌ App requests excessive permissions
- ✅ Solution: Only declare capabilities actually used

#### Policy Violations

**"Contains malware or unwanted software"**
- Rare for legitimate projects
- Solution: Ensure no bundled ads, miners, or tracking

**"Violates trademark or intellectual property"**
- ❌ App name or assets infringe on existing trademarks
- ✅ Solution: Change name or get legal clearance

### Managing Rejections

If certification fails:

1. **Read the rejection reason carefully**
2. **Click "Contact support"** if reason is unclear
3. **Make corrections** (code changes, manifest updates, or metadata)
4. **Rebuild and resubmit** a new version
5. **Reference previous rejection** in certification notes

**Tip**: Microsoft typically allows unlimited resubmissions; each attempt counts as a new version.

---

## CI/CD Integration

### Automated Packaging via GitHub Actions

The existing release workflow (`.github/workflows/release.yml`) already handles MSIX creation:

1. **Trigger release:**
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Workflow automatically:**
   - Builds the project
   - Runs all tests
   - Creates MSIX package with code signing
   - Uploads MSIX to GitHub releases

3. **Download and submit manually:**
   - Go to GitHub Actions → Release Build → Latest run
   - Download MSIX artifact
   - Upload to Partner Center

### Manual Store Submission (Current Approach)

**Steps:**
1. Create a release via git tag
2. Wait for GitHub Actions to complete
3. Download the MSIX artifact
4. Log in to Partner Center
5. Upload MSIX and fill out store listing
6. Submit for certification

**Advantages:**
- ✅ Full control over submission timing
- ✅ Review MSIX before uploading to Store
- ✅ Manually verify all store details
- ✅ Can reject and repackage if needed

**Disadvantages:**
- ❌ Requires manual steps
- ❌ More prone to human error
- ❌ Slower time-to-market

### Optional: Automated Store Submission via API

For future consideration, the Store Submission API allows programmatic upload:

```yaml
# .github/workflows/store-publish.yml (Future)
- name: Publish to Windows Store
  uses: microsoft/windows-appconsult-tools-store-submission@v1
  with:
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    client-secret: ${{ secrets.AZURE_CLIENT_SECRET }}
    app-id: ${{ secrets.STORE_APP_ID }}
    package-path: 'AppPackages/*.msix'
    skip-packaging: true
    force-submit: false
```

**Requirements:**
- Azure AD service principal (for API auth)
- Partner Center API access enabled
- Store API credentials in GitHub Secrets

**Not yet implemented** due to complexity and security implications.

### Versioning Strategy for Store

Each Store submission must have a unique version number.

**Semantic Versioning: `MAJOR.MINOR.PATCH.BUILD`**

- **MAJOR**: Large feature additions or breaking changes (e.g., v1→v2)
- **MINOR**: New features, backward compatible (e.g., v1.0→v1.1)
- **PATCH**: Bug fixes (e.g., v1.0.0→v1.0.1)
- **BUILD**: Auto-incremented by Store on resubmission (e.g., .0, .1, .2)

**Examples:**
- First release: `1.0.0.0`
- Bug fix update: `1.0.1.0`
- Feature update: `1.1.0.0`
- Resubmission after rejection: `1.0.1.1` (build number incremented)

**Application Version Management:**

The release workflow injects the version at build time:

```yaml
# In .github/workflows/release.yml
/p:ApplicationVersion=1.0.0.0
```

This ensures the MSIX package has the correct version metadata.

---

## Post-Submission

### Monitoring Certification Status

1. **In Partner Center**, navigate to your app → **Submissions**
2. **Check status:**
   - 🔵 "In certification" — Being reviewed
   - ✅ "Approved" — Ready to publish (or auto-published)
   - ⚠️ "Held" — Awaiting correction or more info
   - ❌ "Rejected" — Resubmit after fixes

3. **Email notifications:**
   - Certification started
   - Certification completed
   - Rejection with reasons

### Publishing the App

Once approved:

1. **Option A: Auto-publish**
   - Go to **Submission** → **Publish** (if available)
   - App is live immediately after approval

2. **Option B: Manual publish**
   - Go to **Overview**
   - Click **Publish** button
   - Confirm release date/time
   - App goes live at scheduled time

**Tip**: Stagger releases — publish first to a small market segment, then roll out globally.

### Update Process for Published Apps

To release an update:

1. **Increment version** in git tag:
   ```bash
   git tag v1.0.1
   git push origin v1.0.1
   ```

2. **Build new MSIX** via GitHub Actions

3. **Submit new version:**
   - Go to Partner Center → **Submissions** → **Start your submission**
   - Upload new MSIX
   - Update store listing if needed (new screenshots, features, etc.)
   - Fill certification notes
   - Submit

4. **Certification re-runs** for the new version

**Update frequency recommendations:**
- Critical security bugs: Same day
- Important feature updates: Weekly
- Minor improvements: Monthly
- Regular cadence is better than sporadic updates

### Analytics and Crash Reporting

After publishing, monitor app health:

#### Store Analytics
In Partner Center → **Analytics**:

- **Acquisitions**: Download count, source (search, direct, promotion)
- **Usage**: Daily/monthly active users, session duration
- **Health**: Crash rates, ratings, reviews
- **Revenue** (N/A for free apps): Purchase data
- **Reviews**: User ratings and feedback

#### Crash Reporting

Configure Azure Application Insights for telemetry:

```csharp
// In MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder()
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSans"));

        // Add Application Insights (optional)
        // builder.Services.AddApplicationInsights();

        return builder.Build();
    }
}
```

**Recommended telemetry:**
- ✅ Unhandled exception tracking
- ✅ Feature usage metrics
- ❌ **Avoid**: Personally identifiable information
- ❌ **Avoid**: Excessive logging (impacts performance)

### Handling Negative Reviews

When users leave critical feedback:

1. **Respond directly** in Partner Center:
   - Click review → **Reply**
   - Address concerns professionally
   - Offer to help offline

2. **Fix reported issues** in next update
3. **Post release notes** explaining fixes
4. **Monitor ratings** after each update

---

## Checklist: Complete Store Submission

Use this checklist before each submission:

### Pre-Submission (1-2 weeks before)
- [ ] Decide on version number (semantic versioning)
- [ ] Update changelog/release notes
- [ ] Test MSIX locally on Windows
- [ ] Create/update screenshots (4-5 high-quality images)
- [ ] Write compelling description (100-300 words)
- [ ] Review privacy policy for accuracy
- [ ] Verify code signing certificate is valid
- [ ] Complete age ratings questionnaire
- [ ] Test all key features work correctly

### Build & Package
- [ ] Create git tag: `git tag vX.Y.Z`
- [ ] Push tag: `git push origin vX.Y.Z`
- [ ] Monitor GitHub Actions → Release Build
- [ ] Verify MSIX builds without errors
- [ ] Download MSIX artifact
- [ ] Test install locally: `Add-AppxPackage -Path ...`

### Partner Center Submission
- [ ] Log in to Partner Center
- [ ] Create new submission (Update or New App)
- [ ] Upload MSIX package
- [ ] Wait for automated validation (5-10 min)
- [ ] Review validation results
- [ ] Fill app description
- [ ] Add keywords (7 max)
- [ ] Upload screenshots with captions
- [ ] Upload app logo/icons
- [ ] Verify age ratings
- [ ] Add certification notes (if needed)
- [ ] Review all fields one final time
- [ ] Click "Submit for certification"

### Post-Submission
- [ ] Monitor certification progress in Partner Center
- [ ] Read certification reports
- [ ] If rejected: Fix issues and resubmit
- [ ] If approved: Publish manually or auto-publish
- [ ] Update GitHub release notes with Store link
- [ ] Announce release to users

---

## Troubleshooting

### "MSIX validation failed"

**Cause**: Package doesn't meet Store requirements

**Solutions:**
1. Check error message in Partner Center
2. Rebuild with latest .NET 10 SDK
3. Ensure `WindowsPackageType=MSIX` is set
4. Verify manifest is valid XML
5. Redownload from GitHub Actions

### "Code signing failed"

**Cause**: Certificate invalid or password incorrect

**Solutions:**
1. Verify certificate exists in GitHub Secrets
2. Check certificate password is correct
3. Ensure certificate hasn't expired
4. Reissue certificate if > 10 years old

### "App won't launch after Store installation"

**Cause**: Often missing dependencies or corrupted package

**Solutions:**
1. Uninstall: `Get-AppxPackage | Remove-AppxPackage`
2. Clear package cache: `Remove-Item $env:LOCALAPPDATA\Packages\*VideoSplitter* -Recurse`
3. Rebuild locally with `/p:WindowsAppSDKSelfContained=true`
4. Test locally before resubmitting

### "Certification taking too long"

**Cause**: Manual review queue backup or content issues

**Solutions:**
1. Wait 5-7 days (normal max)
2. Check certification notes for issues
3. Contact Store support if > 7 days
4. Don't resubmit duplicate submissions

---

## Resources

- **Partner Center**: [https://partner.microsoft.com/dashboard](https://partner.microsoft.com/dashboard)
- **Microsoft Store Submission**: [https://docs.microsoft.com/windows/apps/publish/](https://docs.microsoft.com/windows/apps/publish/)
- **MSIX Packaging**: [https://docs.microsoft.com/windows/msix/](https://docs.microsoft.com/windows/msix/)
- **Code Signing**: [https://docs.microsoft.com/windows/win32/seccrypto/](https://docs.microsoft.com/windows/win32/seccrypto/)
- **App Manifest**: [https://docs.microsoft.com/uwp/schemas/appxpackage/](https://docs.microsoft.com/uwp/schemas/appxpackage/)
- **MAUI Deployment**: [https://learn.microsoft.com/dotnet/maui/deployment/](https://learn.microsoft.com/dotnet/maui/deployment/)

---

## Support

For issues with Store submission:

1. **Check this guide** — most issues are covered above
2. **Review Partner Center help** — go to Account Settings → Help & Support
3. **Contact Microsoft support** — Partner Center has live chat
4. **Open GitHub issue** — for build/packaging problems

**Contact**: isaac.r.levin@gmail.com
