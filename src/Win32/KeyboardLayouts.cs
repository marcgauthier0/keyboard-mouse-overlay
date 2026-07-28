using System;
using System.Collections.Generic;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Different keyboard layouts and sizes for gaming overlays
    /// Supports: QWERTY/AZERTY/QWERTZ, Full/TKL/75%/60%, FPS/MMO/MOBA configs
    /// </summary>
    public enum KeyboardLayoutType
    {
        QWERTY,  // Standard (US/UK)
        AZERTY,  // French/Belgian
        QWERTZ   // German/Swiss
    }
    
    public enum KeyboardSize
    {
        Full,    // 100% - Full keyboard with numpad
        TKL,     // 80% - Tenkeyless (no numpad)
        SeventyFive, // 75% - Compact with function row
        SixtyFive,   // 65% - No function row
        Sixty        // 60% - Minimal (alpha + modifiers only)
    }
    
    public enum GameConfig
    {
        FPS,         // COD, Fortnite, Apex - WASD movement, minimal keys
        MMO,         // WoW, FFXIV - Full keyboard for many hotkeys
        MOBA,        // LoL, Dota 2 - QWER + abilities
        Racing,      // Sim/Arcade racing - Minimal controls
        Survival,    // Minecraft, Rust - Craft + inventory
        General      // Standard gaming layout
    }
    
    /// <summary>
    /// Helper class to create keyboard layouts with different configurations
    /// Win32 architecture: Everything is calculated rectangles, row by row
    /// Principe: Un layout = une liste de touches avec positions calculées
    /// </summary>
    public static class KeyboardLayoutBuilder
    {
        /// <summary>
        /// Helper: Add a single key to layout (Win32 rectangle calculation)
        /// </summary>
        private static void AddKey(Win32KeyboardLayout layout, ref int x, int y, int width, int height, byte vkCode, string label, int keySpacing)
        {
            layout.Keys.Add(new Win32KeyboardLayout.KeyDefinition
            {
                VKeyCode = vkCode,
                Label = label,
                Width = width,
                X = x,
                Y = y
            });
            x += width + keySpacing;
        }
        
        /// <summary>
        /// Helper: Calculate special key widths based on base key width
        /// Principe: Tout est basé sur les unités de base (KEY_W, KEY_H, GAP)
        /// </summary>
        private static int GetShiftWidth(int keyWidth) => (int)(keyWidth * 2.25);  // Shift = KEY_W * 2.25
        private static int GetSpaceWidth(int keyWidth) => keyWidth * 6;            // Space = KEY_W * 6
        private static int GetTabWidth(int keyWidth) => (int)(keyWidth * 1.5);     // Tab = KEY_W * 1.5
        private static int GetEnterWidth(int keyWidth) => (int)(keyWidth * 2.25);  // Enter = KEY_W * 2.25
        private static int GetBackspaceWidth(int keyWidth) => keyWidth * 2;        // Backspace = KEY_W * 2
        
        /// <summary>
        /// Create a keyboard layout based on type, size, and game config
        /// </summary>
        public static Win32KeyboardLayout CreateLayout(
            KeyboardLayoutType layoutType = KeyboardLayoutType.QWERTY,
            KeyboardSize size = KeyboardSize.Full,
            GameConfig gameConfig = GameConfig.General,
            int startX = 50, int startY = 80, 
            int keyWidth = 40, int keyHeight = 40, int keySpacing = 4)
        {
            Win32KeyboardLayout layout;
            
            // First, determine base layout based on GameConfig
            switch (gameConfig)
            {
                case GameConfig.FPS:
                    // FPS: COD, Fortnite, Apex - WASD-focused layout
                    layout = CreateFPSLayout(startX, startY, keyWidth, keyHeight, keySpacing);
                    break;
                    
                case GameConfig.MOBA:
                    // MOBA: LoL, Dota 2 - QWER centered + items
                    layout = CreateMOBALayout(startX, startY, keyWidth, keyHeight, keySpacing);
                    break;
                    
                case GameConfig.MMO:
                    // MMO: WoW, FFXIV - Dense grid for many hotkeys
                    layout = CreateMMOLayout(size, startX, startY, keyWidth, keyHeight, keySpacing);
                    break;
                    
                case GameConfig.Racing:
                    // Racing: Sim/Arcade - Minimal controls
                    layout = CreateRacingLayout(startX, startY, keyWidth, keyHeight, keySpacing);
                    break;
                    
                case GameConfig.Survival:
                    // Survival: Minecraft, Rust - Craft + inventory
                    layout = CreateSurvivalLayout(startX, startY, keyWidth, keyHeight, keySpacing);
                    break;
                    
                case GameConfig.General:
                default:
                    // General: Standard layout based on size
                    layout = CreateGeneralLayout(size, startX, startY, keyWidth, keyHeight, keySpacing);
                    break;
            }
            
            // Apply keyboard size modifications (remove numpad, function row, etc.)
            ApplySizeModifications(layout, size);
            
            // Apply layout type (QWERTY/AZERTY/QWERTZ) modifications
            ApplyLayoutType(layout, layoutType);
            
            return layout;
        }
        
        /// <summary>
        /// FPS Layout: WASD-focused, optimized for COD/Fortnite
        /// Principe: WASD en croix centrée, regroupement par importance (combat/secondaire)
        /// Layout "pro" recommandé:
        ///   [ESC][1][2][3][4][5][6][TAB]
        ///   [ Q ][ W ][ E ]
        ///   [ A ] [ S ] [ D ]
        ///   [SHIFT][CTRL][ R ][ F ]
        ///   [ Z ][ X ][ C ][ V ]
        ///   [        SPACE        ]
        /// </summary>
        private static Win32KeyboardLayout CreateFPSLayout(int startX, int startY, int keyWidth, int keyHeight, int keySpacing)
        {
            var layout = new Win32KeyboardLayout();
            int x = startX;
            int y = startY;
            
            // Row 1: ESC, Numbers 1-6 (weapons/items), TAB
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x1B, "ESC", keySpacing);
            for (int i = 1; i <= 6; i++)
            {
                byte numKey = (byte)(0x31 + i - 1);
                AddKey(layout, ref x, y, keyWidth, keyHeight, numKey, i.ToString(), keySpacing);
            }
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x09, "TAB", keySpacing);
            
            // Row 2: Q W E (en ligne horizontale)
            y += keyHeight + keySpacing;
            int fpsKeySize = (int)(keyWidth * 1.25); // 25% larger for FPS visibility
            
            // Q W E alignés horizontalement
            x = startX;
            AddKey(layout, ref x, y, fpsKeySize, fpsKeySize, 0x51, "Q", keySpacing);
            AddKey(layout, ref x, y, fpsKeySize, fpsKeySize, 0x57, "W", keySpacing);
            AddKey(layout, ref x, y, fpsKeySize, fpsKeySize, 0x45, "E", keySpacing);
            
            // Row 3: A S D (alignés sous Q W E)
            y += fpsKeySize + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, fpsKeySize, fpsKeySize, 0x41, "A", keySpacing);
            AddKey(layout, ref x, y, fpsKeySize, fpsKeySize, 0x53, "S", keySpacing);
            AddKey(layout, ref x, y, fpsKeySize, fpsKeySize, 0x44, "D", keySpacing);
            
            // Row 4: Groupe Combat - SHIFT CTRL R F (ultra fréquentes, E déjà en haut)
            y += fpsKeySize + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, GetShiftWidth(keyWidth), keyHeight, 0xA0, "SHIFT", keySpacing);
            AddKey(layout, ref x, y, (int)(keyWidth * 1.5), keyHeight, 0xA2, "CTRL", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x52, "R", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x46, "F", keySpacing);
            
            // Row 5: Groupe Secondaire - Z X C V (situationnelles)
            y += keyHeight + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x5A, "Z", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x58, "X", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x43, "C", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x56, "V", keySpacing);
            
            // Row 6: SPACE (large, bien visible)
            y += keyHeight + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, GetSpaceWidth(keyWidth), keyHeight, 0x20, "SPACE", keySpacing);
            
            return layout;
        }
        
        /// <summary>
        /// MOBA Layout: LoL, Dota 2 - QWER centered + items separated
        /// Principe: Sorts centraux, réactivité, peu de touches parasites
        /// Layout:
        ///        [ Q ][ W ][ E ][ R ]
        ///   [ A ][ S ][ D ][ F ]   ← items
        ///   [ 1 ][ 2 ][ 3 ][ 4 ]   ← actives
        ///   [        SPACE        ] ← center cam
        /// </summary>
        private static Win32KeyboardLayout CreateMOBALayout(int startX, int startY, int keyWidth, int keyHeight, int keySpacing)
        {
            var layout = new Win32KeyboardLayout();
            int x = startX;
            int y = startY;
            
            // Row 1: QWER (abilities) - CENTERED
            // Calculer le centre pour QWER (4 touches + 3 gaps)
            int qwerRowWidth = 4 * keyWidth + 3 * keySpacing;
            x = startX + (qwerRowWidth - (4 * keyWidth + 3 * keySpacing)) / 2; // Centrer QWER
            byte[] qwerKeys = { 0x51, 0x57, 0x45, 0x52 };
            string[] qwerLabels = { "Q", "W", "E", "R" };
            for (int i = 0; i < qwerKeys.Length; i++)
            {
                AddKey(layout, ref x, y, keyWidth, keyHeight, qwerKeys[i], qwerLabels[i], keySpacing);
            }
            
            // Row 2: A S D F (items) - séparés de QWER
            y += keyHeight + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x41, "A", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x53, "S", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x44, "D", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x46, "F", keySpacing);
            
            // Row 3: 1 2 3 4 (actives)
            y += keyHeight + keySpacing;
            x = startX;
            for (int i = 1; i <= 4; i++)
            {
                byte numKey = (byte)(0x31 + i - 1);
                AddKey(layout, ref x, y, keyWidth, keyHeight, numKey, i.ToString(), keySpacing);
            }
            
            // Row 4: SPACE (center cam)
            y += keyHeight + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, GetSpaceWidth(keyWidth), keyHeight, 0x20, "SPACE", keySpacing);
            
            return layout;
        }
        
        /// <summary>
        /// MMO Layout: WoW, FFXIV - Dense grid for many hotkeys
        /// Principe: Beaucoup de binds, groupes logiques, lecture rapide des rotations
        /// Layout:
        ///   [ 1 ][ 2 ][ 3 ][ 4 ][ 5 ][ 6 ][ 7 ][ 8 ]
        ///   [ Q ][ W ][ E ][ R ][ T ][ Y ][ U ][ I ]
        ///   [ A ][ S ][ D ][ F ][ G ][ H ][ J ][ K ]
        ///   [ Z ][ X ][ C ][ V ][ B ][ N ][ M ]
        ///   [SHIFT][CTRL][ALT]    ← modificateurs
        /// </summary>
        private static Win32KeyboardLayout CreateMMOLayout(KeyboardSize size, int startX, int startY, int keyWidth, int keyHeight, int keySpacing)
        {
            var layout = new Win32KeyboardLayout();
            int x = startX;
            int y = startY;
            
            // Row 1: Numbers 1-8 (main rotation)
            for (int i = 1; i <= 8; i++)
            {
                byte numKey = (byte)(0x31 + i - 1);
                AddKey(layout, ref x, y, keyWidth, keyHeight, numKey, i.ToString(), keySpacing);
            }
            
            // Row 2: Q-P (abilities)
            y += keyHeight + keySpacing;
            x = startX;
            byte[] row2Keys = { 0x51, 0x57, 0x45, 0x52, 0x54, 0x59, 0x55, 0x49 };
            foreach (var key in row2Keys)
            {
                AddKey(layout, ref x, y, keyWidth, keyHeight, key, key.ToString(), keySpacing);
            }
            
            // Row 3: A-K (more abilities)
            y += keyHeight + keySpacing;
            x = startX;
            byte[] row3Keys = { 0x41, 0x53, 0x44, 0x46, 0x47, 0x48, 0x4A, 0x4B };
            foreach (var key in row3Keys)
            {
                AddKey(layout, ref x, y, keyWidth, keyHeight, key, key.ToString(), keySpacing);
            }
            
            // Row 4: Z-M (utility)
            y += keyHeight + keySpacing;
            x = startX;
            byte[] row4Keys = { 0x5A, 0x58, 0x43, 0x56, 0x42, 0x4E, 0x4D };
            foreach (var key in row4Keys)
            {
                AddKey(layout, ref x, y, keyWidth, keyHeight, key, key.ToString(), keySpacing);
            }
            
            // Row 5: Modifiers (SHIFT/CTRL/ALT = x3 capacités)
            y += keyHeight + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, GetShiftWidth(keyWidth), keyHeight, 0xA0, "SHIFT", keySpacing);
            AddKey(layout, ref x, y, (int)(keyWidth * 1.5), keyHeight, 0xA2, "CTRL", keySpacing);
            AddKey(layout, ref x, y, (int)(keyWidth * 1.5), keyHeight, 0xA4, "ALT", keySpacing);
            
            return layout;
        }
        
        /// <summary>
        /// Racing Layout: Sim/Arcade - Minimal controls
        /// Principe: Peu de touches, priorité aux commandes critiques
        /// Layout:
        ///   [ W ] ← accélérer
        ///   [ S ] ← freiner
        ///   [ A ] ← gauche
        ///   [ D ] ← droite
        ///   [ SPACE ] ← frein à main
        ///   [ SHIFT ] ← nitro
        /// </summary>
        private static Win32KeyboardLayout CreateRacingLayout(int startX, int startY, int keyWidth, int keyHeight, int keySpacing)
        {
            var layout = new Win32KeyboardLayout();
            int x = startX;
            int y = startY;
            
            // Row 1: W (accélérer) - CENTERED au-dessus de A S D
            int racingKeySize = (int)(keyWidth * 1.5); // Plus gros pour visibilité
            // Calculer le centre: A S D = 3 touches + 2 gaps
            int asdRowWidth = 3 * racingKeySize + 2 * keySpacing;
            x = startX + (asdRowWidth - racingKeySize) / 2; // Centrer W au-dessus de A S D
            AddKey(layout, ref x, y, racingKeySize, racingKeySize, 0x57, "W", keySpacing);
            
            // Row 2: A S D (gauche, freiner, droite)
            y += racingKeySize + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, racingKeySize, racingKeySize, 0x41, "A", keySpacing);
            AddKey(layout, ref x, y, racingKeySize, racingKeySize, 0x53, "S", keySpacing);
            AddKey(layout, ref x, y, racingKeySize, racingKeySize, 0x44, "D", keySpacing);
            
            // Row 3: SPACE (frein à main)
            y += racingKeySize + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, GetSpaceWidth(keyWidth), keyHeight, 0x20, "SPACE", keySpacing);
            
            // Row 4: SHIFT (nitro)
            y += keyHeight + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, GetShiftWidth(keyWidth), keyHeight, 0xA0, "SHIFT", keySpacing);
            
            return layout;
        }
        
        /// <summary>
        /// Survival Layout: Minecraft, Rust - Craft + inventory
        /// Principe: Craft, inventaire, combat léger
        /// Layout:
        ///   [1][2][3][4][5][6][7][8][9]
        ///         [ W ]
        ///   [ A ]   [ S ]   [ D ]
        ///   [SHIFT][CTRL][ E ][ R ]
        ///   [ C ][ F ][ Q ]
        ///   [        SPACE        ]
        /// </summary>
        private static Win32KeyboardLayout CreateSurvivalLayout(int startX, int startY, int keyWidth, int keyHeight, int keySpacing)
        {
            var layout = new Win32KeyboardLayout();
            int x = startX;
            int y = startY;
            
            // Row 1: Numbers 1-9 (hotbar)
            for (int i = 1; i <= 9; i++)
            {
                byte numKey = (byte)(0x31 + i - 1);
                AddKey(layout, ref x, y, keyWidth, keyHeight, numKey, i.ToString(), keySpacing);
            }
            
            // Row 2: WASD en croix - W centré
            y += keyHeight + keySpacing;
            int survivalKeySize = (int)(keyWidth * 1.25);
            int wasdRowWidth = 3 * survivalKeySize + 2 * keySpacing;
            x = startX + (wasdRowWidth - survivalKeySize) / 2; // Centrer W
            AddKey(layout, ref x, y, survivalKeySize, survivalKeySize, 0x57, "W", keySpacing);
            
            // Row 3: A S D
            y += survivalKeySize + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, survivalKeySize, survivalKeySize, 0x41, "A", keySpacing);
            AddKey(layout, ref x, y, survivalKeySize, survivalKeySize, 0x53, "S", keySpacing);
            AddKey(layout, ref x, y, survivalKeySize, survivalKeySize, 0x44, "D", keySpacing);
            
            // Row 4: SHIFT CTRL E R (mouvement + interaction)
            y += survivalKeySize + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, GetShiftWidth(keyWidth), keyHeight, 0xA0, "SHIFT", keySpacing);
            AddKey(layout, ref x, y, (int)(keyWidth * 1.5), keyHeight, 0xA2, "CTRL", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x45, "E", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x52, "R", keySpacing);
            
            // Row 5: C F Q (craft, inventory, drop)
            y += keyHeight + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x43, "C", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x46, "F", keySpacing);
            AddKey(layout, ref x, y, keyWidth, keyHeight, 0x51, "Q", keySpacing);
            
            // Row 6: SPACE
            y += keyHeight + keySpacing;
            x = startX;
            AddKey(layout, ref x, y, GetSpaceWidth(keyWidth), keyHeight, 0x20, "SPACE", keySpacing);
            
            return layout;
        }
        
        private static Win32KeyboardLayout CreateGeneralLayout(KeyboardSize size, int startX, int startY, int keyWidth, int keyHeight, int keySpacing)
        {
            // General: Use full layout, size modifications will be applied later
            return Win32KeyboardLayout.CreateFullLayout(startX, startY, keyWidth, keyHeight, keySpacing);
        }
        
        private static void ApplySizeModifications(Win32KeyboardLayout layout, KeyboardSize size)
        {
            // Don't apply size modifications to gaming layouts (FPS, MOBA, Racing, Survival) - they already have the right size
            bool isGamingLayout = layout.Keys.Count < 50; // Gaming layouts have fewer keys
            
            if (isGamingLayout)
            {
                // Gaming layouts are already minimal - no size modifications needed
                return;
            }
            
            switch (size)
            {
                case KeyboardSize.TKL:
                    // TKL: Remove function row (F1-F12, ESC) - keep main keyboard
                    layout.Keys.RemoveAll(k => 
                        (k.VKeyCode >= 0x70 && k.VKeyCode <= 0x7B) ||
                        k.VKeyCode == 0x1B);
                    break;
                    
                case KeyboardSize.SeventyFive:
                    // 75%: Remove some function keys (keep ESC, remove some F keys)
                    // Remove F7-F12, keep F1-F6
                    layout.Keys.RemoveAll(k => 
                        (k.VKeyCode >= 0x76 && k.VKeyCode <= 0x7B));
                    break;
                    
                case KeyboardSize.SixtyFive:
                    // 65%: Remove function row completely
                    layout.Keys.RemoveAll(k => 
                        (k.VKeyCode >= 0x70 && k.VKeyCode <= 0x7B) ||
                        k.VKeyCode == 0x1B);
                    break;
                    
                case KeyboardSize.Sixty:
                    // 60%: For full layouts, convert to minimal (but this should use FPS layout instead)
                    // Remove function row and keep only essential keys
                    layout.Keys.RemoveAll(k => 
                        (k.VKeyCode >= 0x70 && k.VKeyCode <= 0x7B) ||
                        k.VKeyCode == 0x1B);
                    break;
                    
                case KeyboardSize.Full:
                default:
                    // Keep everything
                    break;
            }
        }
        
        private static void ApplyLayoutType(Win32KeyboardLayout layout, KeyboardLayoutType layoutType)
        {
            // Update labels for AZERTY/QWERTZ
            if (layoutType == KeyboardLayoutType.AZERTY)
            {
                UpdateLayoutForAZERTY(layout);
            }
            else if (layoutType == KeyboardLayoutType.QWERTZ)
            {
                UpdateLayoutForQWERTZ(layout);
            }
            // QWERTY is already the default - no changes needed
        }
        
        private static void UpdateLayoutForAZERTY(Win32KeyboardLayout layout)
        {
            // AZERTY: A/Q swapped, Z/W swapped, M moved
            // Detect if it's a gaming layout (has numbers 1-6 at the top)
            bool isGamingLayout = false;
            int numberCount = 0;
            foreach (var keyDef in layout.Keys)
            {
                if (keyDef.VKeyCode >= 0x31 && keyDef.VKeyCode <= 0x36 && keyDef.Y == layout.Keys[0].Y)
                {
                    numberCount++;
                }
            }
            isGamingLayout = (numberCount >= 6); // Gaming layouts have 1-6 in first row
            
            var keyMap = new Dictionary<byte, string>
            {
                { 0x51, "A" }, { 0x41, "Q" },
                { 0x57, "Z" }, { 0x5A, "W" },
                { 0x4D, "," }, { 0xBC, "M" },
                { 0x37, "è" }, { 0x38, "_" },
                { 0x39, "ç" }, { 0x30, "à" }, 
                { 0xBD, ")" }, { 0xBB, "=" },
                { 0xDB, "^" }, { 0xDD, "$" },
                { 0xBA, "ù" }, { 0xDE, "*" },
                { 0xBE, ":" }, { 0xBF, "!" }
            };
            
            foreach (var keyDef in layout.Keys)
            {
                // For gaming layouts, keep numbers 1-6 as numbers
                if (isGamingLayout && keyDef.VKeyCode >= 0x31 && keyDef.VKeyCode <= 0x36)
                {
                    // Keep as numbers - don't change
                    continue;
                }
                
                // Apply key mapping
                if (keyMap.TryGetValue(keyDef.VKeyCode, out string newLabel))
                {
                    keyDef.Label = newLabel;
                }
                // For full layouts, also change numbers 1-6
                else if (!isGamingLayout && keyDef.VKeyCode >= 0x31 && keyDef.VKeyCode <= 0x36)
                {
                    switch (keyDef.VKeyCode)
                    {
                        case 0x31: keyDef.Label = "&"; break;
                        case 0x32: keyDef.Label = "é"; break;
                        case 0x33: keyDef.Label = "\""; break;
                        case 0x34: keyDef.Label = "'"; break;
                        case 0x35: keyDef.Label = "("; break;
                        case 0x36: keyDef.Label = "-"; break;
                    }
                }
            }
        }
        
        private static void UpdateLayoutForQWERTZ(Win32KeyboardLayout layout)
        {
            // QWERTZ: Y/Z swapped
            var keyMap = new Dictionary<byte, string>
            {
                { 0x59, "Z" }, { 0x5A, "Y" },
                { 0xDB, "Ü" }, { 0xDD, "+" },
                { 0xBA, "Ö" }, { 0xDE, "Ä" },
                { 0xBD, "ß" }, { 0xBB, "´" },
                { 0xBE, "-" }
            };
            
            foreach (var keyDef in layout.Keys)
            {
                if (keyMap.TryGetValue(keyDef.VKeyCode, out string newLabel))
                {
                    keyDef.Label = newLabel;
                }
            }
        }
    }
}
