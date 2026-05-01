using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using System.Windows.Documents;

namespace PhantomOS
{
    public partial class MainWindow : Window
    {
        private KeyboardHook? _keyboardHook;
        private AppCoordinator? _coordinator;
        private bool _overlayVisible = true;
        private string _lastMarkdown = "";
        public string LastMarkdown => _lastMarkdown;
        private Storyboard? _pulseStoryboard;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Load persisted configuration
            ConfigManager.Load();
            UpdatePresetLabel();

            // Wire up coordinator and global keyboard hook
            _coordinator = new AppCoordinator(this);
            _keyboardHook = new KeyboardHook();

            _keyboardHook.KeyDown += (s, key) =>
                Dispatcher.Invoke(() => _coordinator.HandleKeyDown(key));
            _keyboardHook.KeyUp += (s, key) =>
                Dispatcher.Invoke(() => _coordinator.HandleKeyUp(key));

            InjectMarkdownDarkStyles();

            // Cache pulse storyboard
            _pulseStoryboard = (Storyboard)FindResource("PulseAnimation");

            // Show the premium welcome screen
            UpdateOverlayText(BuildWelcomeScreen());
        }

        private string BuildWelcomeScreen()
        {
            string m1 = GetReadableKey(ConfigManager.Current.KeyCaptureInstant);
            string m2 = GetReadableKey(ConfigManager.Current.KeyCaptureAppend);
            string m3 = GetReadableKey(ConfigManager.Current.KeyCaptureSearch);
            string m4 = GetReadableKey(ConfigManager.Current.KeyAudioToggle);
            string m5 = GetReadableKey(ConfigManager.Current.KeyHideToggle);
            string m6 = GetReadableKey(ConfigManager.Current.KeyReset);
            string m7 = GetReadableKey(ConfigManager.Current.KeyClear);

            string presetName = ConfigManager.Current.SessionPreset switch
            {
                "interview" => "Interview Companion",
                "code" => "Code & Debug",
                "meeting" => "Meeting Assistant",
                _ => "General Assistant"
            };

            string provider = ConfigManager.Current.Provider switch
            {
                "openai" => "OpenAI",
                "anthropic" => "Anthropic",
                "google" => "Google Gemini",
                "huggingface" => "HuggingFace",
                "custom" => "Custom",
                _ => ConfigManager.Current.Provider
            };

            return $@"# Phantom-OS

> Stealth AI overlay — invisible to screen capture & recording

---

### Active Configuration

| Setting | Value |
|---------|-------|
| **Mode** | {presetName} |
| **Provider** | {provider} |
| **Model** | `{ConfigManager.Current.Model}` |

---

### Screen Capture

| Key | Action | Description |
|-----|--------|-------------|
| **`{m1}`** | **Instant Capture** | Screenshot → OCR → AI analysis in one shot |
| **`{m2}`** | **Batch Append** | Silently scrape screen text into memory buffer |
| **`{m3}`** | **Batch Send** | Send all buffered text to AI for analysis |

### Voice & Audio

| Key | Action | Description |
|-----|--------|-------------|
| **`{m4}`** | **Toggle Audio** | Start/stop system audio capture with live transcription |

### Interface Controls

| Key | Action | Description |
|-----|--------|-------------|
| **`{m5}`** | **Hide / Show** | Toggle overlay visibility (content preserved) |
| **`{m6}`** | **Reset** | Clear everything and return to this screen |
| **`{m7}`** | **Clear Buffer** | Erase captured text from memory |
| **`F9`** | **Settings** | Open configuration panel |
| **`Ctrl+Alt+C`** | **Copy** | Copy the last AI response to clipboard |

### Navigation

| Key | Action | Description |
|-----|--------|-------------|
| **`Ctrl+↑`** | **Scroll Up** | Scroll overlay content up |
| **`Ctrl+↓`** | **Scroll Down** | Scroll overlay content down |
| **`Ctrl+Home`** | **Jump to Top** | Scroll to the top of the content |
| **`Ctrl+End`** | **Jump to Bottom** | Scroll to the bottom of the content |

---

*Phantom is running in stealth mode. This overlay is invisible to all screen capture, recording, and sharing software.*";
        }

