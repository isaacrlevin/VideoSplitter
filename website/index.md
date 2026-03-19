---
title: Home
layout: home
nav_order: 1
---

# VideoSplitter

**AI-powered video segment extraction for short-form content creators.**

VideoSplitter automatically analyzes your long-form videos, transcribes them, and uses AI to identify the most engaging clips — ready to publish to TikTok, YouTube Shorts, and Instagram Reels.

[Watch Demo on YouTube](https://www.youtube.com/watch?v=rBZvVlglR1k){: .btn .btn-primary .fs-5 .mb-4 .mb-md-0 .mr-2 }
[Get Started](docs/setup-transcription){: .btn .fs-5 .mb-4 .mb-md-0 }

---

## Features

- **Multiple Transcription Options** — Local (Whisper.NET, privacy-focused) or cloud-based (Azure AI Speech, faster for long videos)
- **Multiple AI Providers** — Ollama (local), OpenAI, Anthropic Claude, Azure OpenAI, or Google Gemini
- **Social Media Integration** — Direct publishing to TikTok, YouTube Shorts, and Instagram Reels
- **Customizable Prompts** — Tailor AI behavior for your specific content type

---

## Quick Start

1. **Configure Transcription** — Choose between local (Whisper.NET) or cloud-based (Azure Speech) transcription
2. **Configure AI Provider** — Select an LLM provider for intelligent segment detection
3. **Import a Video** — Add your video file to the application
4. **Generate Segments** — Let AI analyze your transcript and suggest the best clips
5. **Export or Publish** — Export clips locally or publish directly to social media

---

## System Requirements

| Requirement | Details |
|-------------|---------|
| **OS** | Windows 10/11, macOS, or Linux |
| **Runtime** | .NET 10 |
| **Video Processing** | FFmpeg (must be in system PATH) |
| **Disk Space** | ~2 GB for local Whisper model (optional) |
| **Network** | Internet connection required for cloud providers |

---

## Documentation

| Guide | Description |
|-------|-------------|
| [Setup Transcription](docs/setup-transcription) | Configure speech-to-text for your videos |
| [Setup Segment Generation](docs/setup-segment-generation) | Configure AI-powered segment extraction |
| [Setup Social Media Publishing](docs/setup-social-media) | Connect your social media accounts |
| [Customizing Prompts](docs/customizing-prompts) | Fine-tune AI behavior with custom prompts |

---

## Legal

- [Privacy Policy](privacy-policy) — How we handle your data
- [Terms of Service](terms-of-service) — Terms for using VideoSplitter
