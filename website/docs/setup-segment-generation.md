---
title: Setup Segment Generation
layout: page
parent: Documentation
nav_order: 2
---

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
- **Storage**: 4–10GB per model
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

1. **Navigate to Settings** → go to **Settings**
2. **Select LLM Provider** → select **Local (Ollama)**
3. **Check Ollama Status** → click **Check Status** to verify Ollama is running
4. **Select a Model** → choose the model you downloaded from the dropdown
5. **Save Settings** → click **Save Settings**

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
- Adjust prompts (see [Customizing Prompts](customizing-prompts))
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

1. Navigate to **Settings**
2. Select **OpenAI** from the LLM Provider dropdown
3. Paste your API key in the **OpenAI API Key** field
4. Click **Get Models** to fetch available models, or enter one manually (e.g., `gpt-4o-mini`)
5. Click **Test** to verify the configuration
6. Click **Save Settings**

### Recommended Models

| Model | Cost | Best For |
|-------|------|----------|
| `gpt-4o-mini` | Low | Most use cases, great balance |
| `gpt-4o` | Medium | Higher quality analysis |
| `gpt-4-turbo` | Higher | Complex reasoning |

### Pricing

OpenAI charges per token (~4 characters = 1 token). For a typical 30-minute video:
- Approximately $0.01–$0.05 with `gpt-4o-mini`

---

## Option 3: Anthropic Claude

Anthropic's Claude models are known for excellent reasoning and following complex instructions.

### Getting an API Key

1. Visit [console.anthropic.com](https://console.anthropic.com)
2. Sign up or log in → go to **API Keys** → click **Create Key**
3. Copy the key (starts with `sk-ant-`)

### Setup Steps

1. Navigate to **Settings**
2. Select **Anthropic Claude** from the dropdown
3. Paste your API key
4. Click **Get Models** or enter a model name manually (e.g., `claude-sonnet-4-20250514`)
5. Click **Test** then **Save Settings**

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

- Azure subscription with Azure OpenAI access ([apply here](https://aka.ms/oai/access))
- An Azure OpenAI resource with a deployed model

### Setup Steps

1. Create an **Azure OpenAI** resource in [portal.azure.com](https://portal.azure.com)
2. Deploy a model (e.g., `gpt-4o`) in **Model deployments** and note the deployment name
3. Copy your **API Key** and **Endpoint URL** from **Keys and Endpoint**
4. In VideoSplitter **Settings**, select **Azure OpenAI**
5. Enter the **API Key**, **Endpoint URL**, and **Deployment Name**
6. Click **Test** then **Save Settings**

---

## Option 5: Google Gemini

Google Gemini offers competitive quality with a generous free tier.

### Getting an API Key

1. Visit [aistudio.google.com](https://aistudio.google.com)
2. Sign in with your Google account → click **Get API Key**
3. Create a new API key and copy it

### Setup Steps

1. Navigate to **Settings**
2. Select **Google Gemini** from the dropdown
3. Paste your Gemini API key
4. Click **Get Models** or enter a model name (e.g., `gemini-2.5-pro`)
5. Click **Test** then **Save Settings**

### Free Tier

- 60 requests per minute
- Suitable for personal and development use

---

## Segment Generation Settings

In addition to the LLM provider, you can configure:

### Default Segment Length
- Maximum length (in seconds) for each generated segment
- Default: **60 seconds**
- Adjust based on your target platform (TikTok, Reels, Shorts)

### Default Segment Count
- How many segments to generate per video
- Default: **5 segments**
- Increase for longer videos with more potential clips

These settings are used in the AI prompts. See [Customizing Prompts](customizing-prompts) for advanced configuration.

---

## Next Steps

- [Setup Social Media Publishing](setup-social-media) — connect your social media accounts
- [Customizing Prompts](customizing-prompts) — fine-tune segment selection for your content
