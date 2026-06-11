# MeetingScribe 🎙️✨

**MeetingScribe** is a high-end, professional AI transcription and meeting productivity suite. It automates the process of recording, transcribing, and summarizing meetings using local AI engines for privacy and cloud-based LLMs for advanced semantic analysis.

## 🎨 Design Philosophy: Lumina Dark
The application follows the **Lumina Dark Production** design system—a modern minimalist aesthetic optimized for focus and technical sophistication.
- **Personality:** "The Silent Expert"—unobtrusive yet powerful.
- **Palette:** Deep charcoal surfaces (#11140D) with high-precision Lime Green accents (#B7E97E) for active AI statuses and Sky Blue (#81CFFF) for user interactions.

## 🚀 Key Features
- **Local ASR (Speech-to-Text):** Powered by `Whisper.net` (Large-v3-Turbo model) for high-accuracy, private transcription.
- **Intelligent VAD:** Integrated `Silero VAD` for precise voice activity detection and natural speech segmentation.
- **Semantic Diarization:** Context-aware speaker identification and cleanup using the Google Gemini API.
- **Audio-Text Sync:** Interactive timeline where clicking text instantly seeks the corresponding audio segment.
- **AI Summary:** Automated generation of action items and meeting minutes based on the meeting agenda.
- **Professional Export:** Export transcripts and summaries to formatted Word documents.

## 🛠 Tech Stack
- **Framework:** Avalonia UI (Cross-platform)
- **Architecture:** MVVM (via `CommunityToolkit.Mvvm`)
- **Audio Engine:** NAudio
- **AI Engines:** Whisper.net (local), Silero VAD (local), Google Gemini API (cloud)
- **Language:** C# 12 / .NET 8+

## 📦 Setup & Installation

### 1. Requirements
- .NET 8.0 SDK or newer.
- Visual Studio 2022 (with Avalonia extension) or JetBrains Rider.
- NVIDIA GPU (optional, for CUDA-accelerated transcription).

### 2. AI Models
To run the application, place the following models into the `/Assets` folder:
1. **Whisper Model:** `ggml-large-v3-turbo-q8_0.bin`
2. **VAD Model:** `silero_vad.onnx`

### 3. API Keys
Obtain a **Gemini API Key** from [Google AI Studio](https://aistudio.google.com/) and add it to the application settings to enable summarization and speaker diarization.

## 📂 Project Structure
- `Services/` — Core logic for Audio, Whisper, and VAD processing.
- `ViewModels/` — UI logic and state management.
- `Views/` — XAML-based UI definitions (Avalonia).
- `Styles/` — Lumina Dark theme, colors, and control templates.
- `Assets/` — Static resources and AI model files.

## 📄 License
Proprietary / In-Development.