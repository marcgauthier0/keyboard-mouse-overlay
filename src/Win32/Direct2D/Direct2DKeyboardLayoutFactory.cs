using GamingKeypressOverlay.Overlay;

namespace GamingKeypressOverlay.Win32.Direct2D
{
    /// <summary>
    /// Factory for creating Direct2D keyboard layouts
    /// Provides a clean, extensible way to manage different keyboard layouts
    /// </summary>
    public static class Direct2DKeyboardLayoutFactory
    {
        /// <summary>
        /// Create a keyboard layout based on the layout type and game config
        /// </summary>
        public static IDirect2DKeyboardLayout CreateLayout(KeyboardLayoutType layoutType, GameConfig gameConfig = GameConfig.General)
        {
            // First, get layout based on game config
            IDirect2DKeyboardLayout baseLayout = gameConfig switch
            {
                GameConfig.FPS => new Direct2DKeyboardLayoutFPS(),
                GameConfig.MOBA => new Direct2DKeyboardLayoutMOBA(),
                GameConfig.MMO => new Direct2DKeyboardLayoutMMO(),
                GameConfig.Racing => new Direct2DKeyboardLayoutRacing(),
                GameConfig.Survival => new Direct2DKeyboardLayoutSurvival(),
                GameConfig.General => CreateLayoutByType(layoutType),
                _ => CreateLayoutByType(layoutType)
            };

            return baseLayout;
        }

        /// <summary>
        /// Create a layout based on keyboard type only (for General config)
        /// </summary>
        private static IDirect2DKeyboardLayout CreateLayoutByType(KeyboardLayoutType layoutType)
        {
            return layoutType switch
            {
                KeyboardLayoutType.QWERTY => new Direct2DKeyboardLayoutQWERTY(),
                KeyboardLayoutType.AZERTY => new Direct2DKeyboardLayoutAZERTY(),
                KeyboardLayoutType.QWERTZ => new Direct2DKeyboardLayoutQWERTZ(),
                _ => new Direct2DKeyboardLayoutQWERTY() // Default fallback
            };
        }
    }
}
