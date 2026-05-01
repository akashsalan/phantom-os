using System;
using System.Text;
using System.Windows.Input;
using System.Threading.Tasks;

namespace PhantomOS
{
    public class AppCoordinator
    {
        private readonly MainWindow _window;
        private readonly VisionService _visionService;
        private readonly AudioService _audioService;
        private readonly LLMClient _llmClient;
        private readonly StringBuilder _contextBuffer = new();

        private bool _isProcessing;
        private bool _settingsOpen;

        // Live transcription buffer for chunked audio
        private readonly StringBuilder _liveTranscript = new();

        public AppCoordinator(MainWindow window)
        {
            _window = window;
            _visionService = new VisionService();
            _audioService = new AudioService();
            _llmClient = new LLMClient();

            // Subscribe to real-time audio chunks for live transcription
            _audioService.AudioChunkReady += OnAudioChunkReady;
        }

        /// <summary>
        /// Returns the appropriate user-level instruction based on the active session preset.
        /// The system prompt in LLMClient handles the persona; this handles the per-request framing.
        /// </summary>
        private string GetScreenCaptureInstruction()
        {
            return ConfigManager.Current.SessionPreset switch
            {
                "interview" =>
                    "The text below is scraped from my screen during a live interview. " +
                    "Identify any interview questions, coding problems, system design prompts, or discussion topics visible. " +
                    "Answer each one DIRECTLY using the interview coaching format. " +
                    "If there's a coding problem, provide the solution with complexity analysis. " +
                    "If there's a behavioral question, structure the answer using STAR method. " +
                    "If there are multiple questions visible, answer ALL of them in order. " +
                    "Do NOT describe what's on screen — just answer the questions.",

                "code" =>
                    "The text below is scraped from my screen. " +
                    "Analyze any code, errors, stack traces, or technical documentation visible. " +
                    "If there are bugs, identify them and provide the fix. " +
                    "If there are questions, answer them directly with code examples. " +
                    "Do NOT describe what you see — just solve the problem.",

                "meeting" =>
                    "The text below is scraped from my screen during a meeting. " +
                    "Extract and summarize key discussion points, decisions made, and action items. " +
                    "Organize by topic with clear headers. " +
                    "Highlight any deadlines, owners, or blockers mentioned.",

                _ =>
                    "The text below is scraped from my screen. " +
                    "Do NOT describe what you see. Read the content and directly execute " +
                    "any instructions, solve any problems, or answer any questions you find in it. " +
                    "Just give me the direct answer or solution."
            };
        }

        private async void OnAudioChunkReady(object? sender, AudioChunkEventArgs e)
        {
            try
            {
                string partial = await _llmClient.TranscribeAudioChunk(e.WavData);
                if (!string.IsNullOrWhiteSpace(partial))
                {
                    _liveTranscript.Append(partial).Append(' ');
                    _window.Dispatcher.Invoke(() =>
                    {
                        _window.UpdateOverlayText($"🎙 **Listening...**\n\n> {_liveTranscript.ToString().Trim()}\n\n*Press again to stop & analyze*");
                    });
                }
            }
            catch { /* Chunk transcription failure is non-fatal */ }
        }

