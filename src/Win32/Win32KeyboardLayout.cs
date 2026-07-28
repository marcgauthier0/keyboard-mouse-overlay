using System;
using System.Collections.Generic;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Complete keyboard layout definition for Win32 rendering
    /// Matches the XAML layout exactly
    /// </summary>
    public class Win32KeyboardLayout
    {
        public class KeyDefinition
        {
            public byte VKeyCode { get; set; } // Virtual key code (0-255)
            public string Label { get; set; }
            public int Width { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
        }
        
        public List<KeyDefinition> Keys { get; } = new List<KeyDefinition>();
        
        public static Win32KeyboardLayout CreateFullLayout(int startX, int startY, int keyWidth, int keyHeight, int keySpacing)
        {
            var layout = new Win32KeyboardLayout();
            int y = startY;
            
            // Function Keys Row: ESC F1-F12
            int x = startX;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x1B, Label = "ESC", Width = 50, X = x, Y = y });
            x += 50 + keySpacing;
            for (int i = 1; i <= 12; i++)
            {
                byte fKey = (byte)(0x70 + i - 1);
                layout.Keys.Add(new KeyDefinition { VKeyCode = fKey, Label = $"F{i}", Width = keyWidth, X = x, Y = y });
                x += keyWidth + keySpacing;
            }
            
            // Numbers Row: 1-9, 0, -, =, Backspace
            y += keyHeight + keySpacing;
            x = startX;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x31, Label = "1", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x32, Label = "2", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x33, Label = "3", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x34, Label = "4", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x35, Label = "5", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x36, Label = "6", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x37, Label = "7", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x38, Label = "8", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x39, Label = "9", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x30, Label = "0", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xBD, Label = "-", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xBB, Label = "=", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x08, Label = "⌫", Width = 80, X = x, Y = y });
            
            // QWERTY Row 1: Tab Q W E R T Y U I O P [ ] \
            y += keyHeight + keySpacing;
            x = startX;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x09, Label = "TAB", Width = 60, X = x, Y = y });
            x += 60 + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x51, Label = "Q", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x57, Label = "W", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x45, Label = "E", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x52, Label = "R", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x54, Label = "T", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x59, Label = "Y", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x55, Label = "U", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x49, Label = "I", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x4F, Label = "O", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x50, Label = "P", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xDB, Label = "[", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xDD, Label = "]", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xDC, Label = "\\", Width = 60, X = x, Y = y });
            
            // QWERTY Row 2: CapsLock A S D F G H J K L ; ' Enter
            y += keyHeight + keySpacing;
            x = startX;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x14, Label = "CAPS", Width = 70, X = x, Y = y });
            x += 70 + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x41, Label = "A", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x53, Label = "S", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x44, Label = "D", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x46, Label = "F", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x47, Label = "G", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x48, Label = "H", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x4A, Label = "J", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x4B, Label = "K", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x4C, Label = "L", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xBA, Label = ";", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xDE, Label = "'", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x0D, Label = "ENTER", Width = 90, X = x, Y = y });
            
            // QWERTY Row 3: Shift Z X C V B N M , . / Shift
            y += keyHeight + keySpacing;
            x = startX;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xA0, Label = "SHIFT", Width = 90, X = x, Y = y });
            x += 90 + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x5A, Label = "Z", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x58, Label = "X", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x43, Label = "C", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x56, Label = "V", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x42, Label = "B", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x4E, Label = "N", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x4D, Label = "M", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xBC, Label = ",", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xBE, Label = ".", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xBF, Label = "/", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xA1, Label = "SHIFT", Width = 100, X = x, Y = y });
            
            // Bottom Row: Ctrl, Win, Alt, Space, AltGr, Ctrl
            y += keyHeight + keySpacing;
            x = startX;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xA2, Label = "CTRL", Width = 70, X = x, Y = y });
            x += 70 + 6; // 6px spacing
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x5B, Label = "WIN", Width = 60, X = x, Y = y });
            x += 60 + 6;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xA4, Label = "ALT", Width = 60, X = x, Y = y });
            x += 60 + 6;
            // Space bar (fills remaining space, but we'll use fixed width for now)
            int spaceX = x;
            int spaceWidth = 300; // Approximate space bar width
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x20, Label = "SPACE", Width = spaceWidth, X = spaceX, Y = y });
            x = spaceX + spaceWidth + 6;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xA5, Label = "ALTGR", Width = 60, X = x, Y = y });
            x += 60 + 6;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xA3, Label = "CTRL", Width = 70, X = x, Y = y });
            
            return layout;
        }
        
        public static Win32KeyboardLayout CreateGamingLayout(int startX, int startY, int keyWidth, int keyHeight, int keySpacing)
        {
            // Gaming layout: Numbers 1-6 + WASD + common game keys
            var layout = new Win32KeyboardLayout();
            int y = startY;
            int x = startX;
            
            // Numbers row: 1, 2, 3, 4, 5, 6
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x31, Label = "1", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x32, Label = "2", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x33, Label = "3", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x34, Label = "4", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x35, Label = "5", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x36, Label = "6", Width = keyWidth, X = x, Y = y });
            
            // WASD row
            y += keyHeight + keySpacing;
            x = startX;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x57, Label = "W", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x41, Label = "A", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x53, Label = "S", Width = keyWidth, X = x, Y = y });
            x += keyWidth + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x44, Label = "D", Width = keyWidth, X = x, Y = y });
            
            // Common game keys
            y += keyHeight + keySpacing;
            x = startX;
            string[] gameKeys = { "Q", "E", "R", "F", "C", "V", "X", "Z", "G", "T", "Y", "U", "I", "O", "P", "H", "J", "K", "L", "B", "N", "M" };
            byte[] gameKeyCodes = { 0x51, 0x45, 0x52, 0x46, 0x43, 0x56, 0x58, 0x5A, 0x47, 0x54, 0x59, 0x55, 0x49, 0x4F, 0x50, 0x48, 0x4A, 0x4B, 0x4C, 0x42, 0x4E, 0x4D };
            
            for (int i = 0; i < gameKeys.Length; i++)
            {
                if (i > 0 && i % 8 == 0)
                {
                    y += keyHeight + keySpacing;
                    x = startX;
                }
                layout.Keys.Add(new KeyDefinition { VKeyCode = gameKeyCodes[i], Label = gameKeys[i], Width = keyWidth, X = x, Y = y });
                x += keyWidth + keySpacing;
            }
            
            // Space, Shift, Ctrl
            y += keyHeight + keySpacing;
            x = startX;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0x20, Label = "SPACE", Width = keyWidth * 3, X = x, Y = y });
            x += keyWidth * 3 + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xA0, Label = "SHIFT", Width = keyWidth * 2, X = x, Y = y });
            x += keyWidth * 2 + keySpacing;
            layout.Keys.Add(new KeyDefinition { VKeyCode = 0xA2, Label = "CTRL", Width = keyWidth * 2, X = x, Y = y });
            
            return layout;
        }
    }
}
