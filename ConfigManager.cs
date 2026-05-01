using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PhantomOS
{
    public class AppConfig
    {
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        public string EncryptedApiKey { get; set; } = "";
        public string Model { get; set; } = "gpt-4o";
        public string Provider { get; set; } = "openai";
        public string SessionPreset { get; set; } = "interview";
        public double OverlayOpacity { get; set; } = 0.92;
        public int FontSize { get; set; } = 14;
        public string OcrEngine { get; set; } = "tesseract"; // "tesseract" or "windows"

        // Custom Keybindings
        public string KeyCaptureInstant { get; set; } = "OemComma";
        public string KeyCaptureAppend { get; set; } = "Oem4";
        public string KeyCaptureSearch { get; set; } = "Oem6";
        public string KeyAudioToggle { get; set; } = "OemPeriod";
        public string KeyClear { get; set; } = "Delete";
        public string KeyHideToggle { get; set; } = "OemTilde";
        public string KeyReset { get; set; } = "F2";
    }

    public static class ConfigManager
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhantomOS");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

        public static AppConfig Current { get; private set; } = new();

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    Current = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                    
                    // Auto-migrate the old F5 default to F2
                    if (Current.KeyReset == "F5")
                    {
                        Current.KeyReset = "F2";
                        Save(); // Commit the migration
                    }
                }
            }
            catch
            {
                Current = new AppConfig();
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Current, options);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Silent fail — config will use defaults next launch
            }
        }

        public static string GetApiKey()
        {
            if (string.IsNullOrEmpty(Current.EncryptedApiKey)) return "";
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(Current.EncryptedApiKey);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return "";
            }
        }

        public static void SetApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Current.EncryptedApiKey = "";
                return;
            }
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(apiKey);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes, null, DataProtectionScope.CurrentUser);
                Current.EncryptedApiKey = Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                // Fallback: store base64 if DPAPI fails
                Current.EncryptedApiKey = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(apiKey));
            }
        }
    }
}