        public async void HandleKeyDown(Key key)
        {
            // Suppress hotkeys while settings window is open
            if (_settingsOpen && key != Key.F12) return;

            string keyName = key.ToString();

            // ─── Settings ───
            if (key == Key.F9)
            {
                if (!_settingsOpen)
                {
                    _settingsOpen = true;
                    var settings = new SettingsWindow();
                    settings.Closed += (s, e) =>
                    {
                        _settingsOpen = false;
                        _window.UpdatePresetLabel();
                    };
                    settings.Show();
                }
                return;
            }

            // ─── Reset Overlay (F5 default) ───
            if (keyName == ConfigManager.Current.KeyReset && !_isProcessing)
            {
                _contextBuffer.Clear();
                _liveTranscript.Clear();
                _window.ResetOverlay();
                return;
            }

            // ─── Screen Capture Append [ ───
            if (keyName == ConfigManager.Current.KeyCaptureAppend && !_isProcessing)
            {
                _isProcessing = true;
                _window.SetStatus("processing");
                _window.UpdateOverlayText("📷 Capturing screen chunk...");
                try
                {
                    string ocrText = await _visionService.CaptureAndOCR();
                    if (ocrText.StartsWith("["))
                    {
                        _window.UpdateOverlayText(ocrText);
                        _window.SetStatus("error");
                    }
                    else
                    {
                        _contextBuffer.AppendLine(ocrText);
                        // Prevent context buffer from growing infinitely (limit to ~40,000 chars)
                        if (_contextBuffer.Length > 40000)
                        {
                            string truncated = _contextBuffer.ToString().Substring(_contextBuffer.Length - 40000);
                            _contextBuffer.Clear();
                            _contextBuffer.Append(truncated);
                        }
                        
                        _window.UpdateOverlayText($"📌 **Appended text chunk to batch.**\n\nTotal context: **{_contextBuffer.Length:N0}** characters.");
                        _window.SetStatus("ready");
                    }
                }
                catch (Exception ex)
                {
                    _window.UpdateOverlayText($"⚠ Error: {ex.Message}");
                    _window.SetStatus("error");
                }
                finally
                {
                    _isProcessing = false;
                }
            }
            // ─── Screen Capture Send ] ───
            else if (keyName == ConfigManager.Current.KeyCaptureSearch && !_isProcessing)
            {
                if (_contextBuffer.Length == 0)
                {
                    _window.UpdateOverlayText("⚠ No text batched. Press **`[`** to append text first.");
                    _window.SetStatus("error");
                    return;
                }
                
                _isProcessing = true;
                _window.SetStatus("processing");
                _window.UpdateOverlayText("🔍 Analyzing batched context...");
                try
                {
                    await _llmClient.GetCompletionStreaming(
                        _contextBuffer.ToString(),
                        GetScreenCaptureInstruction(),
                        onChunk: (partialText) =>
                        {
                            _window.Dispatcher.Invoke(() => _window.AppendStreamingText(partialText));
                        },
                        onComplete: (finalText) =>
                        {
                            _window.Dispatcher.Invoke(() =>
                            {
                                _window.UpdateOverlayText(finalText);
                                _window.SetStatus("ready");
                            });
                        }
                    );

                    _contextBuffer.Clear(); // auto-clear after sending
                }
                catch (Exception ex)
                {
                    _window.UpdateOverlayText($"⚠ Error: {ex.Message}");
                    _window.SetStatus("error");
                }
                finally
                {
                    _isProcessing = false;
                }
            }
            // ─── Instant Screen Capture , ───
            else if (keyName == ConfigManager.Current.KeyCaptureInstant && !_isProcessing)
            {
                _isProcessing = true;
                _window.SetStatus("processing");
                _window.UpdateOverlayText("📷 Capturing screen...");
                try
                {
                    string ocrText = await _visionService.CaptureAndOCR();
                    if (ocrText.StartsWith("["))
                    {
                        _window.UpdateOverlayText(ocrText);
                        _window.SetStatus("error");
                    }
                    else
                    {
                        _contextBuffer.AppendLine(ocrText);
                        _window.UpdateOverlayText("🔍 Analyzing captured text...");

                        await _llmClient.GetCompletionStreaming(
                            _contextBuffer.ToString(),
                            GetScreenCaptureInstruction(),
                            onChunk: (partialText) =>
                            {
                                _window.Dispatcher.Invoke(() => _window.AppendStreamingText(partialText));
                            },
                            onComplete: (finalText) =>
                            {
                                _window.Dispatcher.Invoke(() =>
                                {
                                    _window.UpdateOverlayText(finalText);
                                    _window.SetStatus("ready");
                                });
                            }
                        );
                    }
                }
                catch (Exception ex)
                {
                    _window.UpdateOverlayText($"⚠ Error: {ex.Message}");
                    _window.SetStatus("error");
                }
                finally
                {
                    _isProcessing = false;
                }
            }
            // ─── Audio Capture (Toggle) ───
            else if (keyName == ConfigManager.Current.KeyAudioToggle)
            {
                if (!_audioService.IsRecording && !_isProcessing)
                {
                    _liveTranscript.Clear();
                    _window.SetStatus("listening");
                    _window.UpdateOverlayText("🎙 **Listening...** press again to stop & analyze\n\n> _Waiting for audio..._");
                    _audioService.StartRecording();
                    if (!_audioService.IsRecording)
                    {
                        _window.UpdateOverlayText("⚠ Audio capture failed.\nMake sure audio output is active.");
                        _window.SetStatus("error");
                    }
                }
                else if (_audioService.IsRecording)
                {
                    _isProcessing = true;
                    try
                    {
                        _window.SetStatus("processing");
                        _window.UpdateOverlayText("⏳ Processing final audio...");
                        var result = await _audioService.StopRecordingAsync();

                        if (string.IsNullOrEmpty(result.FilePath))
                        {
                            _window.UpdateOverlayText("⚠ No audio captured.");
                            _window.SetStatus("ready");
                            return;
                        }

                        // Process the final chunk if it exists
                        if (result.FinalChunk != null && result.FinalChunk.Length > 100)
                        {
                            string finalPartial = await _llmClient.TranscribeAudioChunk(result.FinalChunk);
                            if (!string.IsNullOrWhiteSpace(finalPartial))
                            {
                                _liveTranscript.Append(finalPartial).Append(' ');
                            }
                        }

                        string transcript = _liveTranscript.Length > 0
                            ? _liveTranscript.ToString().Trim()
                            : await _llmClient.TranscribeAudio(result.FilePath);

                        _window.UpdateOverlayText($"🗣 **\"{transcript}\"**\n\n⏳ Thinking...");

                        await _llmClient.GetCompletionStreaming(
                            _contextBuffer.ToString(),
                            transcript,
                            onChunk: (partialText) =>
                            {
                                _window.Dispatcher.Invoke(() =>
                                    _window.AppendStreamingText($"🗣 **\"{transcript}\"**\n\n{partialText}"));
                            },
                            onComplete: (finalText) =>
                            {
                                _window.Dispatcher.Invoke(() =>
                                {
                                    _window.UpdateOverlayText($"🗣 **\"{transcript}\"**\n\n{finalText}");
                                    _window.SetStatus("ready");
                                });
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        _window.UpdateOverlayText($"⚠ Audio error: {ex.Message}");
                        _window.SetStatus("error");
                    }
                    finally
                    {
                        _isProcessing = false;
                        _liveTranscript.Clear();
                    }
                }
            }
            // ─── Clear Context Buffer (does NOT clear displayed output) ───
            else if ((keyName == ConfigManager.Current.KeyClear || key == Key.Back) && !_isProcessing)
            {
                _contextBuffer.Clear();
                _window.UpdateOverlayText("🗑 **Context buffer cleared.**\nReady for new captures.");
                _window.SetStatus("ready");
            }
            // ─── Toggle Overlay Visibility (preserves content) ───
            else if (keyName == ConfigManager.Current.KeyHideToggle)
            {
                _window.ToggleVisibility();
            }
            // ─── Keyboard Scrolling (since overlay is click-through) ───
            else if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                if (key == Key.Up)
                {
                    _window.ScrollUp();
                }
                else if (key == Key.Down)
                {
                    _window.ScrollDown();
                }
                else if (key == Key.Home)
                {
                    _window.ScrollToTop();
                }
                else if (key == Key.End)
                {
                    _window.ScrollToBottom();
                }
            }
            // ─── Copy AI Response to Clipboard (Ctrl + Alt + C) ───
            else if (key == Key.C && 
                    (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == (ModifierKeys.Control | ModifierKeys.Alt))
            {
                try
                {
                    System.Windows.Clipboard.SetText(_window.LastMarkdown);
                    // Quick flash to indicate success
                    string previous = _window.LastMarkdown;
                    _window.UpdateOverlayText("📋 **Copied to clipboard!**");
                    _window.SetStatus("ready");
                    await Task.Delay(1000);
                    if (_window.LastMarkdown == "📋 **Copied to clipboard!**")
                        _window.UpdateOverlayText(previous);
                }
                catch { }
            }
        }

        public void HandleKeyUp(Key key)
        {
            // Empty because Audio toggling logic has been merged into HandleKeyDown
        }
    }
}
