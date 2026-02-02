# Setup Transcription

VideoSplitter supports two transcription providers: **Local (Whisper.NET)** for privacy-focused offline processing, and **Azure AI Speech** for cloud-based transcription with enhanced accuracy.

## Choosing a Provider

| Feature | Local (Whisper.NET) | Azure AI Speech |
|---------|---------------------|-----------------|
| **Privacy** | ? All processing on your machine | ? Audio sent to Azure servers |
| **Internet Required** | ? Works offline | ? Requires internet |
| **Speed** | Depends on hardware | Fast, cloud-powered |
| **Cost** | Free | Pay-per-use Azure pricing |
| **Setup Complexity** | Model download required | API key configuration |
| **Best For** | Privacy-conscious users, offline use | Long videos, production workloads |

---

## Option 1: Local Transcription (Whisper.NET)

Local transcription uses OpenAI's Whisper model running entirely on your machine via Whisper.NET.

### Prerequisites

- Sufficient disk space (~150MB for the base model)
- Adequate CPU/RAM (transcription is CPU-intensive)

### Setup Steps

1. **Navigate to Settings**
   - Open the application and go to **Settings**

2. **Select Transcript Provider**
   - In the **Transcript Generation** section, select **Local (Whisper.NET)** from the dropdown

3. **Check Model Status**
   - Click **Check Status** to see if the Whisper model is available
   - The status badge will show:
     - ?? **Model loaded and ready** - Ready to use
     - ?? **Model file available** - Downloaded but not initialized
     - ?? **Model not available** - Download required

4. **Download the Model** (if needed)
   - If the model isn't available, click **Download Model (~142MB)**
   - Wait for the download to complete (progress will be shown)
   - The model is downloaded from HuggingFace to your local machine

5. **Initialize the Model** (optional)
   - Click **Initialize Model** to pre-load it into memory
   - This speeds up the first transcription

6. **Save Settings**
   - Click **Save Settings** at the bottom of the page

### Model Options

The application uses the `ggml-base.bin` model by default, which provides a good balance of speed and accuracy. Available models include:

| Model | Size | Speed | Accuracy |
|-------|------|-------|----------|
| `ggml-tiny.bin` | ~75MB | Fastest | Lower |
| `ggml-base.bin` | ~142MB | Fast | Good |
| `ggml-small.bin` | ~466MB | Slower | Better |

### Troubleshooting Local Transcription

**Model download fails:**
- Check your internet connection
- Ensure you have sufficient disk space
- Try downloading again - the download is resumable

**Transcription is very slow:**
- This is normal for longer videos on older hardware
- Consider using Azure AI Speech for faster processing
- Close other resource-intensive applications

**Poor transcription quality:**
- Ensure the audio quality of your video is good
- Background noise significantly affects accuracy
- Consider using a larger model (requires code modification)

---

## Option 2: Azure AI Speech (Cloud)

Azure AI Speech provides fast, accurate cloud-based transcription with support for multiple languages.

### Prerequisites

- Azure subscription ([Create a free account](https://azure.microsoft.com/free/))
- Azure Speech Service resource

### Creating an Azure Speech Resource

1. **Sign in to Azure Portal**
   - Go to [portal.azure.com](https://portal.azure.com)

2. **Create Speech Resource**
   - Click **Create a resource**
   - Search for **Speech** and select **Speech** from Microsoft
   - Click **Create**

3. **Configure the Resource**
   - **Subscription**: Select your Azure subscription
   - **Resource Group**: Create new or select existing
   - **Region**: Choose a region close to you (e.g., `westus2`, `eastus`)
   - **Name**: Give your resource a unique name
   - **Pricing Tier**: Select **Free F0** (5 hours/month) or **Standard S0**

4. **Get Your API Key and Region**
   - Once created, go to your Speech resource
   - Navigate to **Keys and Endpoint** in the left menu
   - Copy **KEY 1** or **KEY 2** (either works)
   - Note the **Location/Region** (e.g., `westus2`)

### Setup Steps in VideoSplitter

1. **Navigate to Settings**
   - Open the application and go to **Settings**

2. **Select Transcript Provider**
   - In the **Transcript Generation** section, select **Azure AI Speech** from the dropdown

3. **Enter Azure Credentials**
   - **Azure Speech API Key**: Paste your key from the Azure portal
   - **Azure Speech Region**: Enter your region (e.g., `westus2`)

4. **Test the Connection**
   - Click the **Test** button next to the API key field
   - A success message confirms your configuration is correct

5. **Save Settings**
   - Click **Save Settings** at the bottom of the page

### Azure Speech Pricing

| Tier | Price | Included |
|------|-------|----------|
| **Free (F0)** | $0 | 5 hours/month |
| **Standard (S0)** | ~$1/hour | Pay-as-you-go |

For most personal use, the free tier is sufficient.

### Troubleshooting Azure Speech

**Test connection fails:**
- Verify your API key is correct (no extra spaces)
- Confirm the region matches your Azure resource exactly
- Check that your Azure subscription is active

**Transcription produces empty results:**
- Ensure the video has audible speech
- Check that the audio isn't corrupted
- Try with a different video to isolate the issue

**"Quota exceeded" errors:**
- You've exceeded the free tier limit
- Upgrade to Standard tier or wait for the monthly reset

---

## Next Steps

Once transcription is configured, proceed to [Setup Segment Generation](SetupSegmentGeneration.md) to configure AI-powered clip detection.
