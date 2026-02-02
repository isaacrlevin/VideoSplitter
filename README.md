# VideoSplitter Documentation

Welcome to VideoSplitter! This application helps you automatically extract engaging short-form video segments from longer videos using AI-powered analysis.

## Demo

Check out VideoSplitter in action: [Watch Demo on YouTube](https://www.youtube.com/watch?v=rBZvVlglR1k)

## Documentation Index

- [Setup Transcription](docs/SetupTranscription.md) - Configure speech-to-text for your videos
- [Setup Segment Generation](docs/SetupSegmentGeneration.md) - Configure AI-powered segment extraction
- [Setup Social Media Publishing](docs/SetupSocialMediaPublishing.md) - Connect your social media accounts
- [Customizing Prompts](docs/CustomizingPrompts.md) - Fine-tune AI behavior with custom prompts

## Quick Start

1. **Configure Transcription** - Choose between local (Whisper.NET) or cloud-based (Azure Speech) transcription
2. **Configure AI Provider** - Select an LLM provider for intelligent segment detection
3. **Import a Video** - Add your video file to the application
4. **Generate Segments** - Let AI analyze your transcript and suggest the best clips
5. **Export or Publish** - Export clips locally or publish directly to social media

## System Requirements

- Windows 10/11, macOS, or Linux
- .NET 10 Runtime
- FFmpeg (for video processing)
- ~2GB disk space for local Whisper model (if using local transcription)
- Internet connection (for cloud providers)

## Features

- **Multiple Transcription Options**: Local (privacy-focused) or cloud-based (faster for long videos)
- **Multiple AI Providers**: Choose from Ollama (local), OpenAI, Anthropic Claude, Azure OpenAI, or Google Gemini
- **Social Media Integration**: Direct publishing to TikTok, YouTube Shorts, and Instagram Reels
- **Customizable Prompts**: Tailor the AI behavior to your content type
