# CI/CD Quick Start Checklist

This checklist helps you get the CI/CD pipeline up and running for VideoSplitter.

## ✅ Initial Setup (One-Time)

### 1. GitHub Repository Configuration

- [ ] Repository is hosted on GitHub
- [ ] You have admin access to repository settings
- [ ] GitHub Actions are enabled (Settings → Actions → General)

### 2. Branch Protection (Recommended)

- [ ] Go to Settings → Branches
- [ ] Add branch protection rule for `main`:
  - [ ] Require pull request reviews before merging
  - [ ] Require status checks to pass (select "Build and Test" from PR validation)
  - [ ] Require branches to be up to date before merging

### 3. Configure Secrets (For Release Builds)

**Required for signed MSIX packages:**

- [ ] Create code signing certificate ([See CERTIFICATE-SETUP.md](CERTIFICATE-SETUP.md))
- [ ] Convert certificate to Base64 format
- [ ] Add `WINDOWS_CERTIFICATE_BASE64` secret to GitHub
- [ ] Add `CERTIFICATE_PASSWORD` secret to GitHub

**Optional:**

- [ ] Add `CODECOV_TOKEN` for code coverage reporting (sign up at codecov.io)

📖 **Detailed Guide**: [SECRETS-SETUP.md](SECRETS-SETUP.md)

## 🔄 Daily Development Workflow

### For Contributors

- [ ] Create feature branch from `main`
- [ ] Make code changes
- [ ] Run tests locally: `dotnet test`
- [ ] Push branch to GitHub
- [ ] Create Pull Request
- [ ] Wait for PR validation to pass (automatic)
- [ ] Address any test failures or build errors
- [ ] Get PR review and approval
- [ ] Merge to `main`

**Note**: CI runs automatically on PRs and main branch merges.

## 🚀 Creating a Release

### Pre-Release Checklist

- [ ] All tests passing on main branch
- [ ] Documentation updated (if needed)
- [ ] CHANGELOG updated (if you maintain one)
- [ ] Version number decided (e.g., `1.2.0`)
- [ ] Code signing certificate valid and configured

### Release Process

**Option 1: Git Tag (Recommended)**

```bash
# 1. Ensure you're on main and up to date
git checkout main
git pull origin main

# 2. Create and push tag
git tag v1.2.0
git push origin v1.2.0

# 3. GitHub Actions automatically:
#    - Builds the application
#    - Runs all tests
#    - Creates MSIX package
#    - Creates draft GitHub release
```

**Option 2: Manual Trigger**

- [ ] Go to Actions tab in GitHub
- [ ] Select "Release Build" workflow
- [ ] Click "Run workflow"
- [ ] Enter version number (e.g., `1.2.0`)
- [ ] Click "Run workflow" button

### Post-Release Steps

- [ ] Monitor GitHub Actions for build completion (~10-15 min)
- [ ] Download MSIX package from workflow artifacts
- [ ] Test MSIX package locally (optional but recommended)
- [ ] Edit GitHub release from draft to published
- [ ] Submit MSIX to Windows Store Partner Center

## 🔍 Monitoring and Troubleshooting

### Check Pipeline Status

- [ ] Go to Actions tab in GitHub
- [ ] View recent workflow runs
- [ ] Check for failures (red X) or successes (green checkmark)

### Common Issues and Solutions

**Build fails with "Could not find .NET 10"**
- Wait for GitHub to update hosted runners (weekly updates)
- Or use manual runner with .NET 10 installed

**Tests fail in CI but pass locally**
- Check for timing issues or flaky tests
- Review test logs in Actions artifacts
- Ensure no dependencies on local environment

**MSIX generation fails**
- Verify certificate is correctly configured
- Check certificate expiration date
- Ensure `WindowsPackageType=MSIX` is set

**Release workflow doesn't trigger**
- Verify tag format matches `v*.*.*` (e.g., `v1.0.0`)
- Check repository permissions for GitHub Actions
- Look for workflow errors in Actions tab

📖 **Detailed Troubleshooting**: [CI-CD.md](CI-CD.md#troubleshooting)

## 📊 Build Badges (Optional)

Add status badges to README.md:

```markdown
[![PR Validation](https://github.com/YOUR_USERNAME/VideoSplitter/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/YOUR_USERNAME/VideoSplitter/actions/workflows/pr-validation.yml)

[![CI Build](https://github.com/YOUR_USERNAME/VideoSplitter/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR_USERNAME/VideoSplitter/actions/workflows/ci.yml)
```

Replace `YOUR_USERNAME` with your GitHub username or organization name.

## 📈 Success Criteria

You'll know the CI/CD pipeline is working correctly when:

- ✅ PR validation runs automatically on every pull request
- ✅ All tests pass in CI (43 tests currently)
- ✅ Main branch always stays green (builds successfully)
- ✅ Release tags create MSIX packages automatically
- ✅ Signed packages are ready for Windows Store submission

## 🎯 Next Steps

Once the basic pipeline is working:

- [ ] Set up Codecov integration for coverage tracking
- [ ] Add build status badges to README
- [ ] Configure automated changelog generation
- [ ] Set up branch deployment previews (optional)
- [ ] Configure automated Windows Store submission (advanced)

## 📚 Reference Documentation

- [CI/CD Pipeline Overview](CI-CD.md) - Complete pipeline documentation
- [Certificate Setup](CERTIFICATE-SETUP.md) - Code signing certificates
- [Secrets Configuration](SECRETS-SETUP.md) - GitHub secrets setup
- [Testing Guide](TESTING.md) - Running and writing tests

## 🆘 Getting Help

If you encounter issues:

1. Check the [CI-CD.md](CI-CD.md) troubleshooting section
2. Review workflow logs in GitHub Actions
3. Search [GitHub Actions documentation](https://docs.github.com/actions)
4. Open an issue on the repository
5. Contact: isaac.r.levin@gmail.com

---

**Last Updated**: 2026-02-11  
**Pipeline Version**: 1.0.0  
**Maintainer**: Mike (DevOps)
