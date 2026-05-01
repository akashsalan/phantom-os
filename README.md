<div align="center">

<img src="https://raw.githubusercontent.com/Tarikul-Islam-Anik/Animated-Fluent-Emojis/master/Emojis/Smilies/Ghost.png" alt="Ghost" width="80" />

# Phantom-OS

<a href="https://github.com/PhantomOS"><img src="https://readme-typing-svg.demolab.com?font=Fira+Code&weight=600&size=20&duration=3000&pause=1000&color=00F0FF&center=true&vCenter=true&width=500&lines=The+ultimate+stealth+AI+assistant.;Nail+your+technical+interviews.;Transcribe+meetings+silently.;Code+faster+with+active+vision." alt="Typing SVG" /></a>

[![WPF](https://img.shields.io/badge/WPF-Windows-blue?style=for-the-badge&logo=windows)](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
[![.NET 8](https://img.shields.io/badge/.NET%208-Supported-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.style=for-the-badge)](https://opensource.org/licenses/MIT)

Phantom-OS is an **invisible, high-performance cognitive overlay** for Windows 11. Built to act as a real-time copilot during interviews, coding sessions, and meetings without *ever* being detected by screen-sharing or recording software.

<br>

*(Insert Demo GIF Here: `![Phantom-OS Demo](assets/demo.gif)`)*

</div>

<br>

## 🌟 Key Features

<details>
<summary><b>🥷 True Stealth Mode</b></summary>
<br>
Uses low-level Windows APIs (<code>SetWindowDisplayAffinity</code>) to remain <b>100% invisible</b> to OBS, Zoom, Teams, Google Meet, and any other screen capture tools. Only you can see it.
</details>

<details>
<summary><b>🧠 Bring Your Own LLM</b></summary>
<br>
Native plug-and-play support for OpenAI (GPT-4o), Anthropic (Claude 3.5 Sonnet), Google (Gemini 1.5 Pro), and fully local, offline models via HuggingFace or LM Studio.
</details>

<details>
<summary><b>📷 DPI-Aware Active Vision</b></summary>
<br>
Instantly scrapes and understands code, questions, and UI elements directly from your active screen using a custom Tesseract OCR preprocessing pipeline optimized for monospaced fonts.
</details>

<details>
<summary><b>🎙️ Real-Time Audio Loopback</b></summary>
<br>
Hooks directly into Windows WASAPI to silently record internal system audio, transcribing multi-speaker meetings and spoken interview questions in real time.
</details>

<details>
<summary><b>⚡ Zero-Latency Global Hotkeys</b></summary>
<br>
Control the AI entirely via the keyboard while in-game or mid-interview. You never have to move your mouse or click away from your active window.
</details>

<br>

## 🛠️ Technology Stack

Phantom-OS is built for extreme performance, low resource consumption, and stealth.

* **Core Framework**: `.NET 8` & `WPF` (Windows Presentation Foundation)
* **Screen Processing**: `Tesseract.NET` (LTSM OCR Engine) + Custom EmguCV/GDI+ Pipeline
* **Audio Capture**: `NAudio` (WASAPI Loopback Capture)
* **Markdown Rendering**: `Markdig.Wpf` (Dynamic Dark Mode Syntax Highlighting)
* **System Integration**: Native `user32.dll` P/Invoke (Hooks, Display Affinity)
* **Security**: `System.Security.Cryptography.ProtectedData` (Local DPAPI Encryption)

---

## ⌨️ Global Keybindings

Phantom-OS runs transparently over your desktop. Use these hotkeys to control it without moving your mouse:

| Key | Action | Description |
|-----|--------|-------------|
| **`[`** | **Batch Append** | Snap a piece of the screen and save it to memory (Great for long coding files). |
| **`]`** | **Batch Send** | Send all batched screenshots to the AI at once. |
| **`,`** | **Instant Capture** | Take a single screenshot and instantly ask the AI to solve/explain it. |
| **`.`** | **Audio Toggle** | Start/Stop silent internal audio recording and transcription. |
| **`~`** | **Hide / Show** | Toggle the overlay visibility (content is preserved). |
| **`Ctrl+Alt+C`** | **Copy** | Silently copy the AI's last generated response to your clipboard. |
| **`F2`** | **Reset** | Clear everything and return to the home screen. |
| **`F9`** | **Settings** | Open the configuration panel to change API keys or models. |

---

## 🚀 Installation & Setup

You can install Phantom-OS using the pre-compiled installer, or you can clone and build it yourself from the source code.

### Option A: Install via Windows Installer (Recommended)

We provide a self-contained, single-click Windows Installer. You don't need to install any prerequisites.

1. Navigate to the `Releases` Bar in this repository.
2. Download and run `PhantomOS_Setup.exe`.
3. *(Note: Windows SmartScreen may flag the app since it is a new, unsigned executable utilizing keyboard hooks. Click **More Info -> Run Anyway**).*
4. Once installed, launch the app from your Start Menu.
5. Press **F9** to open settings and configure your preferred LLM Provider and API Key.

<br>

### Option B: Install via Git Clone (For Developers)

Want to run the code directly or modify it yourself? 

**Prerequisites:**
* Windows 10/11
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

**Steps:**
1. Open your terminal and clone the repository:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Phantom-OS.git
   cd Phantom-OS
   ```
2. Run the application using the .NET CLI:
   ```bash
   dotnet run
   ```
3. To package the application into a standalone installer yourself, run the automated PowerShell script (requires [Inno Setup 6](https://jrsoftware.org/isinfo.php)):
   ```powershell
   .\build_installer.ps1
   ```

---

## 📜 License

Distributed under the MIT License. See `LICENSE` for more information.

<br>

<div align="center">
Built with 🖤 by akashsalan
</div>
