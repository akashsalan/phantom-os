# Contributing to Phantom-OS

First off, thank you for considering contributing to Phantom-OS! It's people like you that make Phantom-OS such a great open-source tool. 

## 1. Where do I go from here?

If you've noticed a bug or have a feature request, please make sure to **open an issue** before writing any code. If you'd like to tackle an existing issue, please leave a comment on it so others know you are working on it.

## 2. Fork & Create a Branch

1. Fork the repository on GitHub.
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Phantom-OS.git
   cd Phantom-OS
   ```
3. Create a new branch for your feature or bugfix:
   ```bash
   git checkout -b feature/your-awesome-feature
   ```

## 3. Development Setup

Phantom-OS is built using **.NET 8** and **WPF**. 

*   You must have the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed.
*   We recommend using **Visual Studio 2022** or **JetBrains Rider** for the best WPF debugging experience, but VS Code works fine as well.
*   Ensure that you are building for the `x64` platform architecture, as the low-level hooks (`user32.dll`) and Tesseract OCR depend on bitness.

## 4. Coding Guidelines

*   **Keep it stealthy:** Any UI components added should respect the `WindowStyle="None"` and `SetWindowDisplayAffinity` settings to ensure the overlay remains invisible.
*   **Performance first:** The app uses global hooks. Any code inside `HandleKeyDown` or the Vision processing pipeline must be extremely fast or safely offloaded to a background task so it doesn't freeze the user's OS.
*   **Documentation:** Please add XML comments (`///`) to any complex public methods or low-level P/Invoke signatures you add.

## 5. Make your Changes & Commit

When you are ready, commit your changes. Please use clear and descriptive commit messages.

```bash
git commit -m "feat: Add support for custom local Ollama models"
```

## 6. Push & Pull Request

1. Push your branch to your GitHub fork:
   ```bash
   git push origin feature/your-awesome-feature
   ```
2. Open a Pull Request from your fork into the `main` branch of the official Phantom-OS repository.
3. In the PR description, explain *what* you changed and *why*. If it fixes an open issue, link to it (e.g., "Fixes #12").

---

Thank you for helping democratize AI tools! 👻
