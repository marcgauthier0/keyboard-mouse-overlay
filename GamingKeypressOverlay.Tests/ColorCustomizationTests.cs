using System.Drawing;
using GamingKeypressOverlay.Overlay;
using GamingKeypressOverlay.Settings;
using Xunit;

namespace GamingKeypressOverlay.Tests
{
    public class ColorCustomizationTests
    {
        [Theory]
        [InlineData("#00D4FF", 0, 212, 255)]
        [InlineData("ff4fa3", 255, 79, 163)]
        [InlineData("  #101218  ", 16, 18, 24)]
        public void TryParseHexColor_ValidRgb_ParsesColor(string value, int red, int green, int blue)
        {
            bool parsed = StyleManager.TryParseHexColor(value, out Color color);

            Assert.True(parsed);
            Assert.Equal(red, color.R);
            Assert.Equal(green, color.G);
            Assert.Equal(blue, color.B);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("#12345")]
        [InlineData("#GG0000")]
        [InlineData("#11223344")]
        public void TryParseHexColor_InvalidValue_ReturnsFalse(string? value)
        {
            Assert.False(StyleManager.TryParseHexColor(value, out _));
        }

        [Fact]
        public void NormalizeHexColor_ReturnsUppercaseRgb()
        {
            Assert.Equal("#0CA2FF", StyleManager.NormalizeHexColor(Color.FromArgb(12, 162, 255)));
        }

        [Fact]
        public void GetCustomTheme_UsesConfiguredPalette()
        {
            var settings = new AppSettings
            {
                CustomPrimaryColor = "#123456",
                CustomIdleKeyColor = "#234567",
                CustomPressedKeyColor = "#345678",
                CustomTextColor = "#456789",
                CustomPressedTextColor = "#56789A"
            };

            OverlayTheme theme = StyleManager.GetCustomTheme(settings);

            Assert.Equal(Color.FromArgb(0x12, 0x34, 0x56), ((SolidBrush)theme.PrimaryColor).Color);
            Assert.Equal(Color.FromArgb(0x23, 0x45, 0x67), ((SolidBrush)theme.KeyIdleBackground).Color);
            Assert.Equal(Color.FromArgb(0x34, 0x56, 0x78), ((SolidBrush)theme.KeyPressedBackground).Color);
            Assert.Equal(Color.FromArgb(0x45, 0x67, 0x89), ((SolidBrush)theme.KeyIdleForeground).Color);
            Assert.Equal(Color.FromArgb(0x56, 0x78, 0x9A), ((SolidBrush)theme.KeyPressedForeground).Color);
        }
    }
}
