namespace GamingKeypressOverlay.Win32.Direct2D
{
    /// <summary>
    /// FPS keyboard layout (COD, Fortnite, Apex)
    /// WASD-focused layout optimized for competitive FPS
    /// </summary>
    public class Direct2DKeyboardLayoutFPS : IDirect2DKeyboardLayout
    {
        private static readonly string[][] Layout = new string[][] {
            new[] { "ESC", "1", "2", "3", "4", "5", "6", "TAB" },
            new[] { "Q", "W", "E" },
            new[] { "A", "S", "D" },
            new[] { "SHIFT", "CTRL", "R", "F" },
            new[] { "Z", "X", "C", "V" },
            new[] { "SPACE" }
        };

        public string[][] GetLayout() => Layout;

        public int GetKeyWidth(string keyLabel)
        {
            return keyLabel switch
            {
                // SPACE width = SHIFT (2.4) + CTRL (1.5) + R (1) + F (1) + 3 gaps = ~8.9, round to 9
                "SPACE" => 9,
                "SHIFT" => (int)(2.4),
                "CTRL" => (int)(1.5),
                "Q" or "W" or "E" or "A" or "S" or "D" => (int)(1.25), // Larger for FPS visibility
                _ => 1
            };
        }

        public byte KeyLabelToVkCode(string label)
        {
            return label.ToUpper() switch
            {
                "ESC" => 0x1B,
                "TAB" => 0x09,
                "SHIFT" => 0x10,
                "CTRL" => 0x11,
                "SPACE" => 0x20,
                "Q" => 0x51, "W" => 0x57, "E" => 0x45, "R" => 0x52,
                "A" => 0x41, "S" => 0x53, "D" => 0x44, "F" => 0x46,
                "Z" => 0x5A, "X" => 0x58, "C" => 0x43, "V" => 0x56,
                "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34, "5" => 0x35, "6" => 0x36,
                _ => 0
            };
        }
    }
}
