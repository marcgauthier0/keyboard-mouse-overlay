using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using GamingKeypressOverlay.Settings;

namespace GamingKeypressOverlay.Overlay
{
    // A single internal style remains for compatibility with renderer APIs.
    // Users configure a palette directly; there are no named theme presets.
    public enum OverlayStyle
    {
        Custom
    }

    public class OverlayTheme
    {
        public Brush BackgroundBrush { get; set; } = new SolidBrush(Color.Transparent);
        public Brush KeyboardBackground { get; set; } = new SolidBrush(Color.Black);
        public Brush KeyboardBorder { get; set; } = new SolidBrush(Color.Cyan);
        public Brush MouseBackground { get; set; } = new SolidBrush(Color.Black);
        public Brush MouseBorder { get; set; } = new SolidBrush(Color.Magenta);
        public Brush ActiveKeysBackground { get; set; } = new SolidBrush(Color.Black);
        public Brush ActiveKeysBorder { get; set; } = new SolidBrush(Color.Purple);
        public SolidBrush PrimaryColor { get; set; } = new SolidBrush(Color.Cyan);
        public SolidBrush SecondaryColor { get; set; } = new SolidBrush(Color.Magenta);
        public SolidBrush AccentColor { get; set; } = new SolidBrush(Color.Purple);
        public Brush LastKeyGradient { get; set; } = new SolidBrush(Color.White);
        public Brush KeyBorder { get; set; } = new SolidBrush(Color.Gray);
        public Brush KeyIdleBackground { get; set; } = new SolidBrush(Color.DarkGray);
        public Brush KeyIdleForeground { get; set; } = new SolidBrush(Color.LightGray);
        public Brush KeyPressedBackground { get; set; } = new SolidBrush(Color.Cyan);
        public Brush KeyPressedForeground { get; set; } = new SolidBrush(Color.Black);
        public Brush ActiveKeyBackground { get; set; } = new SolidBrush(Color.DarkGray);
        public Brush ActiveKeyBorder { get; set; } = new SolidBrush(Color.Cyan);
        public Brush ActiveKeyForeground { get; set; } = new SolidBrush(Color.White);
        public Brush MouseButtonIdle { get; set; } = new SolidBrush(Color.DarkGray);
        public Brush MouseButtonPressed { get; set; } = new SolidBrush(Color.Cyan);
        public Color PressGlowColor { get; set; } = Color.Cyan;
        public double GlowRadius { get; set; } = 20;
        public double GlowOpacity { get; set; } = 0.8;
        public bool UseOutlineEffect { get; set; }
        public double OutlineThickness { get; set; } = 2;
        public Brush KeyPressedBorder { get; set; } = new SolidBrush(Color.Cyan);
    }

    public static class StyleManager
    {
        public static OverlayTheme GetTheme(OverlayStyle _) =>
            GetCustomTheme(SettingsManager.LoadSettings());

        public static OverlayTheme GetCustomTheme(AppSettings settings)
        {
            settings ??= new AppSettings();

            Color background = ParseHexColor(settings.CustomBackgroundColor, Color.FromArgb(16, 18, 24));
            Color surface = ParseHexColor(settings.CustomSurfaceColor, Color.FromArgb(24, 28, 36));
            Color idleKey = ParseHexColor(settings.CustomIdleKeyColor, Color.FromArgb(42, 48, 59));
            Color pressedKey = ParseHexColor(settings.CustomPressedKeyColor, Color.FromArgb(0, 212, 255));
            Color text = ParseHexColor(settings.CustomTextColor, Color.FromArgb(243, 247, 250));
            Color pressedText = ParseHexColor(settings.CustomPressedTextColor, Color.FromArgb(7, 16, 20));
            Color primary = ParseHexColor(settings.CustomPrimaryColor, Color.FromArgb(0, 212, 255));
            Color secondary = ParseHexColor(settings.CustomSecondaryColor, Color.FromArgb(255, 79, 163));
            Color accent = ParseHexColor(settings.CustomAccentColor, Color.FromArgb(138, 92, 255));

            return new OverlayTheme
            {
                BackgroundBrush = new SolidBrush(background),
                KeyboardBackground = new SolidBrush(surface),
                KeyboardBorder = new SolidBrush(primary),
                MouseBackground = new SolidBrush(surface),
                MouseBorder = new SolidBrush(secondary),
                ActiveKeysBackground = new SolidBrush(surface),
                ActiveKeysBorder = new SolidBrush(accent),
                PrimaryColor = new SolidBrush(primary),
                SecondaryColor = new SolidBrush(secondary),
                AccentColor = new SolidBrush(accent),
                LastKeyGradient = CreateLinearGradient(primary, secondary, 0f),
                KeyBorder = new SolidBrush(primary),
                KeyIdleBackground = new SolidBrush(idleKey),
                KeyIdleForeground = new SolidBrush(text),
                KeyPressedBackground = new SolidBrush(pressedKey),
                KeyPressedForeground = new SolidBrush(pressedText),
                KeyPressedBorder = new SolidBrush(accent),
                ActiveKeyBackground = new SolidBrush(secondary),
                ActiveKeyBorder = new SolidBrush(accent),
                ActiveKeyForeground = new SolidBrush(text),
                MouseButtonIdle = new SolidBrush(idleKey),
                MouseButtonPressed = new SolidBrush(pressedKey),
                PressGlowColor = accent,
                GlowRadius = 20,
                GlowOpacity = 0.8
            };
        }

        public static bool TryParseHexColor(string value, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string hex = value.Trim().TrimStart('#');
            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out int rgb))
                return false;

            color = Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return true;
        }

        public static string NormalizeHexColor(Color color) =>
            $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        private static Color ParseHexColor(string value, Color fallback) =>
            TryParseHexColor(value, out Color color) ? color : fallback;

        private static LinearGradientBrush CreateLinearGradient(Color startColor, Color endColor, float angle) =>
            new(new RectangleF(0, 0, 100, 100), startColor, endColor, angle);
    }
}
