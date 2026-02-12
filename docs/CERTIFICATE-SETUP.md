# Certificate Setup for Windows Store Publishing

This guide explains how to create and configure code signing certificates for the CI/CD pipeline.

## Creating a Self-Signed Certificate (Development)

For testing the pipeline without a real certificate:

```powershell
# Create self-signed certificate
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=VideoSplitter Dev Certificate" `
    -KeyUsage DigitalSignature `
    -FriendlyName "VideoSplitter Development" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Export to PFX
$password = ConvertTo-SecureString -String "YourPasswordHere" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "VideoSplitter.pfx" -Password $password

# Convert to Base64 for GitHub Secret
$certBytes = [System.IO.File]::ReadAllBytes("VideoSplitter.pfx")
$certBase64 = [System.Convert]::ToBase64String($certBytes)

# Copy to clipboard
$certBase64 | Set-Clipboard

Write-Host "Base64 certificate copied to clipboard!"
Write-Host "Add this as WINDOWS_CERTIFICATE_BASE64 secret in GitHub"
```

## Using a Production Certificate

For actual Windows Store submission, you need a certificate from one of these sources:

### Option 1: Windows Partner Center Certificate

1. Go to [Windows Partner Center](https://partner.microsoft.com/dashboard)
2. Navigate to your app submission
3. Download the certificate for your app package
4. Convert to Base64 (see below)

### Option 2: Trusted Certificate Authority

Purchase a code signing certificate from:
- DigiCert
- GlobalSign
- Sectigo
- Other trusted CA

Requirements:
- Must support code signing (EKU: 1.3.6.1.5.5.7.3.3)
- Must be in PFX format with private key
- Must be valid (not expired)

## Converting Certificate to Base64

```powershell
# Read certificate file
$certPath = "path\to\your\certificate.pfx"
$certBytes = [System.IO.File]::ReadAllBytes($certPath)

# Convert to Base64
$certBase64 = [System.Convert]::ToBase64String($certBytes)

# Save to file (easier for large certificates)
$certBase64 | Out-File -FilePath "certificate-base64.txt" -Encoding ASCII

# Or copy to clipboard
$certBase64 | Set-Clipboard
```

## Configuring GitHub Secrets

1. Go to your GitHub repository
2. Navigate to: Settings → Secrets and variables → Actions
3. Click "New repository secret"

### Required Secrets:

**WINDOWS_CERTIFICATE_BASE64**
- Paste the Base64-encoded certificate content
- This should be a very long string (thousands of characters)

**CERTIFICATE_PASSWORD**
- The password you used when creating/exporting the PFX
- Keep this secure!

### Optional Secrets:

**CODECOV_TOKEN**
- Token from codecov.io for code coverage uploads
- Only needed if using Codecov integration

## Verifying Certificate Setup

Test locally before pushing to GitHub:

```powershell
# Decode Base64 back to PFX
$certBase64 = Get-Content "certificate-base64.txt" -Raw
$certBytes = [Convert]::FromBase64String($certBase64)
$certPath = "test-certificate.pfx"
[IO.File]::WriteAllBytes($certPath, $certBytes)

# Try to load certificate with password
$password = ConvertTo-SecureString -String "YourPassword" -Force -AsPlainText
$cert = Get-PfxCertificate -FilePath $certPath -Password $password

# Check certificate properties
$cert | Format-List Subject, NotAfter, HasPrivateKey

# Clean up
Remove-Item $certPath
```

Expected output:
```
Subject       : CN=VideoSplitter Dev Certificate
NotAfter      : [Future Date]
HasPrivateKey : True
```

## Security Best Practices

✅ **DO:**
- Store certificates only in GitHub Secrets
- Use strong passwords for PFX files
- Set expiration reminders for certificates
- Rotate certificates before expiration
- Use organization secrets for shared certificates
- Audit who has access to secrets

❌ **DON'T:**
- Commit certificates to repository (even in .gitignore files)
- Share certificate passwords in plain text
- Use same certificate across multiple apps (unless intended)
- Store certificates in cloud storage without encryption
- Use expired or self-signed certificates for production

## Troubleshooting

### "Certificate not valid for code signing"

Check the certificate's Extended Key Usage (EKU):

```powershell
$cert = Get-PfxCertificate -FilePath "certificate.pfx"
$cert.EnhancedKeyUsageList
```

Should include: "Code Signing (1.3.6.1.5.5.7.3.3)"

### "Wrong password"

Verify password:

```powershell
# This should NOT throw an error
$password = ConvertTo-SecureString -String "TestPassword" -Force -AsPlainText
$cert = Get-PfxCertificate -FilePath "certificate.pfx" -Password $password
```

### "Certificate expired"

Check expiration date:

```powershell
$cert = Get-PfxCertificate -FilePath "certificate.pfx"
$cert.NotAfter
```

Renew certificate before expiration date.

### "Base64 decode failed"

Ensure no extra spaces or line breaks in the secret:

```powershell
# Remove whitespace when creating Base64
$certBase64 = [System.Convert]::ToBase64String($certBytes)
# Don't format or wrap the output
```

## Certificate Renewal Process

1. **Before Expiration (30 days):**
   - Obtain new certificate from CA or Partner Center
   - Convert to Base64
   - Test locally

2. **Update Secrets:**
   - Update `WINDOWS_CERTIFICATE_BASE64` with new certificate
   - Update `CERTIFICATE_PASSWORD` if password changed

3. **Verify:**
   - Trigger a manual release build
   - Check that MSIX is signed correctly
   - Verify expiration date in package

4. **Document:**
   - Update certificate expiration date in team docs
   - Set reminder for next renewal

## Resources

- [Create certificates for package signing](https://learn.microsoft.com/windows/msix/package/create-certificate-package-signing)
- [Sign an app package using SignTool](https://learn.microsoft.com/windows/msix/package/sign-app-package-using-signtool)
- [Windows Partner Center](https://partner.microsoft.com/dashboard)
- [GitHub Actions Encrypted Secrets](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
