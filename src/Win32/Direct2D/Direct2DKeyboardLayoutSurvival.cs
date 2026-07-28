namespace GamingKeypressOverlay.Win32.Direct2D
{
    /// <summary>
    /// Survival keyboard layout (Minecraft, Rust)
    /// Craft + inventory focused
    /// </summary>
    public class Direct2DKeyboardLayoutSurvival : IDirect2DKeyboardLayout
    {
        private static readonly string[][] Layout = new string[][] {
            new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" },
            new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
            new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" },
            new[] { "SHIFT", "Z", "X", "C", "V", "B", "N", "M", "SHIFT" },
            new[] { "CTRL", "SPACE", "CTRL" }
        };

        public string[][] GetLayout() => Layout;

        public int GetKeyWidth(string keyLabel)
        {
            return keyLabel switch
            {
                "SPACE" => 6,
                "SHIFT" => (int)(2.4),
                "CTRL" => (int)(1.5),
                _ => 1
            };
        }

        public byte KeyLabelToVkCode(string label)
        {
            return label.ToUpper() switch
            {
                "SHIFT" => 0x10,
                "CTRL" => 0x11,
                "SPACE" => 0x20,
                "Q" => 0x51, "W" => 0x57, "E" => 0x45, "R" => 0x52, "T" => 0x54,
                "Y" => 0x59, "U" => 0x55, "I" => 0x49, "O" => 0x4F, "P" => 0x50,
                "A" => 0x41, "S" => 0x53, "D" => 0x44, "F" => 0x46, "G" => 0x47,
                "H" => 0x48, "J" => 0x4A, "K" => 0x4B, "L" => 0x4C,
                "Z" => 0x5A, "X" => 0x58, "C" => 0x43, "V" => 0x56, "B" => 0x42,
                "N" => 0x4E, "M" => 0x4D,
                "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34, "5" => 0x35,
                "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
                _ => 0
            };
        }
    }
}
