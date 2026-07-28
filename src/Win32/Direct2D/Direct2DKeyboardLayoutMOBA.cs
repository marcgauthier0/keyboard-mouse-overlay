namespace GamingKeypressOverlay.Win32.Direct2D
{
    /// <summary>
    /// MOBA keyboard layout (LoL, Dota 2)
    /// QWER centered + items layout
    /// </summary>
    public class Direct2DKeyboardLayoutMOBA : IDirect2DKeyboardLayout
    {
        private static readonly string[][] Layout = new string[][] {
            new[] { "", "", "Q", "W", "E", "R", "", "" },
            new[] { "", "A", "S", "D", "F", "", "" },
            new[] { "", "1", "2", "3", "4", "", "" },
            new[] { "", "", "", "SPACE", "", "", "" }
        };

        public string[][] GetLayout() => Layout;

        public int GetKeyWidth(string keyLabel)
        {
            return keyLabel switch
            {
                "SPACE" => 8,
                "Q" or "W" or "E" or "R" => (int)(1.3), // Slightly larger for abilities
                _ => 1
            };
        }

        public byte KeyLabelToVkCode(string label)
        {
            return label.ToUpper() switch
            {
                "SPACE" => 0x20,
                "Q" => 0x51, "W" => 0x57, "E" => 0x45, "R" => 0x52,
                "A" => 0x41, "S" => 0x53, "D" => 0x44, "F" => 0x46,
                "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
                _ => 0
            };
        }
    }
}
