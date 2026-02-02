# Setup Segment Generation

VideoSplitter uses Large Language Models (LLMs) to intelligently analyze your video transcripts and identify the most engaging segments. You can choose between local AI (Ollama) for privacy or cloud-based providers for enhanced capabilities.

## Choosing an LLM Provider

| Provider | Privacy | Cost | Quality | Setup |
|----------|---------|------|---------|-------|
| **Ollama (Local)** | On-device | Free | Good with right model | Install Ollama + download model |
| **OpenAI** | Cloud | Pay-per-use | Excellent | API key only |
| **Anthropic Claude** | Cloud | Pay-per-use | Excellent | API key only |
| **Azure OpenAI** | Cloud | Pay-per-use | Excellent | Azure resource + deployment |
| **Google Gemini** | Cloud | Pay-per-use / Free tier | Excellent | API key only |

---

## Option 1: Local AI with Ollama

Ollama allows you to run LLMs locally on your machine, keeping all data private.

### Prerequisites

- **Hardware**: Modern CPU or GPU with at least 8GB RAM (16GB+ recommended)
- **Storage**: 4-10GB per model
- **Ollama**: Installed and running

### Installing Ollama

1. **Download Ollama**
   - Visit [ollama.com](https://ollama.com)
   - Download the installer for your operating system
   - Run the installer

2. **Verify Installation**
   - Open a terminal/command prompt
   - Run: `ollama --version`
   - You should see version information

3. **Start Ollama Service**
   - Ollama runs as a background service automatically on most systems
   - If needed, run: `ollama serve`

### Downloading a Model

Choose a model based on your hardware and needs:

| Model | Size | RAM Needed | Quality | Speed |
|-------|------|------------|---------|-------|
| `llama3.2:3b` | ~2GB | 8GB | Good | Fast |
| `llama3.1:8b` | ~4.5GB | 16GB | Better | Medium |
| `mistral` | ~4GB | 16GB | Good | Fast |
| `qwen2.5:7b` | ~4GB | 16GB | Good | Medium |
| `deepseek-r1:8b` | ~5GB | 16GB | Excellent | Slower |

Download a model using the terminal:

```bash
# Recommended for most users
ollama pull llama3.2:3b

# For better quality (requires more RAM)
ollama pull llama3.1:8b

# Alternative options
ollama pull mistral
ollama pull qwen2.5:7b
```

### Setup Steps in VideoSplitter

1. **Navigate to Settings**
   - Open the application and go to **Settings**

2. **Select LLM Provider**
   - In the **AI Segment Generation** section, select **Local (Ollama)**

3. **Check Ollama Status**
   - Click **Check Status** to verify Ollama is running
   - The status badge shows:
     - ?? **Running** - Ollama is ready
     - ?? **Not Running** - Start Ollama service

4. **Select a Model**
   - Once running, your downloaded models appear in the dropdown
   - Select the model you want to use

5. **Save Settings**
   - Click **Save Settings**

### Troubleshooting Ollama

**"Not Running" status:**
- Open terminal and run `ollama serve`
- Check if port 11434 is blocked by firewall
- Restart the Ollama service

**Model not appearing:**
- Ensure the model download completed successfully
- Run `ollama list` in terminal to see downloaded models
- Re-pull the model if needed: `ollama pull model-name`

**Poor segment quality:**
- Try a larger model if hardware permits
- Adjust prompts (see [Customizing Prompts](CustomizingPrompts.md))
- Ensure transcript quality is good

---

## Option 2: OpenAI

OpenAI provides high-quality models like GPT-4 and GPT-4o-mini.

### Getting an API Key

1. Visit [platform.openai.com](https://platform.openai.com)
2. Sign up or log in
3. Go to **API Keys** in the left menu
4. Click **Create new secret key**
5. Copy the key immediately (it won't be shown again)

### Setup Steps

1. **Navigate to Settings**
   - Open the application and go to **Settings**

2. **Select LLM Provider**
   - Choose **OpenAI** from the dropdown

3. **Enter API Key**
   - Paste your API key in the **OpenAI API Key** field

4. **Load Available Models**
   - Click **Get Models** to fetch your available models
   - Or manually enter a model name (e.g., `gpt-4o-mini`)

5. **Test Connection**
   - Click **Test** to verify the configuration
   - A success message confirms everything works

6. **Save Settings**
   - Click **Save Settings**

### Recommended Models

| Model | Cost | Best For |
|-------|------|----------|
| `gpt-4o-mini` | Low | Most use cases, great balance |
| `gpt-4o` | Medium | Higher quality analysis |
| `gpt-4-turbo` | Higher | Complex reasoning |

### Pricing

OpenAI charges per token (roughly 4 characters = 1 token). For typical video segment generation:
- A 30-minute video transcript: ~$0.01-0.05 with gpt-4o-mini
- Costs vary based on transcript length and model choice

---

## Option 3: Anthropic Claude

Anthropic's Claude models are known for excellent reasoning and following instructions.

### Getting an API Key

1. Visit [console.anthropic.com](https://console.anthropic.com)
2. Sign up or log in
3. Go to **API Keys**
4. Click **Create Key**
5. Copy the key

### Setup Steps

1. **Navigate to Settings**
   - Open the application and go to **Settings**

2. **Select LLM Provider**
   - Choose **Anthropic Claude** from the dropdown

3. **Enter API Key**
   - Paste your API key (starts with `sk-ant-`)

4. **Load Available Models**
   - Click **Get Models** to fetch available models
   - Or manually enter: `claude-sonnet-4-20250514`

5. **Test and Save**
   - Click **Test** to verify
   - Click **Save Settings**

### Recommended Models

| Model | Cost | Best For |
|-------|------|----------|
| `claude-3-haiku-20240307` | Low | Fast, simple tasks |
| `claude-sonnet-4-20250514` | Medium | Best balance |
| `claude-3-opus-20240229` | Higher | Maximum quality |

---

## Option 4: Azure OpenAI

Azure OpenAI is ideal for enterprise users who need Azure compliance and security.

### Prerequisites

- Azure subscription
- Azure OpenAI resource with approved access
- Deployed model

### Creating Azure OpenAI Resource

1. **Request Access**
   - Azure OpenAI requires approval
   - Apply at [aka.ms/oai/access](https://aka.ms/oai/access)

2. **Create Resource**
   - Go to [portal.azure.com](https://portal.azure.com)
   - Create an **Azure OpenAI** resource

3. **Deploy a Model**
   - In your Azure OpenAI resource, go to **Model deployments**
   - Click **Create new deployment**
   - Select a model (e.g., `gpt-4o`) and give it a name
   - Note the **deployment name**

4. **Get Credentials**
   - Go to **Keys and Endpoint**
   - Copy **KEY 1** and the **Endpoint URL**

### Setup Steps

1. **Navigate to Settings**
   - Open the application and go to **Settings**

2. **Select LLM Provider**
   - Choose **Azure OpenAI** from the dropdown

3. **Enter Credentials**
   - **Azure OpenAI API Key**: Your key from Azure
   - **Azure OpenAI Endpoint**: The full endpoint URL (e.g., `https://your-resource.openai.azure.com/`)
   - **Deployment Name**: The name you gave your deployment

4. **Test and Save**
   - Click **Test** to verify
   - Click **Save Settings**

---

## Option 5: Google Gemini

Google Gemini offers competitive quality with a generous free tier.

### Getting an API Key

1. Visit [aistudio.google.com](https://aistudio.google.com)
2. Sign in with your Google account
3. Click **Get API Key**
4. Create a new API key or use an existing one
5. Copy the key

### Setup Steps

1. **Navigate to Settings**
   - Open the application and go to **Settings**

2. **Select LLM Provider**
   - Choose **Google Gemini** from the dropdown

3. **Enter API Key**
   - Paste your Gemini API key

4. **Load Available Models**
   - Click **Get Models** to see available options
   - Or manually enter: `gemini-2.5-pro`

5. **Test and Save**
   - Click **Test** to verify
   - Click **Save Settings**

### Free Tier

Google Gemini offers a generous free tier:
- 60 requests per minute
- Suitable for personal and development use

---

## Segment Generation Settings

In addition to the LLM provider, you can configure:

### Default Segment Length
- The maximum length (in seconds) for each generated segment
- Default: **60 seconds**
- Adjust based on your target platform (TikTok, Reels, Shorts)

### Default Segment Count
- How many segments to generate per video
- Default: **5 segments**
- Increase for longer videos with more potential clips

These settings are used in the AI prompts. See [Customizing Prompts](CustomizingPrompts.md) for advanced configuration.

---

## Next Steps

- [Setup Social Media Publishing](SetupSocialMediaPublishing.md) to connect your accounts
- [Customizing Prompts](CustomizingPrompts.md) to fine-tune segment selection
