---
title: Setup Social Media Publishing
layout: page
parent: Documentation
nav_order: 3
---

# Setup Social Media Publishing

VideoSplitter can publish your video segments directly to TikTok, YouTube Shorts, and Instagram Reels. Each platform requires OAuth authentication and has specific setup requirements.

## Platform Overview

| Platform | Account Type Required | API Program | Approval Time |
|----------|-----------------------|-------------|---------------|
| **TikTok** | TikTok account | TikTok Developer Portal | Instant – days |
| **YouTube Shorts** | Google/YouTube account | Google Cloud Console | Instant |
| **Instagram Reels** | Instagram Professional + Facebook Business | Meta Developer Portal | Instant – weeks |

---

## TikTok Setup

### Step 1: Create TikTok Developer App

1. **Visit TikTok Developer Portal** — go to [developers.tiktok.com](https://developers.tiktok.com) and log in
2. **Create an App** — click **Manage apps** → **Create app**; fill in app details
3. **Configure Scopes** — add:
   - `user.info.basic`
   - `video.publish`
   - `video.upload`
4. **Set Redirect URI** — add `http://localhost:8890/callback`
5. **Copy Credentials** — note your **Client Key** and **Client Secret**

### Step 2: Configure in VideoSplitter

1. Go to **Settings** → **Social Media Publishing**
2. Expand **TikTok Settings**
3. Enter your **Client Key** and **Client Secret**
4. Click **Save Settings**

### Step 3: Authenticate

1. Open any project with generated segments
2. Click the TikTok connect button — a browser window opens for login
3. Authorize the application; you'll be redirected back automatically

### TikTok Video Requirements

| Requirement | Value |
|-------------|-------|
| Max Duration | 10 minutes (60s for some accounts) |
| Max File Size | ~287 MB |
| Supported Formats | MP4, WebM, MOV |
| Aspect Ratios | 9:16 (vertical), 1:1, 16:9 |

### Troubleshooting TikTok

**"Invalid client key" error:** Verify your Client Key and ensure you're using the correct app credentials.

**OAuth callback fails:** Confirm `http://localhost:8890/callback` is listed in your app's redirect URIs and no firewall is blocking port 8890.

**Upload fails:** Verify the video meets TikTok's requirements and your account isn't restricted.

---

## YouTube Shorts Setup

### Step 1: Create Google Cloud Project

1. Go to [console.cloud.google.com](https://console.cloud.google.com) — create or select a project
2. Enable **YouTube Data API v3** — go to **APIs & Services** → **Library**
3. Configure **OAuth Consent Screen** — choose **External**, fill in app details, add scopes:
   - `https://www.googleapis.com/auth/youtube.upload`
   - `https://www.googleapis.com/auth/youtube`
4. Add your email as a **test user**
5. Create **OAuth Credentials** — go to **Credentials** → **Create Credentials** → **OAuth client ID** (Desktop app)
6. Copy your **Client ID** and **Client Secret**

### Step 2: Configure in VideoSplitter

1. Go to **Settings** → **Social Media Publishing** → **YouTube Settings**
2. Enter your **Client ID** and **Client Secret**
3. Click **Save Settings**

### Step 3: Authenticate

1. Open any project with generated segments
2. Click the YouTube connect button
3. Sign in with Google and grant the requested permissions

### YouTube Shorts Requirements

| Requirement | Value |
|-------------|-------|
| Max Duration | 180 seconds (3 minutes) for Shorts |
| Recommended Duration | 60 seconds or less |
| Max File Size | 256 GB (general YouTube limit) |
| Supported Formats | MP4, MOV, AVI, WMV, FLV, 3GP, WebM, MKV |
| Aspect Ratio | 9:16 (vertical) for Shorts; 1:1 also works |

> **Tip:** For a video to be classified as a YouTube Short, it should be vertical (9:16) and under 60 seconds, or include `#Shorts` in the title or description.

### Troubleshooting YouTube

**"Access blocked" during OAuth:** Add your Google account as a test user in the OAuth consent screen, or submit the app for verification.

**Upload succeeds but video is private:** Newly uploaded videos default to private. They may also need processing time before appearing.

**"Quota exceeded" error:** YouTube API has daily quotas (~10,000 units/day). Wait 24 hours or request a quota increase in Google Cloud Console.

---

## Instagram Reels Setup

Instagram Reels publishing requires the most complex setup, using the Instagram Graph API through Facebook.

### Prerequisites

- **Instagram Professional Account** (Business or Creator)
- **Facebook Page** connected to your Instagram account
- **Meta Developer Account**

### Step 1: Convert to Professional Account

1. In Instagram, go to **Settings** → **Account** → **Switch to Professional Account**
2. Connect your Instagram to a Facebook Page (**Settings** → **Account** → **Sharing to other apps**)

### Step 2: Create Meta Developer App

1. Go to [developers.facebook.com](https://developers.facebook.com) and log in
2. Click **My Apps** → **Create App** → **Other** → **Business**
3. Add **Instagram Graph API** product to your app
4. Request permissions: `instagram_basic`, `instagram_content_publish`, `pages_show_list`, `pages_read_engagement`
5. Add redirect URI `http://localhost:8890/callback` to **Facebook Login** settings
6. Copy your **App ID** and **App Secret** from **Settings** → **Basic**

### Step 3: Configure in VideoSplitter

1. Go to **Settings** → **Social Media Publishing** → **Instagram Settings**
2. Enter your **App ID** and **App Secret**
3. Click **Save Settings**

### Step 4: Authenticate

1. Open any project with generated segments
2. Click the Instagram connect button
3. Log in with Facebook, grant permissions, and select the Facebook Page connected to your Instagram

### Instagram Reels Requirements

| Requirement | Value |
|-------------|-------|
| Max Duration | 90 seconds |
| Recommended Duration | 15–30 seconds for best engagement |
| Max File Size | 1 GB |
| Supported Formats | MP4, MOV |
| Aspect Ratio | 9:16 (vertical) recommended; 4:5 to 1.91:1 supported |

### Troubleshooting Instagram

**"App not authorized":** Ensure all required permissions are approved and your Instagram is linked to a Facebook Page.

**Can't find Instagram account:** Verify it's a Professional account and the Facebook Page is correctly linked.

**"Permission denied" errors:** Submit your app for App Review on Meta Developer Portal, or keep using Development mode (limited to test users).

---

## General Publishing Notes

### Token Refresh

OAuth tokens expire. VideoSplitter handles automatic refresh when possible, but you may occasionally need to re-authenticate.

### Rate Limits

| Platform | Limit |
|----------|-------|
| TikTok | Limited uploads per day (varies by account status) |
| YouTube | ~10,000 quota units/day; uploads cost ~1,600 units each |
| Instagram | Varies by account age and standing |

### Best Practices

1. **Test with shorter videos first** to verify your setup
2. **Keep credentials secure** — don't share API keys
3. **Monitor for errors** — check platform-specific error messages
4. **Respect platform guidelines** — ensure content complies with each platform's terms of service

---

## Next Steps

- [Customizing Prompts](customizing-prompts) — optimize segment selection for your content
- [Home]({{ site.baseurl }}/) — return to the documentation home