        private string GetReadableKey(string keyName) => keyName switch {
            "OemComma" => ",", "OemPeriod" => ".", "Oem4" => "[", "Oem6" => "]", "OemTilde" => "`", "OemMinus" => "-", "OemPlus" => "+", "OemQuestion" => "?", _ => keyName
        };

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            // Stealth: exclude from screen capture / screen sharing
            Win32Interop.SetWindowDisplayAffinity(hwnd, Win32Interop.WDA_EXCLUDEFROMCAPTURE);

            // Make click-through + hide from Alt+Tab
            int extStyle = Win32Interop.GetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE);
            Win32Interop.SetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE,
                extStyle
                | Win32Interop.WS_EX_LAYERED
                | Win32Interop.WS_EX_TRANSPARENT
                | Win32Interop.WS_EX_TOOLWINDOW);
        }

        /// <summary>
        /// Toggles the overlay panel between visible and hidden with animation.
        /// Content is always preserved.
        /// </summary>
        public void ToggleVisibility()
        {
            _overlayVisible = !_overlayVisible;
            Dispatcher.Invoke(() =>
            {
                if (_overlayVisible)
                {
                    OverlayPanel.Visibility = Visibility.Visible;
                    AnimateShow();
                }
                else
                {
                    AnimateHide();
                }
            });
        }

        private void AnimateShow()
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var slideIn = new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleIn = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            OverlayPanel.BeginAnimation(OpacityProperty, fadeIn);
            OverlayTranslate.BeginAnimation(TranslateTransform.YProperty, slideIn);
            OverlayScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            OverlayScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
        }

        private void AnimateHide()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var slideOut = new DoubleAnimation(0, 18, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var scaleOut = new DoubleAnimation(1, 0.97, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                OverlayPanel.Visibility = Visibility.Collapsed;
            };

            OverlayPanel.BeginAnimation(OpacityProperty, fadeOut);
            OverlayTranslate.BeginAnimation(TranslateTransform.YProperty, slideOut);
            OverlayScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
            OverlayScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
        }

        /// <summary>
        /// Updates the main response text in the overlay. Preserves content for toggle.
        /// Auto-scrolls to bottom so latest content is visible.
        /// </summary>
        public void UpdateOverlayText(string text)
        {
            _lastMarkdown = text;
            Dispatcher.Invoke(() =>
            {
                OverlayMarkdown.Markdown = text;
                // Auto-scroll to bottom so user sees the latest content
                ResponseScroller.ScrollToEnd();
            });
        }

        /// <summary>
        /// Appends streaming text to the overlay (replaces full markdown each time for rendering).
        /// Auto-scrolls to bottom during streaming.
        /// </summary>
        public void AppendStreamingText(string fullTextSoFar)
        {
            _lastMarkdown = fullTextSoFar;
            Dispatcher.Invoke(() =>
            {
                OverlayMarkdown.Markdown = fullTextSoFar;
                ResponseScroller.ScrollToEnd();
            });
        }

        // ─── Keyboard Scroll Methods ───
        // Since the overlay is click-through (WS_EX_TRANSPARENT), mouse wheel events
        // pass to the app behind. These methods let users scroll via keyboard instead.

        private const double ScrollStep = 60.0;

        /// <summary>
        /// Scrolls the overlay content up by one step.
        /// </summary>
        public void ScrollUp()
        {
            Dispatcher.Invoke(() =>
            {
                ResponseScroller.ScrollToVerticalOffset(
                    Math.Max(0, ResponseScroller.VerticalOffset - ScrollStep));
            });
        }

        /// <summary>
        /// Scrolls the overlay content down by one step.
        /// </summary>
        public void ScrollDown()
        {
            Dispatcher.Invoke(() =>
            {
                ResponseScroller.ScrollToVerticalOffset(
                    ResponseScroller.VerticalOffset + ScrollStep);
            });
        }

        /// <summary>
        /// Scrolls to the very top of the overlay content.
        /// </summary>
        public void ScrollToTop()
        {
            Dispatcher.Invoke(() =>
            {
                ResponseScroller.ScrollToHome();
            });
        }

        /// <summary>
        /// Scrolls to the very bottom of the overlay content.
        /// </summary>
        public void ScrollToBottom()
        {
            Dispatcher.Invoke(() =>
            {
                ResponseScroller.ScrollToEnd();
            });
        }

        /// <summary>
        /// Resets the overlay to welcome screen. Used by the dedicated reset key.
        /// </summary>
        public void ResetOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateOverlayText(BuildWelcomeScreen());
                SetStatus("ready");
            });
        }

        private void InjectMarkdownDarkStyles()
        {
            // Dynamically build and inject dark theme styles for Markdig.Wpf components bypassing XAML parser limitations
            try
            {
                var mdStylesType = typeof(Markdig.Wpf.Styles);

                // CodeBlock Style
                var codeBlockKey = new ComponentResourceKey(mdStylesType, "CodeBlockStyleKey");
                var codeBlockStyle = new Style(typeof(Paragraph));
                codeBlockStyle.Setters.Add(new Setter(Paragraph.BackgroundProperty, new SolidColorBrush(WpfColor.FromRgb(14, 14, 24))));
                codeBlockStyle.Setters.Add(new Setter(Paragraph.ForegroundProperty, new SolidColorBrush(WpfColor.FromRgb(200, 212, 230))));
                codeBlockStyle.Setters.Add(new Setter(Paragraph.BorderBrushProperty, new SolidColorBrush(WpfColor.FromRgb(40, 40, 60))));
                codeBlockStyle.Setters.Add(new Setter(Paragraph.BorderThicknessProperty, new Thickness(1)));
                codeBlockStyle.Setters.Add(new Setter(Paragraph.PaddingProperty, new Thickness(14)));
                codeBlockStyle.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0, 6, 0, 6)));
                codeBlockStyle.Setters.Add(new Setter(Paragraph.FontFamilyProperty, new System.Windows.Media.FontFamily("Cascadia Code, Consolas, monospace")));
                codeBlockStyle.Setters.Add(new Setter(Paragraph.FontSizeProperty, 12.5));
                this.Resources[codeBlockKey] = codeBlockStyle;

                // Inline Code Style
                var codeKey = new ComponentResourceKey(mdStylesType, "CodeStyleKey");
                var codeStyle = new Style(typeof(Run));
                codeStyle.Setters.Add(new Setter(Run.BackgroundProperty, new SolidColorBrush(WpfColor.FromRgb(30, 30, 48))));
                codeStyle.Setters.Add(new Setter(Run.ForegroundProperty, new SolidColorBrush(WpfColor.FromRgb(255, 185, 120))));
                codeStyle.Setters.Add(new Setter(Run.FontFamilyProperty, new System.Windows.Media.FontFamily("Cascadia Code, Consolas, monospace")));
                this.Resources[codeKey] = codeStyle;

                // Heading1 Style
                var h1Key = new ComponentResourceKey(mdStylesType, "Heading1StyleKey");
                var h1Style = new Style(typeof(Paragraph));
                h1Style.Setters.Add(new Setter(Paragraph.ForegroundProperty, new SolidColorBrush(WpfColor.FromRgb(235, 240, 255))));
                h1Style.Setters.Add(new Setter(Paragraph.FontSizeProperty, 22.0));
                h1Style.Setters.Add(new Setter(Paragraph.FontWeightProperty, FontWeights.Bold));
                h1Style.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0, 0, 0, 4)));
                h1Style.Setters.Add(new Setter(Paragraph.FontFamilyProperty, new System.Windows.Media.FontFamily("Segoe UI")));
                this.Resources[h1Key] = h1Style;

                // Heading2 Style
                var h2Key = new ComponentResourceKey(mdStylesType, "Heading2StyleKey");
                var h2Style = new Style(typeof(Paragraph));
                h2Style.Setters.Add(new Setter(Paragraph.ForegroundProperty, new SolidColorBrush(WpfColor.FromRgb(205, 215, 245))));
                h2Style.Setters.Add(new Setter(Paragraph.FontSizeProperty, 17.0));
                h2Style.Setters.Add(new Setter(Paragraph.FontWeightProperty, FontWeights.SemiBold));
                h2Style.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0, 6, 0, 4)));
                this.Resources[h2Key] = h2Style;

                // Heading3 Style
                var h3Key = new ComponentResourceKey(mdStylesType, "Heading3StyleKey");
                var h3Style = new Style(typeof(Paragraph));
                h3Style.Setters.Add(new Setter(Paragraph.ForegroundProperty, new SolidColorBrush(WpfColor.FromRgb(175, 185, 215))));
                h3Style.Setters.Add(new Setter(Paragraph.FontSizeProperty, 15.0));
                h3Style.Setters.Add(new Setter(Paragraph.FontWeightProperty, FontWeights.SemiBold));
                h3Style.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0, 8, 0, 4)));
                this.Resources[h3Key] = h3Style;

                // Table Cell Style — if available
                try
                {
                    var tableCellKey = new ComponentResourceKey(mdStylesType, "TableCellStyleKey");
                    var tableCellStyle = new Style(typeof(TableCell));
                    tableCellStyle.Setters.Add(new Setter(TableCell.PaddingProperty, new Thickness(8, 4, 8, 4)));
                    tableCellStyle.Setters.Add(new Setter(TableCell.BorderBrushProperty, new SolidColorBrush(WpfColor.FromRgb(40, 40, 55))));
                    tableCellStyle.Setters.Add(new Setter(TableCell.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
                    this.Resources[tableCellKey] = tableCellStyle;
                }
                catch { }

                // Blockquote Style
                try
                {
                    var quoteKey = new ComponentResourceKey(mdStylesType, "QuoteBlockStyleKey");
                    var quoteStyle = new Style(typeof(Section));
                    quoteStyle.Setters.Add(new Setter(Section.BorderBrushProperty, new SolidColorBrush(WpfColor.FromRgb(80, 80, 140))));
                    quoteStyle.Setters.Add(new Setter(Section.BorderThicknessProperty, new Thickness(3, 0, 0, 0)));
                    quoteStyle.Setters.Add(new Setter(Section.PaddingProperty, new Thickness(14, 6, 0, 6)));
                    quoteStyle.Setters.Add(new Setter(Section.MarginProperty, new Thickness(0, 6, 0, 6)));
                    this.Resources[quoteKey] = quoteStyle;
                }
                catch { }
            }
            catch { }
        }

        /// <summary>
        /// Updates the status dot color and controls the pulse animation.
        /// </summary>
        public void SetStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                var color = status switch
                {
                    "ready"      => WpfColor.FromRgb(0, 221, 119),
                    "processing" => WpfColor.FromRgb(255, 187, 0),
                    "error"      => WpfColor.FromRgb(255, 68, 68),
                    "listening"  => WpfColor.FromRgb(100, 180, 255),
                    _            => WpfColor.FromRgb(0, 221, 119)
                };

                StatusDot.Fill = new SolidColorBrush(color);

                // Update glow ring color to match
                try
                {
                    StatusGlow.Fill = new RadialGradientBrush(color, Colors.Transparent);
                }
                catch { }

                // Control pulse animation
                try
                {
                    if (status == "processing" || status == "listening")
                    {
                        _pulseStoryboard?.Begin(this, true);
                    }
                    else
                    {
                        _pulseStoryboard?.Stop(this);
                        StatusDot.Opacity = 1.0;
                    }
                }
                catch { /* storyboard may not be loaded yet */ }
            });
        }

        /// <summary>
        /// Updates the session preset label in the header.
        /// </summary>
        public void UpdatePresetLabel()
        {
            Dispatcher.Invoke(() =>
            {
                PresetLabel.Text = ConfigManager.Current.SessionPreset switch
                {
                    "interview" => " · INTERVIEW",
                    "code" => " · CODE",
                    "meeting" => " · MEETING",
                    _ => " · READY"
                };
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            _keyboardHook?.Dispose();
            base.OnClosed(e);
        }
    }
}
