# Setup Social Media Publishing

VideoSplitter can publish your video segments directly to TikTok, YouTube Shorts, and Instagram Reels. Each platform requires OAuth authentication and has specific setup requirements.

## Platform Overview

| Platform | Account Type Required | API Program | Approval Time |
|----------|----------------------|-------------|---------------|
| **TikTok** | TikTok account | TikTok Developer Portal | Instant - days |
| **YouTube Shorts** | Google/YouTube account | Google Cloud Console | Instant |
| **Instagram Reels** | Instagram Professional + Facebook Business | Meta Developer Portal | Instant - weeks |

---

## TikTok Setup

### Prerequisites

- TikTok account
- TikTok Developer Portal access

### Step 1: Create TikTok Developer App

1. **Visit TikTok Developer Portal**
   - Go to [developers.tiktok.com](https://developers.tiktok.com)
   - Log in with your TikTok account

2. **Create an App**
   - Click **Manage apps** ? **Create app**
   - Fill in app details:
     - **App name**: e.g., "VideoSplitter"
     - **Description**: Brief description of your use case
     - **Category**: Select appropriate category

3. **Configure Scopes**
   - In your app settings, add the following scopes:
     - `user.info.basic` - Basic user info
     - `video.publish` - Publish videos
     - `video.upload` - Upload video content

4. **Set Redirect URI**
   - Add redirect URI: `http://localhost:8890/callback`
   - This is the local callback URL VideoSplitter uses

5. **Get Credentials**
   - Note your **Client Key** and **Client Secret**
   - These are found in your app's settings

### Step 2: Configure in VideoSplitter

1. **Navigate to Settings**
   - Go to Settings and find the **Social Media Publishing** section

2. **Expand TikTok Settings**
   - Enter your **Client Key**
   - Enter your **Client Secret**

3. **Save Settings**
   - Click **Save Settings**

### Step 3: Authenticate

1. **Go to a Project**
   - Open any project with generated segments

2. **Connect TikTok**
   - Click the TikTok authentication/connect button
   - A browser window opens for TikTok login
   - Authorize the application
   - You'll be redirected back to VideoSplitter

### TikTok Video Requirements

| Requirement | Value |
|-------------|-------|
| Max Duration | 10 minutes (60s for some accounts) |
| Max File Size | ~287 MB |
| Supported Formats | MP4, WebM, MOV |
| Aspect Ratios | 9:16 (vertical), 1:1, 16:9 |

### Troubleshooting TikTok

**"Invalid client key" error:**
- Verify your Client Key is correct
- Ensure you're using credentials from the correct app

**OAuth callback fails:**
- Check that `http://localhost:8890/callback` is in your app's redirect URIs
- Ensure no firewall is blocking port 8890

**Upload fails:**
- Verify video meets TikTok's requirements
- Check your TikTok account isn't restricted

---

## YouTube Shorts Setup

### Prerequisites

- Google account with YouTube channel
- Google Cloud Console project

### Step 1: Create Google Cloud Project

1. **Go to Google Cloud Console**
   - Visit [console.cloud.google.com](https://console.cloud.google.com)
   - Create a new project or select an existing one

2. **Enable YouTube Data API**
   - Go to **APIs & Services** ? **Library**
   - Search for "YouTube Data API v3"
   - Click **Enable**

3. **Configure OAuth Consent Screen**
   - Go to **APIs & Services** ? **OAuth consent screen**
   - Choose **External** (or Internal for Workspace)
   - Fill in app information:
     - **App name**: "VideoSplitter"
     - **User support email**: Your email
     - **Developer contact**: Your email
   - Add scopes:
     - `https://www.googleapis.com/auth/youtube.upload`
     - `https://www.googleapis.com/auth/youtube`
   - Add your email as a test user (while in testing mode)

4. **Create OAuth Credentials**
   - Go to **APIs & Services** ? **Credentials**
   - Click **Create Credentials** ? **OAuth client ID**
   - Application type: **Desktop app**
   - Name: "VideoSplitter"
   - Click **Create**
   - Download or copy the **Client ID** and **Client Secret**

### Step 2: Configure in VideoSplitter

1. **Navigate to Settings**
   - Go to Settings ? **Social Media Publishing** section

2. **Expand YouTube Settings**
   - Enter your **Client ID**
   - Enter your **Client Secret**

3. **Save Settings**
   - Click **Save Settings**

### Step 3: Authenticate

1. **Go to a Project**
   - Open any project with generated segments

2. **Connect YouTube**
   - Click the YouTube authentication button
   - Sign in with your Google account
   - Grant the requested permissions
   - You'll be redirected back to VideoSplitter

### YouTube Shorts Requirements

| Requirement | Value |
|-------------|-------|
| Max Duration | 180 seconds (3 minutes) for Shorts |
| Recommended Duration | 60 seconds or less |
| Max File Size | 256 GB (general YouTube limit) |
| Supported Formats | MP4, MOV, AVI, WMV, FLV, 3GP, WebM, MKV |
| Aspect Ratio | 9:16 (vertical) for Shorts, 1:1 also works |

> **Note**: For a video to be classified as a YouTube Short, it should be vertical (9:16) and under 60 seconds, or have `#Shorts` in the title/description.

### Troubleshooting YouTube

**"Access blocked" during OAuth:**
- Your app is in testing mode - add your Google account as a test user
- Or submit your app for verification

**Upload succeeds but video is private:**
- Newly uploaded videos default to private
- Videos may need processing time before they appear

**"Quota exceeded" error:**
- YouTube API has daily quotas
- Wait 24 hours or request quota increase in Google Cloud Console

---

## Instagram Reels Setup

Instagram Reels publishing requires the most complex setup as it uses the Instagram Graph API through Facebook.

### Prerequisites

- **Instagram Professional Account** (Business or Creator)
- **Facebook Page** connected to your Instagram account
- **Meta Developer Account**

### Step 1: Convert to Professional Account

1. **Open Instagram App**
   - Go to Settings ? Account ? Switch to Professional Account
   - Choose **Business** or **Creator**
   - Complete the setup

2. **Connect to Facebook Page**
   - In Instagram settings, go to **Account** ? **Sharing to other apps**
   - Connect your Instagram to a Facebook Page you manage

### Step 2: Create Meta Developer App

1. **Go to Meta Developer Portal**
   - Visit [developers.facebook.com](https://developers.facebook.com)
   - Log in with your Facebook account

2. **Create an App**
   - Click **My Apps** ? **Create App**
   - Select **Other** use case
   - Select **Business** app type
   - Fill in app details

3. **Add Instagram Graph API**
   - In your app dashboard, go to **Add Products**
   - Find **Instagram Graph API** and click **Set Up**

4. **Configure Permissions**
   - Go to **App Review** ? **Permissions and Features**
   - Request the following permissions:
     - `instagram_basic`
     - `instagram_content_publish`
     - `pages_show_list`
     - `pages_read_engagement`

5. **Set Redirect URI**
   - Go to **Facebook Login** ? **Settings**
   - Add redirect URI: `http://localhost:8890/callback`

6. **Get Credentials**
   - Go to **Settings** ? **Basic**
   - Copy your **App ID** and **App Secret**

### Step 3: Configure in VideoSplitter

1. **Navigate to Settings**
   - Go to Settings ? **Social Media Publishing** section

2. **Expand Instagram Settings**
   - Enter your **App ID**
   - Enter your **App Secret**

3. **Save Settings**
   - Click **Save Settings**

### Step 4: Authenticate

1. **Go to a Project**
   - Open any project with generated segments

2. **Connect Instagram**
   - Click the Instagram authentication button
   - Log in with Facebook (which connects to your Instagram)
   - Grant the requested permissions
   - Select the Facebook Page connected to your Instagram

### Instagram Reels Requirements

| Requirement | Value |
|-------------|-------|
| Max Duration | 90 seconds |
| Recommended Duration | 15-30 seconds for best engagement |
| Max File Size | 1 GB |
| Supported Formats | MP4, MOV |
| Aspect Ratio | 9:16 (vertical) recommended, 4:5 to 1.91:1 supported |

### Troubleshooting Instagram

**"App not authorized" error:**
- Ensure all required permissions are approved
- Check that your Instagram is linked to a Facebook Page

**Can't find Instagram account:**
- Verify Instagram is a Professional account (not personal)
- Confirm the Facebook Page is correctly linked

**Upload fails:**
- Video must be publicly accessible URL for Instagram API
- VideoSplitter handles this automatically, but network issues can cause failures

**"Permission denied" errors:**
- Submit your app for App Review on Meta Developer Portal
- Or use your app in Development mode (limited to test users)

---

## General Publishing Notes

### Video Processing

VideoSplitter automatically:
1. Extracts the selected segment from your video
2. Converts it to the correct format if needed
3. Validates it meets platform requirements
4. Uploads to the selected platform

### Token Refresh

OAuth tokens expire. VideoSplitter handles automatic refresh when possible, but you may occasionally need to re-authenticate.

### Rate Limits

All platforms have rate limits:
- **TikTok**: Limited uploads per day (varies by account status)
- **YouTube**: ~10,000 quota units/day (uploads cost ~1,600 units each)
- **Instagram**: Rate limits vary by account age and standing

### Best Practices

1. **Test with shorter videos first** to verify your setup
2. **Keep credentials secure** - don't share API keys
3. **Monitor for errors** - check platform-specific error messages
4. **Respect platform guidelines** - ensure content complies with terms of service

---

## Next Steps

- [Customizing Prompts](CustomizingPrompts.md) to optimize segment selection for your content
- Return to [Documentation Index](README.md)
