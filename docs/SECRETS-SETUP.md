# GitHub Secrets Configuration

This file documents the GitHub Secrets required for the CI/CD pipeline to function properly.

## Required for Release Builds

These secrets are needed to create signed MSIX packages for Windows Store submission:

| Secret Name | Purpose | How to Obtain | Required For |
|------------|---------|---------------|--------------|
| `WINDOWS_CERTIFICATE_BASE64` | Code signing certificate in Base64 format | See [CERTIFICATE-SETUP.md](CERTIFICATE-SETUP.md) | Release workflow |
| `CERTIFICATE_PASSWORD` | Password for the PFX certificate | Password used when creating/exporting the certificate | Release workflow |

## Required for Automated Store Submission

These secrets enable the Store publication workflow to automatically submit to Partner Center:

| Secret Name | Purpose | How to Obtain | Required For |
|------------|---------|---------------|--------------|
| `AZURE_TENANT_ID` | Azure AD tenant ID | Azure Portal → App registrations → Directory ID | store-publish workflow |
| `AZURE_CLIENT_ID` | Azure AD application ID | Azure Portal → App registrations → Application ID | store-publish workflow |
| `AZURE_CLIENT_SECRET` | Azure AD client secret | Azure Portal → App registrations → Certificates & secrets | store-publish workflow |
| `STORE_APP_ID` | Windows Store app ID | Partner Center → App overview → App identity → Product ID | store-publish workflow |

**Note**: These are only required if you want automated Store submission. Without them, the workflow will validate packages but not submit them (you can manually submit via Partner Center).

See [WINDOWS-STORE-AUTOMATION.md](./WINDOWS-STORE-AUTOMATION.md) for detailed setup instructions.

## Optional Secrets

These secrets enhance the pipeline but are not required:

| Secret Name | Purpose | How to Obtain | Required For |
|------------|---------|---------------|--------------|
| `CODECOV_TOKEN` | Upload code coverage reports to Codecov | Sign up at [codecov.io](https://codecov.io) and get token | CI workflow (optional) |

## How to Add Secrets

1. Navigate to your GitHub repository
2. Go to: **Settings** → **Secrets and variables** → **Actions**
3. Click **"New repository secret"**
4. Enter the secret name (exactly as shown above)
5. Paste the secret value
6. Click **"Add secret"**

## Testing Without Secrets

The CI/CD pipeline is designed to work without secrets for development:

- **PR Validation**: No secrets required (builds and tests only)
- **CI Builds**: No secrets required (builds, tests, artifacts)
- **Release Builds**: Works without secrets but produces unsigned packages
  - Unsigned packages can be used for testing
  - Windows Store submission requires signed packages

## Verifying Secret Configuration

After adding secrets:

1. Go to **Actions** tab in GitHub
2. Manually trigger the **Release Build** workflow
3. Check the workflow logs:
   - If certificate configured: "Decode signing certificate" step succeeds
   - If certificate missing: Build continues but package is unsigned

## Security Notes

⚠️ **IMPORTANT:**
- Never commit secrets to the repository
- Never share secrets in plain text (Slack, email, etc.)
- Rotate secrets periodically
- Audit who has access to repository secrets
- Use environment-specific secrets if needed (dev/staging/prod)

## Secret Management Best Practices

1. **Use strong passwords** for certificates
2. **Document expiration dates** for certificates
3. **Set calendar reminders** 30 days before certificate expiration
4. **Test secret rotation** in a non-production environment first
5. **Use organization secrets** for secrets shared across repositories
6. **Enable secret scanning** in GitHub Security settings

## Troubleshooting

### "Secret not found" error

- Verify secret name matches exactly (case-sensitive)
- Check secret is added at repository level, not environment level
- Ensure workflow has permission to access secrets

### "Invalid certificate" error

- Verify Base64 encoding is correct
- Check certificate is in PFX format
- Ensure certificate has not expired
- Verify password matches certificate

### Build succeeds but package is unsigned

This is expected when certificates are not configured. To sign packages:
1. Add `WINDOWS_CERTIFICATE_BASE64` secret
2. Add `CERTIFICATE_PASSWORD` secret
3. Re-run the release workflow

## Certificate Renewal Checklist

When renewing code signing certificates:

- [ ] Obtain new certificate 30 days before expiration
- [ ] Convert new certificate to Base64 format
- [ ] Test new certificate locally
- [ ] Update `WINDOWS_CERTIFICATE_BASE64` secret
- [ ] Update `CERTIFICATE_PASSWORD` secret (if changed)
- [ ] Trigger test release build
- [ ] Verify MSIX package is signed correctly
- [ ] Document new expiration date
- [ ] Set reminder for next renewal

## For Contributors

If you're a contributor without access to organization secrets:

- PR validation and CI builds work without any secrets
- Release builds will create unsigned packages
- Maintainers will handle signed releases
- Focus on code quality; CI will validate your changes

## For Maintainers

As a maintainer with secret management access:

1. **Regular Audits**: Review who has access quarterly
2. **Rotation Policy**: Rotate passwords annually minimum
3. **Certificate Monitoring**: Track expiration dates
4. **Backup**: Keep encrypted backup of certificates
5. **Documentation**: Update this file when secrets change

## Related Documentation

- [CI/CD Pipeline Overview](CI-CD.md)
- [Windows Store Automated Submission](WINDOWS-STORE-AUTOMATION.md)
- [GitHub Encrypted Secrets Documentation](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
