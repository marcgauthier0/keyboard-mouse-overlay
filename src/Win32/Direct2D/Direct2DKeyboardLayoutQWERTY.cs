namespace GamingKeypressOverlay.Win32.Direct2D
{
    /// <summary>
    /// QWERTY keyboard layout (US/UK standard)
    /// </summary>
    public class Direct2DKeyboardLayoutQWERTY : IDirect2DKeyboardLayout
    {
        private static readonly string[][] Layout = new string[][] {
            new[] { "ESC", "", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" },
            new[] { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "←" },
            new[] { "TAB", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\" },
            new[] { "CAPS", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "ENTER" },
            new[] { "SHIFT", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "SHIFT" },
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
                "`" => 0xC0,
                "-" => 0xBD,
                "=" => 0xBB,
                "[" => 0xDB,
                "]" => 0xDD,
                "\\" => 0xDC,
                ";" => 0xBA,
                "'" => 0xDE,
                "," => 0xBC,
                "." => 0xBE,
                "/" => 0xBF,
                "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
                "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
                "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
                _ => label.Length == 1 ? GetSingleCharVkCode(label[0]) : (byte)0
            };
        }

        private byte GetSingleCharVkCode(char c)
        {
            if (c >= 'A' && c <= 'Z') return (byte)(0x41 + (c - 'A'));
            if (c >= '0' && c <= '9') return (byte)(0x30 + (c - '0'));
            return 0;
        }
    }
}
