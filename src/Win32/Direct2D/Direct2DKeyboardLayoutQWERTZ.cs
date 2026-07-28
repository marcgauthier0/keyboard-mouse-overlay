namespace GamingKeypressOverlay.Win32.Direct2D
{
    /// <summary>
    /// QWERTZ keyboard layout (German/Swiss)
    /// </summary>
    public class Direct2DKeyboardLayoutQWERTZ : IDirect2DKeyboardLayout
    {
        private static readonly string[][] Layout = new string[][] {
            new[] { "ESC", "", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" },
            new[] { "^", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "ß", "´", "←" },
            new[] { "TAB", "Q", "W", "E", "R", "T", "Z", "U", "I", "O", "P", "Ü", "+", "*" },
            new[] { "CAPS", "A", "S", "D", "F", "G", "H", "J", "K", "L", "Ö", "Ä", "#", "ENTER" },
            new[] { "SHIFT", "<", "Y", "X", "C", "V", "B", "N", "M", ",", ".", "-", "SHIFT" },
            new[] { "CTRL", "WIN", "ALT", "SPACE", "ALTGR", "CTRL" }
        };

        public string[][] GetLayout() => Layout;

        public int GetKeyWidth(string keyLabel)
        {
            return keyLabel switch
            {
                "←" => 2,
                "TAB" => (int)(1.5),
                "CAPS" => (int)(1.8),
                "ENTER" => (int)(2.2),
                "SHIFT" => (int)(2.4),
                "SPACE" => 10,
                "CTRL" or "WIN" or "ALT" or "ALTGR" => (int)(1.3),
                _ => 1
            };
        }

        public byte KeyLabelToVkCode(string label)
        {
            // QWERTZ uses same VK codes but different physical positions
            return label.ToUpper() switch
            {
                "ESC" => 0x1B,
                "TAB" => 0x09,
                "CAPS" => 0x14,
                "SHIFT" => 0x10,
                "CTRL" => 0x11,
                "WIN" => 0x5B,
                "ALT" => 0x12,
                "ALTGR" => 0x12,
                "SPACE" => 0x20,
                "ENTER" => 0x0D,
                "←" => 0x08,
                "Q" => 0x51, "W" => 0x57, "E" => 0x45, "R" => 0x52, "T" => 0x54,
                "Z" => 0x5A, "U" => 0x55, "I" => 0x49, "O" => 0x4F, "P" => 0x50,
                "A" => 0x41, "S" => 0x53, "D" => 0x44, "F" => 0x46, "G" => 0x47,
                "H" => 0x48, "J" => 0x4A, "K" => 0x4B, "L" => 0x4C,
                "Y" => 0x59, "X" => 0x58, "C" => 0x43, "V" => 0x56, "B" => 0x42, "N" => 0x4E, "M" => 0x4D,
                "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
                "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
                "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
                _ => 0
            };
        }
    }
}
