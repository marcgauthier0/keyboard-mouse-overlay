using System;
using System.IO;
using System.Text.Json;

namespace GamingKeypressOverlay.Settings
{
    public class AppSettings
    {
        // Kept for compatibility with older settings files. The UI now uses a
        // single freely customizable color palette instead of named themes.
        public string Style { get; set; } = "Custom";
        public string KeyboardMode { get; set; } = "Full";
        public string MousePosition { get; set; } = "Right";
        public bool ShowMousePosition { get; set; } = true;
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public double WindowWidth { get; set; } = 1400;
        public double WindowHeight { get; set; } = 600;
        public string WindowState { get; set; } = "Normal";
        
        // Performance mode: "Competitive" (gaming) or "Desktop" (normal use)
        public string PerformanceMode { get; set; } = "Competitive";  // "Competitive" or "Desktop"
        
        // Language: "en" (English) or "fr-CA" (French Canada)
        public string Language { get; set; } = "en";  // "en" or "fr-CA"
        
        // Keyboard layout options
        public string KeyboardLayoutType { get; set; } = "QWERTY";  // "QWERTY", "AZERTY", "QWERTZ"
        public string KeyboardSize { get; set; } = "Full";  // "Full", "TKL", "SeventyFive", "SixtyFive", "Sixty"
        public string GameConfig { get; set; } = "General";  // "FPS", "MMO", "MOBA", "General"
        
        // Personalization features are available to everyone.
        public string CustomLogoPath { get; set; } = "";  // Path to custom logo PNG
        public bool UseAnimatedBackground { get; set; } = false;  // Enable animated backgrounds
        
        // Free-form color palette. Values use #RRGGBB.
        public bool UseCustomColors { get; set; } = true;
        public string CustomBackgroundColor { get; set; } = "#101218";
        public string CustomSurfaceColor { get; set; } = "#181C24";
        public string CustomIdleKeyColor { get; set; } = "#2A303B";
        public string CustomPressedKeyColor { get; set; } = "#00D4FF";
        public string CustomTextColor { get; set; } = "#F3F7FA";
        public string CustomPressedTextColor { get; set; } = "#071014";
        public string CustomPrimaryColor { get; set; } = "#00D4FF";
        public string CustomSecondaryColor { get; set; } = "#FF4FA3";
        public string CustomAccentColor { get; set; } = "#8A5CFF";
        
        // Mouse Style
        public string MouseStyle { get; set; } = "Gaming";

        // Background: "Transparent" (OBS/capture) or "Opaque"
        public string BackgroundMode { get; set; } = "Opaque";
    }

    public static class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GamingKeypressOverlay",
            "settings.json");

        public static AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch
            {
                // If loading fails, return default settings
            }

            return new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsPath, json);
                System.Diagnostics.Debug.WriteLine($"SettingsManager.SaveSettings: Successfully saved to {SettingsPath}");
                System.Diagnostics.Debug.WriteLine($"SettingsManager.SaveSettings: Style={settings.Style}, Layout={settings.KeyboardLayoutType}, GameConfig={settings.GameConfig}, MousePos={settings.MousePosition}");
            }
            catch (Exception ex)
            {
                // Log error instead of silently ignoring
                System.Diagnostics.Debug.WriteLine($"SettingsManager.SaveSettings: FAILED to save settings to {SettingsPath}");
                System.Diagnostics.Debug.WriteLine($"SettingsManager.SaveSettings: Error = {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SettingsManager.SaveSettings: StackTrace = {ex.StackTrace}");
                // Re-throw to allow caller to handle if needed
                throw;
            }
        }
    }
}
