using System;
using System.Windows;
using System.Windows.Controls;

namespace PhantomOS
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            // Load Provider
            foreach (ComboBoxItem item in ProviderCombo.Items)
            {
                if (item.Tag?.ToString() == ConfigManager.Current.Provider)
                {
                    ProviderCombo.SelectedItem = item;
                    break;
                }
            }
            if (ProviderCombo.SelectedItem == null)
                ProviderCombo.SelectedIndex = 0;

            UrlBox.Text = ConfigManager.Current.BaseUrl;
            ModelBox.Text = ConfigManager.Current.Model;

            // Load decrypted API key into password box
            string apiKey = ConfigManager.GetApiKey();
            if (!string.IsNullOrEmpty(apiKey))
                ApiKeyBox.Password = apiKey;

            // Select the matching preset in the combo box
            foreach (ComboBoxItem item in PresetCombo.Items)
            {
                if (item.Tag?.ToString() == ConfigManager.Current.SessionPreset)
                {
                    PresetCombo.SelectedItem = item;
                    break;
                }
            }
            if (PresetCombo.SelectedItem == null)
                PresetCombo.SelectedIndex = 0;

            // Load OCR Engine
            foreach (ComboBoxItem item in OcrEngineCombo.Items)
            {
                if (item.Tag?.ToString() == ConfigManager.Current.OcrEngine)
                {
                    OcrEngineCombo.SelectedItem = item;
                    break;
                }
            }
            if (OcrEngineCombo.SelectedItem == null)
                OcrEngineCombo.SelectedIndex = 0;

            // Load Hotkeys
            var keys = Enum.GetValues(typeof(System.Windows.Input.Key));
            KeyInstantCombo.ItemsSource = keys;
            KeyAppendCombo.ItemsSource = keys;
            KeySearchCombo.ItemsSource = keys;
            KeyAudioCombo.ItemsSource = keys;
            KeyClearCombo.ItemsSource = keys;
            KeyHideCombo.ItemsSource = keys;
            KeyResetCombo.ItemsSource = keys;

            KeyInstantCombo.SelectedItem = Enum.TryParse(ConfigManager.Current.KeyCaptureInstant, out System.Windows.Input.Key k1) ? k1 : System.Windows.Input.Key.OemComma;
            KeyAppendCombo.SelectedItem = Enum.TryParse(ConfigManager.Current.KeyCaptureAppend, out System.Windows.Input.Key k2) ? k2 : System.Windows.Input.Key.Oem4;
            KeySearchCombo.SelectedItem = Enum.TryParse(ConfigManager.Current.KeyCaptureSearch, out System.Windows.Input.Key k3) ? k3 : System.Windows.Input.Key.Oem6;
            KeyAudioCombo.SelectedItem = Enum.TryParse(ConfigManager.Current.KeyAudioToggle, out System.Windows.Input.Key k4) ? k4 : System.Windows.Input.Key.OemPeriod;
            KeyClearCombo.SelectedItem = Enum.TryParse(ConfigManager.Current.KeyClear, out System.Windows.Input.Key k5) ? k5 : System.Windows.Input.Key.Delete;
            KeyHideCombo.SelectedItem = Enum.TryParse(ConfigManager.Current.KeyHideToggle, out System.Windows.Input.Key k6) ? k6 : System.Windows.Input.Key.OemTilde;
            KeyResetCombo.SelectedItem = Enum.TryParse(ConfigManager.Current.KeyReset, out System.Windows.Input.Key k7) ? k7 : System.Windows.Input.Key.F2;
        }

        private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProviderCombo.SelectedItem is ComboBoxItem selectedItem && IsLoaded)
            {
                string provider = selectedItem.Tag?.ToString() ?? "openai";
                switch (provider)
                {
                    case "openai":
                        UrlBox.Text = "https://api.openai.com/v1";
                        ModelBox.Text = "gpt-4o";
                        break;
                    case "google":
                        UrlBox.Text = "https://generativelanguage.googleapis.com/v1beta";
                        ModelBox.Text = "gemini-1.5-pro-latest";
                        break;
                    case "anthropic":
                        UrlBox.Text = "https://api.anthropic.com/v1";
                        ModelBox.Text = "claude-3-opus-20240229";
                        break;
                    case "huggingface":
                        UrlBox.Text = "https://api-inference.huggingface.co/models/";
                        ModelBox.Text = "meta-llama/Meta-Llama-3-8B-Instruct";
                        break;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Validate
            string url = UrlBox.Text.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(url))
            {
                System.Windows.MessageBox.Show("Please enter a valid API base URL.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string model = ModelBox.Text.Trim();
            if (string.IsNullOrEmpty(model))
            {
                System.Windows.MessageBox.Show("Please enter a model name (e.g., gpt-4o).",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Save
            if (ProviderCombo.SelectedItem is ComboBoxItem providerItem)
                ConfigManager.Current.Provider = providerItem.Tag?.ToString() ?? "openai";

            ConfigManager.Current.BaseUrl = url;
            ConfigManager.Current.Model = model;

            if (!string.IsNullOrEmpty(ApiKeyBox.Password))
                ConfigManager.SetApiKey(ApiKeyBox.Password);

            if (PresetCombo.SelectedItem is ComboBoxItem selected)
                ConfigManager.Current.SessionPreset = selected.Tag?.ToString() ?? "interview";

            if (OcrEngineCombo.SelectedItem is ComboBoxItem ocrSelected)
                ConfigManager.Current.OcrEngine = ocrSelected.Tag?.ToString() ?? "tesseract";

            ConfigManager.Current.KeyCaptureInstant = KeyInstantCombo.SelectedItem?.ToString() ?? "OemComma";
            ConfigManager.Current.KeyCaptureAppend = KeyAppendCombo.SelectedItem?.ToString() ?? "Oem4";
            ConfigManager.Current.KeyCaptureSearch = KeySearchCombo.SelectedItem?.ToString() ?? "Oem6";
            ConfigManager.Current.KeyAudioToggle = KeyAudioCombo.SelectedItem?.ToString() ?? "OemPeriod";
            ConfigManager.Current.KeyClear = KeyClearCombo.SelectedItem?.ToString() ?? "Delete";
            ConfigManager.Current.KeyHideToggle = KeyHideCombo.SelectedItem?.ToString() ?? "OemTilde";
            ConfigManager.Current.KeyReset = KeyResetCombo.SelectedItem?.ToString() ?? "F2";

            ConfigManager.Save();

            System.Windows.MessageBox.Show("Settings saved and applied.",
                "Phantom-OS", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
