# Automated Windows Store Submission

This document covers the automated deployment pipeline for submitting VideoSplitter to the Windows Store. For the manual submission process, see [WINDOWS-STORE-SUBMISSION.md](./WINDOWS-STORE-SUBMISSION.md).

## Overview

The automated Store submission workflow (`store-publish.yml`) validates MSIX packages and optionally submits them to Partner Center without manual intervention. This reduces friction and enables rapid iteration.

### Current State

- ✅ **Package validation**: Automated MSIX integrity checks
- ✅ **Safe by default**: Opt-in via explicit flag (never auto-submits)
- ✅ **Dry-run mode**: Test submission logic without hitting the API
- ⚠️ **API integration**: Pending Store API setup (documented below)
- ✅ **Manual fallback**: MSIX always available for manual submission

### Security Posture

- No automatic submission on every release
- Explicit `auto-submit-store=true` flag required
- Dry-run mode for testing without API access
- Service principal approach (not user credentials)
- Proper error handling and notifications

---

## How It Works

### Triggers

The store-publish workflow can be triggered two ways:

#### 1. **Automatic Trigger** (via Release Workflow)

```bash
# Trigger release with auto-submit flag
git tag v1.0.0
git push origin v1.0.0

# Then run with workflow dispatch:
gh workflow run release.yml --ref main -f version=1.0.0 -f auto-submit-store=true
```

#### 2. **Manual Trigger** (Standalone)

```bash
# Manually trigger store submission (e.g., for a previous release)
gh workflow run store-publish.yml \
  -f artifact-run-id=12345 \
  -f dry-run=false
```

Or via GitHub UI:
- Go to **Actions** → **Publish to Windows Store**
- Click **Run workflow**
- Enter the release workflow run ID
- Toggle dry-run if needed

### Validation Pipeline

1. **Artifact Download**: Retrieves MSIX from release workflow
2. **Integrity Check**: Verifies ZIP structure and manifest
3. **Secrets Validation**: Confirms all Azure/Store credentials
4. **Authentication**: Logs in to Azure AD via service principal
5. **Submission**: Uploads to Partner Center (or logs what would be submitted)

### Success Path

```
Release workflow creates MSIX
    ↓
[if auto-submit-store=true]
    ↓
Store-publish workflow triggered
    ↓
Download MSIX from release artifacts
    ↓
Validate package integrity
    ↓
Authenticate to Azure AD
    ↓
Submit to Partner Center (if not dry-run)
    ↓
Monitor certification progress
    ↓
Notify on completion
```

### Dry-Run Mode

For testing the submission workflow **without touching Partner Center**:

```bash
gh workflow run store-publish.yml \
  -f artifact-run-id=12345 \
  -f dry-run=true
```

The workflow will:
- Download the MSIX
- Validate it
- Verify secrets are configured
- Print what _would_ be submitted
- **NOT** make any API calls

Use dry-run to verify your setup works before enabling production submission.

---

## Setup: Azure Service Principal

To enable automated submission, create an Azure AD service principal that Partner Center can recognize.

### Step 1: Create Azure AD App Registration

This represents your automation service to Azure and Partner Center.

