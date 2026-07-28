namespace GamingKeypressOverlay.Win32.Direct2D
{
    /// <summary>
    /// Racing keyboard layout (Sim/Arcade racing)
    /// Minimal controls for racing games
    /// </summary>
    public class Direct2DKeyboardLayoutRacing : IDirect2DKeyboardLayout
    {
        private static readonly string[][] Layout = new string[][] {
            new[] { "W", "S" },
            new[] { "A", "D" },
            new[] { "SPACE" },
            new[] { "SHIFT", "CTRL" }
        };

        public string[][] GetLayout() => Layout;

        public int GetKeyWidth(string keyLabel)
        {
            return keyLabel switch
            {
                "SPACE" => 6,
                "SHIFT" or "CTRL" => 3,
                "W" or "S" or "A" or "D" => 2, // Larger for visibility
                _ => 1
            };
        }

        public byte KeyLabelToVkCode(string label)
        {
            return label.ToUpper() switch
            {
                "SPACE" => 0x20,
                "SHIFT" => 0x10,
                "CTRL" => 0x11,
                "W" => 0x57, "A" => 0x41, "S" => 0x53, "D" => 0x44,
                _ => 0
            };
        }
    }
}
