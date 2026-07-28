using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using GamingKeypressOverlay.Input;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Keyboard rendering module for GDI renderer
    /// Handles keyboard keys, last input, and active keys display
    /// </summary>
    internal class GDIRendererKeyboard
    {
        private readonly GDIRenderContext _context;
        private readonly Win32KeyboardLayout _keyboardLayout;
        
        // Layout constants
        private const int KEY_HEIGHT = 40;
        private const int KEYBOARD_X = 50;
        private const int KEYBOARD_Y = 100;
        private const int TILE_Y = 20;
        private const int TILE_HEIGHT = 60;
        private const int TILE_PADDING = 10;
        private const int GLOBAL_PADDING = 50; // Global padding around application
        
        // Last input display
        private string _lastDisplayedInput = "---";
        private long _lastInputDisplayTime = 0;
        private const long LAST_INPUT_DISPLAY_DURATION_MS = 2000;
        
        // Win32 API for key name
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetKeyNameText(int lParam, StringBuilder lpString, int nSize);
        
        /// <summary>
        /// Convert virtual key code to key name string
        /// </summary>
        private static string VirtualKeyToString(byte vkCode)
        {
            // Use Win32 API to get key name
            int scanCode = MapVirtualKey(vkCode, 0);
            int lParam = (scanCode << 16);
            StringBuilder sb = new StringBuilder(256);
            if (GetKeyNameText(lParam, sb, 256) > 0)
            {
                return sb.ToString();
            }
            
            // Fallback: manual mapping for common keys
            return VkCodeToName(vkCode);
        }
        
        [DllImport("user32.dll")]
        private static extern int MapVirtualKey(int uCode, int uMapType);
        
        private static string VkCodeToName(byte vkCode)
        {
            return vkCode switch
            {
                0x08 => "Backspace",
                0x09 => "Tab",
                0x0D => "Enter",
                0x10 => "Shift",
                0x11 => "Ctrl",
                0x12 => "Alt",
                0x1B => "Esc",
                0x20 => "Space",
                0x25 => "Left",
                0x26 => "Up",
                0x27 => "Right",
                0x28 => "Down",
                0x2C => "PrintScreen",
                0x2D => "Insert",
                0x2E => "Delete",
                0x5B => "Win",
                0x5C => "Win",
                0x5D => "Menu",
                >= 0x30 and <= 0x39 => ((char)('0' + (vkCode - 0x30))).ToString(), // 0-9
                >= 0x41 and <= 0x5A => ((char)('A' + (vkCode - 0x41))).ToString(), // A-Z
                >= 0x60 and <= 0x69 => $"Num{vkCode - 0x60}", // Numpad 0-9
                0x6A => "Num*",
                0x6B => "Num+",
                0x6D => "Num-",
                0x6E => "Num.",
                0x6F => "Num/",
                0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
                0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
                0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
                _ => $"VK{vkCode:X2}"
            };
        }
        
        public GDIRendererKeyboard(GDIRenderContext context, Win32KeyboardLayout keyboardLayout)
        {
            _context = context;
            _keyboardLayout = keyboardLayout;
        }
        
        /// <summary>
        /// Render keyboard with all keys
        /// </summary>
        public unsafe void RenderKeyboard(Graphics g, InputStateSnapshot snapshot)
        {
            if (_keyboardLayout == null) return;
            
            // Render all keys from layout
            foreach (var keyDef in _keyboardLayout.Keys)
            {
                bool isPressed = _context.IsKeyPressed(snapshot, keyDef.VKeyCode);
                RenderKey(g, keyDef.Label, keyDef.X, keyDef.Y, keyDef.Width, KEY_HEIGHT, snapshot, keyDef.VKeyCode);
            }
        }
        
        /// <summary>
        /// Render a single key
        /// </summary>
        private void RenderKey(Graphics g, string label, int x, int y, int width, int height, 
                              InputStateSnapshot snapshot, byte vkCode)
        {
            bool isPressed = _context.IsKeyPressed(snapshot, vkCode);
            
            // Shadow effect for unpressed keys (depth)
            if (!isPressed)
            {
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                {
                    g.FillRectangle(shadowBrush, x + 2, y + 2, width, height);
                }
            }
            
            // Use theme brushes directly (support gradients; fix broken styles)
            Brush brush = isPressed ? _context.Theme.KeyPressedBackground : _context.Theme.KeyIdleBackground;
            Pen pen = isPressed ? _context.PressedKeyBorderPen : _context.KeyBorderPen;
            Brush textBrush = isPressed ? _context.PressedTextBrush : _context.TextBrush;

            g.FillRectangle(brush, x, y, width, height);

            // Add highlight gradient effect on top for unpressed keys
            if (!isPressed)
            {
                Rectangle highlightRect = new Rectangle(x, y, width, Math.Max(1, height / 3));
                Color keyColor = _context.BrushToColor(brush);
                Color highlightColor = Color.FromArgb(60,
                    Math.Min(255, keyColor.R + 30),
                    Math.Min(255, keyColor.G + 30),
                    Math.Min(255, keyColor.B + 30)
                );
                using (SolidBrush highlightBrush = new SolidBrush(highlightColor))
                {
                    g.FillRectangle(highlightBrush, highlightRect);
                }
            }
            
            g.DrawRectangle(pen, x, y, width, height);
            
            // Draw key label with better positioning
            SizeF textSize = g.MeasureString(label, _context.KeyFont);
            float textX = x + (width - textSize.Width) / 2;
            float textY = y + (height - textSize.Height) / 2;
            
            // Text shadow for better readability
            if (!isPressed)
            {
                using (SolidBrush textShadow = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                {
                    g.DrawString(label, _context.KeyFont, textShadow, textX + 1, textY + 1);
                }
            }
            
            g.DrawString(label, _context.KeyFont, textBrush, textX, textY);
        }
        
        /// <summary>
        /// Render last input tile
        /// </summary>
        public unsafe void RenderLastInput(Graphics g, InputStateSnapshot snapshot)
        {
            if (snapshot == null) return;
            
            long currentTime = Environment.TickCount;
            
            // Build last input string from snapshot
            string currentInput = "---";
            if (snapshot.SecondLastKey != 0 && snapshot.LastKey != 0)
            {
                string secondKey = VirtualKeyToString(snapshot.SecondLastKey);
                string lastKey = VirtualKeyToString(snapshot.LastKey);
                if (!string.IsNullOrEmpty(secondKey) && !string.IsNullOrEmpty(lastKey))
                {
                    currentInput = $"{secondKey} → {lastKey}";
                }
            }
            else if (snapshot.LastKey != 0)
            {
                string lastKey = VirtualKeyToString(snapshot.LastKey);
                if (!string.IsNullOrEmpty(lastKey))
                {
                    currentInput = lastKey;
                }
            }
            
            // Update displayed input if there's a new key press
            if (currentInput != "---")
            {
                _lastDisplayedInput = currentInput;
                _lastInputDisplayTime = currentTime;
            }
            // Keep displaying last input for a duration after key release
            else if (_lastDisplayedInput != "---" && 
                     (currentTime - _lastInputDisplayTime) < LAST_INPUT_DISPLAY_DURATION_MS)
            {
                // Keep showing last input
                currentInput = _lastDisplayedInput;
            }
            else
            {
                // Reset after duration
                _lastDisplayedInput = "---";
                currentInput = "---";
            }
            
            string lastInput = currentInput;
            
            // Position en haut dans une tile stylisée
            int tileX = GLOBAL_PADDING;
            int tileY = GLOBAL_PADDING;
            int tileWidth = 300;
            
            // Draw tile background avec bordure
            Color tileBgColor = _context.BrushToColor(_context.Theme.KeyIdleBackground);
            Color tileBorderColor = _context.BrushToColor(_context.Theme.PrimaryColor);
            
            using (SolidBrush tileBrush = new SolidBrush(Color.FromArgb(200, tileBgColor)))
            using (Pen tileBorderPen = new Pen(tileBorderColor, 2))
            {
                g.FillRectangle(tileBrush, tileX, tileY, tileWidth, TILE_HEIGHT);
                g.DrawRectangle(tileBorderPen, tileX, tileY, tileWidth, TILE_HEIGHT);
            }
            
            // Draw "Last Input:" label
            Color labelColor = _context.BrushToColor(_context.Theme.PrimaryColor);
            using (SolidBrush labelBrush = new SolidBrush(labelColor))
            {
                g.DrawString("Last Input:", _context.KeyFont, labelBrush, tileX + TILE_PADDING, tileY + 5);
            }
            
            // Draw last input value
            Color valueColor = _context.BrushToColor(_context.Theme.PrimaryColor);
            using (SolidBrush valueBrush = new SolidBrush(valueColor))
            using (Font valueFont = new Font("Consolas", 20, FontStyle.Bold))
            {
                g.DrawString(lastInput, valueFont, valueBrush, tileX + TILE_PADDING, tileY + 28);
            }
        }
        
        /// <summary>
        /// Render active keys tile
        /// </summary>
        public unsafe void RenderActiveKeys(Graphics g, InputStateSnapshot snapshot)
        {
            if (snapshot == null) return;
            
            // Collect all pressed keys
            System.Collections.Generic.List<string> pressedKeys = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 256; i++)
            {
                if (snapshot.Keys[i])
                {
                    string keyName = VirtualKeyToString((byte)i);
                    if (!string.IsNullOrEmpty(keyName))
                    {
                        pressedKeys.Add(keyName);
                    }
                }
            }
            
            // Position en haut dans une tile stylisée (à côté de Last Input)
            int tileX = GLOBAL_PADDING + 320;
            int tileY = GLOBAL_PADDING;
            int tileWidth = 400;
            
            // Calculer la hauteur nécessaire
            int activeKeyBoxHeight = 25;
            int activeKeyBoxSpacing = 4;
            int estimatedKeysPerRow = tileWidth / 80;
            int estimatedRows = (pressedKeys.Count + estimatedKeysPerRow - 1) / estimatedKeysPerRow;
            int dynamicTileHeight = Math.Max(TILE_HEIGHT, 28 + (estimatedRows * (activeKeyBoxHeight + activeKeyBoxSpacing)) + TILE_PADDING);
            
            // Draw tile background
            Color tileBgColor = _context.BrushToColor(_context.Theme.KeyIdleBackground);
            Color tileBorderColor = _context.BrushToColor(_context.Theme.AccentColor);
            
            using (SolidBrush tileBrush = new SolidBrush(Color.FromArgb(200, tileBgColor)))
            using (Pen tileBorderPen = new Pen(tileBorderColor, 2))
            {
                g.FillRectangle(tileBrush, tileX, tileY, tileWidth, dynamicTileHeight);
                g.DrawRectangle(tileBorderPen, tileX, tileY, tileWidth, dynamicTileHeight);
            }
            
            // Draw "Active Keys:" label
            Color labelColor = _context.BrushToColor(_context.Theme.AccentColor);
            using (SolidBrush labelBrush = new SolidBrush(labelColor))
            {
                g.DrawString("Active Keys:", _context.KeyFont, labelBrush, tileX + TILE_PADDING, tileY + 5);
            }
            
            // Draw active keys
            int x = tileX + TILE_PADDING;
            int y = tileY + 28;
            int maxWidth = tileWidth - TILE_PADDING * 2;
            int currentX = x;
            int currentY = y;
            
            if (pressedKeys.Count == 0)
            {
                Color emptyColor = _context.BrushToColor(_context.Theme.KeyIdleForeground);
                using (SolidBrush emptyBrush = new SolidBrush(Color.FromArgb(150, emptyColor)))
                using (Font emptyFont = new Font("Consolas", 16, FontStyle.Regular))
                {
                    g.DrawString("---", emptyFont, emptyBrush, x, y);
                }
                return;
            }
            
            Color activeKeyBg = _context.BrushToColor(_context.Theme.ActiveKeyBackground);
            Color activeKeyBorder = _context.BrushToColor(_context.Theme.ActiveKeyBorder);
            Color activeKeyText = _context.BrushToColor(_context.Theme.ActiveKeyForeground);
            
            foreach (var key in pressedKeys)
            {
                string keyLabel = key.ToUpper();
                SizeF textSize = g.MeasureString(keyLabel, _context.KeyFont);
                int keyBoxWidth = (int)textSize.Width + 20;
                int keyBoxHeight = 25;
                
                // Wrap to next line if needed
                if (currentX + keyBoxWidth > x + maxWidth)
                {
                    currentX = x;
                    currentY += keyBoxHeight + activeKeyBoxSpacing;
                }
                
                // Draw key box
                using (SolidBrush bgBrush = new SolidBrush(activeKeyBg))
                using (Pen borderPen = new Pen(activeKeyBorder, 2))
                using (SolidBrush textBrush = new SolidBrush(activeKeyText))
                {
                    g.FillRectangle(bgBrush, currentX, currentY, keyBoxWidth, keyBoxHeight);
                    g.DrawRectangle(borderPen, currentX, currentY, keyBoxWidth, keyBoxHeight);
                    g.DrawString(keyLabel, _context.KeyFont, textBrush, currentX + 10, currentY + 5);
                }
                
                currentX += keyBoxWidth + activeKeyBoxSpacing;
            }
        }
    }
}