1. Go to [Azure Portal](https://portal.azure.com)
2. Search for **App registrations** and click
3. Click **+ New registration**
4. Fill in:
   - **Name**: `VideoSplitter Store Submission` (or your preferred name)
   - **Supported account types**: *Single tenant* (for your organization)
   - **Redirect URI**: Leave blank (not needed for service principal)
5. Click **Register**

6. On the app page, note:
   - **Application ID** (aka Client ID) → Save as `AZURE_CLIENT_ID`
   - **Tenant ID** → Save as `AZURE_TENANT_ID`

### Step 2: Create Client Secret

This is the "password" for the service principal. It's sensitive—treat like a password.

1. In your app registration, go to **Certificates & secrets**
2. Click **+ New client secret**
3. Description: `GitHub Actions Store Submission`
4. Expires: `24 months` (or your preferred duration)
5. Click **Add**

6. **Important**: Copy the secret VALUE immediately (not the ID)
   - Once you navigate away, you cannot recover it
   - Save as `AZURE_CLIENT_SECRET` in GitHub Secrets

### Step 3: Grant Partner Center API Permissions

Azure needs permission to access Partner Center on your behalf.

1. In your app registration, go to **API permissions**
2. Click **+ Add a permission**
3. Search for **Microsoft Partner Center API**
4. Click the Microsoft app
5. Select **Application permissions** (not Delegated)
6. Check:
   - `Submission.ReadWrite.All` (allows create/update submissions)
   - OR `Submission.Read.All` if only reading submission status
7. Click **Add permissions**

8. Click **Grant admin consent for [Your Org]**
   - This allows the app to act on Partner Center without user approval

### Step 4: Link Service Principal to Partner Center

Partner Center needs to know about this Azure app.

1. Go to [Partner Center](https://partner.microsoft.com/dashboard)
2. Navigate to **Account settings** → **Users**
3. Click **Add Azure AD apps**
4. Search for your app by name (`VideoSplitter Store Submission`)
5. Select it and click **Add**
6. Assign roles:
   - ✅ **Developer** — minimum required for submission
   - 📝 Note: Only users with Admin role can manage Azure AD apps

**Alternative**: If you can't add apps, ask your Partner Center admin to do this.

### Step 5: Verify Azure Credentials (Optional)

Test the credentials locally before committing to CI:

```powershell
# Log in with the service principal
$tenantId = "your-tenant-id"
$clientId = "your-client-id"
$clientSecret = ConvertTo-SecureString "your-secret" -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential($clientId, $clientSecret)

Connect-AzAccount -ServicePrincipal -Credential $credential -TenantId $tenantId

# If this succeeds, credentials are valid
```

---

## GitHub Secrets Configuration

Add the following secrets to your GitHub repository:

| Secret Name | Value | Required |
|------------|-------|----------|
| `AZURE_TENANT_ID` | From app registration (Step 1.6) | Yes |
| `AZURE_CLIENT_ID` | From app registration (Step 1.6) | Yes |
| `AZURE_CLIENT_SECRET` | From client secret (Step 2.6) | Yes |
| `STORE_APP_ID` | Your Partner Center app ID | Yes |

### How to Add Secrets

1. Go to GitHub repo → **Settings** → **Secrets and variables** → **Actions**
2. Click **New repository secret**
3. Name: `AZURE_TENANT_ID`
4. Value: Copy from Azure Portal
5. Click **Add secret**
6. Repeat for remaining secrets

### Verifying Secrets Are Set

```bash
# List secrets (doesn't show values, just existence)
gh secret list
```

Expected output:
```
AZURE_CLIENT_ID
AZURE_CLIENT_SECRET
AZURE_TENANT_ID
STORE_APP_ID
```

---

## Store Submission API Integration

The workflow includes placeholders for Store API integration. When you're ready to enable full automation:

### Option 1: Use StoreBroker PowerShell Module

Microsoft provides the StoreBroker module for Partner Center automation.

```powershell
# Install (already done in workflow)
Install-Module -Name StoreBroker -Force

# Create submission and upload package
$submission = New-SubmissionPackage -AppId $appId -PackagePath $msixPath

# Commit submission (marks as ready for certification)
Commit-Submission -AppId $appId -SubmissionId $submissionId

# Check status
Get-SubmissionStatus -AppId $appId -SubmissionId $submissionId
```

**Pros**: Official Microsoft module, well-tested  
**Cons**: Limited documentation, occasional API changes

### Option 2: Direct REST API

Microsoft Store Submission API: https://docs.microsoft.com/windows/uwp/monetize/api/submit-an-app-to-the-microsoft-store

```powershell
# Example: Create submission
$uri = "https://manage.devcenter.microsoft.com/v1.0/my/applications/$appId/submissions"
$headers = @{
    "Authorization" = "Bearer $accessToken"
    "Content-Type" = "application/json"
}
$body = @{ targetPublishMode = "Immediate" } | ConvertTo-Json

$submission = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $body
```

**Pros**: Direct API control, latest features first  
**Cons**: More code, manual token management

### To Enable Full Automation

1. Uncomment the `New-SubmissionPackage` and `Commit-Submission` calls in store-publish.yml
2. Update the API module/code to match your Store configuration
3. Test with dry-run first
4. Set auto-submit-store=true on a test release
5. Monitor Partner Center for submission status

---

## Troubleshooting

### "Secrets not configured" Error

**Symptom**: Workflow fails with "AZURE_TENANT_ID secret not configured"

**Solution**:
1. Verify all 4 secrets are added to GitHub:
   - `AZURE_TENANT_ID`
   - `AZURE_CLIENT_ID`
   - `AZURE_CLIENT_SECRET`
   - `STORE_APP_ID`
2. Check spelling matches exactly (case-sensitive)
3. Ensure secrets are at **Repository** level, not Organization level
4. Wait 5 minutes after adding secrets (sometimes GitHub delays)
5. Re-run the workflow

### "Service principal failed to authenticate"

**Symptom**: Azure AD login fails with "invalid credentials"

**Solution**:
1. Verify `AZURE_CLIENT_SECRET` hasn't expired
2. Regenerate in Azure Portal if > 24 months old
3. Double-check the secret copied (not the ID)
4. Verify `AZURE_TENANT_ID` is correct (not client ID)
5. Test locally:
   ```powershell
   Connect-AzAccount -ServicePrincipal -Credential $cred -TenantId $tenantId
   ```

### "MSIX not found in artifacts"

**Symptom**: Workflow can't locate the MSIX file

**Solution**:
1. Verify the release workflow succeeded first
2. Check release artifacts weren't deleted (90-day retention)
3. Verify correct run ID is passed to store-publish
4. Check artifact naming hasn't changed in release.yml

### "MSIX validation failed"

**Symptom**: "MSIX package integrity verification failed"

**Solution**:
1. The MSIX file may be corrupted
2. Download from release artifacts and test locally:
   ```powershell
   Add-AppxPackage -Path VideoSplitter_1.0.0.0_x64.msix
   ```
3. If install fails, rebuild with:
   ```bash
   git tag v1.0.1
   git push origin v1.0.1
   ```

### "Cannot find app in Partner Center"

**Symptom**: API returns "App not found"

**Solution**:
1. Verify `STORE_APP_ID` is the **numeric ID**, not the app name
2. Find the correct ID in Partner Center → **App overview** → **App identity**
3. Ensure the service principal was added to Partner Center account
4. Check app isn't archived or deleted

---

## Monitoring and Status

### Check Submission Status

In Partner Center:
1. Go to your app → **Submissions**
2. Click the most recent submission
3. Status flow:
   - 🔵 "In certification" — Being processed
   - ✅ "Approved" — Ready to publish
   - ⚠️ "Held" — Awaiting info from you
   - ❌ "Rejected" — Fix and resubmit

### Check Workflow Logs

```bash
# View recent runs
gh workflow view store-publish.yml --limit 5

# View detailed logs for a specific run
gh run view <RUN_ID> --log
```

### Set Up Notifications

The workflow sends status updates via GitHub Actions:

- ✅ Success: "Submission successful" step summary
- ❌ Failure: Job creates issue or sends notification
- 📧 Email: GitHub emails on workflow failure

To also notify Slack/Discord:
- Add webhook in release or store-publish workflow
- Trigger on failure conditions

---

## Best Practices

### Do's ✅

- **Use dry-run before first production submission** — Tests everything without API calls
- **Start with auto-submit-store=false** — Gives you time to review
- **Monitor Partner Center** — Check certification status after submission
- **Keep service principal secrets rotated** — Refresh every 12-24 months
- **Test locally first** — Validate MSIX on Windows before submission
- **Document changes** — Keep notes when updating Store listing

### Don'ts ❌

- **Never commit secrets to Git** — Always use GitHub Secrets
- **Don't share credentials** — Service principal is for CI only
- **Don't auto-submit on unstable builds** — Requires manual opt-in
- **Don't ignore certification failures** — Each failure delays your update
- **Don't submit frequently** — Excessive submissions get flagged
- **Don't modify version after building** — Breaks package signature

---

## Rollback and Recovery

### If Submission is Rejected

1. **Read rejection reason** in Partner Center
2. **Fix the issue** (code, manifest, listing, etc.)
3. **Rebuild with new version**:
   ```bash
   git tag v1.0.1
   git push origin v1.0.1
   ```
4. **Resubmit** — New version will be submitted automatically (if auto-submit enabled)

### If You Need to Abort a Submission

1. Go to Partner Center → **Submissions** → Select submission
2. Click **Certification status** → **Cancel**
3. The submission is removed from certification queue
4. Create new submission with updated package/listing

### If Automated Submission Breaks

1. **Disable auto-submit** — Revert `auto-submit-store=true` to `false`
2. **Investigate logs** — Check GitHub Actions workflow logs
3. **Manual submission** — Download MSIX and submit via Partner Center UI
4. **Fix and re-enable** — Once root cause is addressed

---

## Future Enhancements

Planned improvements to the automation:

- [ ] Publish automatically after certification approval (not just submit)
- [ ] Gradual rollout — Submit to 10%, then 50%, then 100% of markets
- [ ] A/B testing support — Different assets by region
- [ ] Automatic rollback on crash spikes
- [ ] Slack integration for certification notifications
- [ ] Version history and changelogs auto-generated from Git tags
- [ ] Store analytics dashboard in GitHub

---

## Related Documentation

- [Manual Windows Store Submission](./WINDOWS-STORE-SUBMISSION.md) — Step-by-step UI guide
- [GitHub Secrets Configuration](./SECRETS-SETUP.md) — How to add repository secrets
- [CI/CD Pipeline Overview](./CI-CD.md) — Release workflow details
- [Microsoft Store Documentation](https://docs.microsoft.com/windows/apps/publish/) — Official reference

---

## Support

For issues with automated submission:

1. Check this guide's troubleshooting section
2. Review the workflow logs in GitHub Actions
3. Verify Partner Center app health
4. Contact Microsoft Store support (Partner Center → Help)
5. Open a GitHub issue if it's a build/automation problem

**Project Contact**: isaac.r.levin@gmail.com
