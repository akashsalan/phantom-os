# Phantom-OS System Architecture

This document provides a high-level overview of the technical architecture of Phantom-OS to help contributors navigate the codebase.

## Overview

Phantom-OS is a WPF (Windows Presentation Foundation) desktop application targeting .NET 8. It relies heavily on low-level Windows APIs (P/Invoke) to bypass standard window management and input loops, enabling its core "stealth" functionality.

The application is structured into three primary processing domains:
1. **The Overlay UI Layer** (`MainWindow.xaml`)
2. **The Global Input Hook Layer** (`AppCoordinator.cs`)
3. **The Sensor & Processing Services** (`VisionService`, `AudioService`, `LLMClient`)

---

## 1. The Overlay UI Layer

**File:** `MainWindow.xaml` / `MainWindow.xaml.cs`

*   **Transparency & Stealth:** The window is configured with `AllowsTransparency="True"`, `WindowStyle="None"`, and `Topmost="True"`. 
*   **Click-Through (WS_EX_TRANSPARENT):** We modify the extended window style using `SetWindowLong` to pass all mouse clicks *through* the UI to the applications beneath it. 
*   **Display Affinity (WDA_EXCLUDEFROMCAPTURE):** This is the most critical stealth component. Using `SetWindowDisplayAffinity`, the window is instructed by the OS kernel to be invisible to any software trying to capture the screen (OBS, Zoom, WebRTC, Snipping Tool).

## 2. The Global Input Hook Layer

**File:** `AppCoordinator.cs`

Because the app is "click-through" and often doesn't have active focus, standard WPF key bindings will not work. 
*   We use a **Global Low-Level Keyboard Hook** (`WH_KEYBOARD_LL` / `SetWindowsHookEx`) to intercept keystrokes system-wide.
*   When a designated hotkey is detected (e.g., `[`, `]`, `,`), the hook intercepts the key, blocks it from reaching the foreground application, and triggers the internal state machine.
*   The `AppCoordinator` manages the queue of captured screenshots, the audio transcription state, and the running Context Buffer (rolling window of 40k characters) to prevent OOM errors.

## 3. The Sensor & Processing Services

### Vision Processing (`VisionService.cs`)
*   When triggered, the app grabs the bounds of the active monitor using `Screen.FromPoint(Cursor.Position)`.
*   A `Bitmap` of the screen is captured and processed through a contrast/grayscale filter to maximize text readability.
*   The image is fed into `Tesseract.NET` (or gracefully falls back to `Windows.Media.Ocr` if language packs are available) to extract raw text or code.

### Audio Loopback (`AudioService.cs`)
*   Uses `NAudio` with `WasapiLoopbackCapture`. This records the *internal output* of the sound card (what the user hears in their headphones), bypassing the need for a microphone.
*   The raw `.wav` byte stream is chunked in memory.
*   When the user stops the recording (`.`), the chunks are flushed and sent to the LLM for speech-to-text transcription.

### AI Integration (`LLMClient.cs`)
*   Acts as the central router for OpenAI, Anthropic, Google Gemini, and HuggingFace API formats.
*   Handles streaming Server-Sent Events (SSE) so the `MainWindow` can aggressively type out the response in real-time, just like ChatGPT.
*   Uses `HttpClient` and is entirely stateless.

---

## Security & Storage (`ConfigManager.cs`)

*   **Zero Cloud Footprint:** No telemetry or keys are sent to any central Phantom-OS server.
*   **DPAPI Encryption:** The user's API keys are encrypted at rest using `ProtectedData.Protect` (Windows Data Protection API). Only the Windows User Profile that encrypted the keys can decrypt them.
*   **Config Location:** `%AppData%\PhantomOS\config.json`
