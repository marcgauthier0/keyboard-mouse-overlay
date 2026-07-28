using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using GamingKeypressOverlay.Input;
using GamingKeypressOverlay.Overlay;
using GamingKeypressOverlay.Settings;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// GDI+ renderer for keyboard/mouse overlay
    /// Refactored to use separate modules: Keyboard, Mouse, Interface
    /// </summary>
    public unsafe class GDIRenderer : IDisposable
    {
        private IntPtr _hwnd;
        private bool _disposed = false;
        
        // Shared rendering context
        private GDIRenderContext _context;
        
        // Rendering modules
        private GDIRendererKeyboard _keyboardRenderer;
        private GDIRendererMouse _mouseRenderer;
        private GDIRendererInterface _interfaceRenderer;
        
        // Layout constants
        private const int KEY_WIDTH = 40;
        private const int KEY_HEIGHT = 40;
        private const int KEY_SPACING = 4;
        private const int GLOBAL_PADDING = 50; // Global padding/margin around the entire application
        private const int KEYBOARD_X = 50; // Will be adjusted with global padding
        private const int KEYBOARD_Y = 100; // Will be adjusted with global padding
        private const int MOUSE_WIDTH = 280;
        private const int MOUSE_HEIGHT = 200;
        private const int KEYBOARD_MOUSE_SPACING = 60;
        private const int TILE_HEIGHT = 60;
        private const int SIDE_BUTTON_OFFSET = 30; // Distance from mouse left edge to side buttons
        
        // Keyboard layout
        private Win32KeyboardLayout _keyboardLayout;
        private bool _isGamingMode = false;
        private KeyboardLayoutType _layoutType = KeyboardLayoutType.QWERTY;
        private KeyboardSize _keyboardSize = KeyboardSize.Full;
        private GameConfig _gameConfig = GameConfig.General;
        
        // Mouse position
        private bool _mouseOnRight = true; // Default: right side (right-handed)
        
        // Public properties
        public OverlayStyle CurrentStyle => _context.CurrentStyle;
        public KeyboardLayoutType CurrentLayoutType => _layoutType;
        public GameConfig CurrentGameConfig => _gameConfig;
        public bool MouseOnRight => _mouseOnRight;
        
        // Check if transparent background is enabled
        public bool IsTransparentBackgroundEnabled() => _context?.UseTransparentBackground ?? false;
        
        public GDIRenderer(IntPtr hwnd, OverlayStyle style = OverlayStyle.Custom, bool gamingMode = false)
        {
            _hwnd = hwnd;
            _isGamingMode = gamingMode;
            
            // Initialize context
            _context = new GDIRenderContext
            {
                CurrentStyle = style,
                Theme = StyleManager.GetTheme(style),
                KeyFont = new Font("Consolas", 12, FontStyle.Bold),
                TitleFont = new Font("Consolas", 16, FontStyle.Bold)
            };
            
            // Update theme brushes
            UpdateTheme();
            
            // Create keyboard layout
            UpdateKeyboardLayout();
            
            // Initialize rendering modules
            _keyboardRenderer = new GDIRendererKeyboard(_context, _keyboardLayout);
            _mouseRenderer = new GDIRendererMouse(_context, _keyboardLayout, _mouseOnRight);
            _interfaceRenderer = new GDIRendererInterface(_context);
        }
        
        private void UpdateKeyboardLayout()
        {
            // Use FPS config when gaming mode is enabled, otherwise use current game config
            GameConfig configToUse = _isGamingMode ? GameConfig.FPS : _gameConfig;
            
            // Calculer la position X du clavier selon la position de la souris
            // Add global padding + space for side buttons when mouse is on left
            int keyboardStartX = KEYBOARD_X;
            if (!_mouseOnRight)
            {
                // When mouse is on left: keyboard needs space for mouse + side buttons + spacing
                int mouseLeftOffset = MOUSE_WIDTH + SIDE_BUTTON_OFFSET + KEYBOARD_MOUSE_SPACING;
                keyboardStartX = GLOBAL_PADDING + mouseLeftOffset;
            }
            else
            {
                keyboardStartX = GLOBAL_PADDING;
            }
            
            // Calculate keyboard Y position with global padding
            int keyboardStartY = GLOBAL_PADDING + TILE_HEIGHT + 10; // padding + tile + spacing
            
            _keyboardLayout = KeyboardLayoutBuilder.CreateLayout(
                _layoutType,
                _keyboardSize,
                configToUse,
                keyboardStartX, keyboardStartY, KEY_WIDTH, KEY_HEIGHT, KEY_SPACING
            );
            
            // Update renderers with new layout
            _keyboardRenderer = new GDIRendererKeyboard(_context, _keyboardLayout);
            _mouseRenderer = new GDIRendererMouse(_context, _keyboardLayout, _mouseOnRight);
            
            System.Diagnostics.Debug.WriteLine($"UpdateKeyboardLayout: GamingMode={_isGamingMode}, GameConfig={configToUse}, KeyboardSize={_keyboardSize}, KeyboardX={keyboardStartX}, Keys count={_keyboardLayout.Keys.Count}");
        }
        
        private void UpdateTheme()
        {
            // Dispose old brushes
            _context.KeyBrush?.Dispose();
            _context.PressedKeyBrush?.Dispose();
            _context.TextBrush?.Dispose();
            _context.PressedTextBrush?.Dispose();
            _context.BackgroundBrush?.Dispose();
            _context.KeyBorderPen?.Dispose();
            _context.PressedKeyBorderPen?.Dispose();
            
            // Convert WPF colors to GDI+ colors
            Color keyIdleColor = _context.BrushToColor(_context.Theme.KeyIdleBackground);
            Color keyPressedColor = _context.BrushToColor(_context.Theme.KeyPressedBackground);
            Color textIdleColor = _context.BrushToColor(_context.Theme.KeyIdleForeground);
            Color textPressedColor = _context.BrushToColor(_context.Theme.KeyPressedForeground);
            Color borderIdleColor = _context.BrushToColor(_context.Theme.KeyBorder);
            Color borderPressedColor = _context.BrushToColor(_context.Theme.KeyPressedBorder ?? _context.Theme.PrimaryColor);
            Color bgColor = _context.BrushToColor(_context.Theme.BackgroundBrush);
            
            // Create brushes
            _context.KeyBrush = new SolidBrush(keyIdleColor);
            _context.PressedKeyBrush = new SolidBrush(keyPressedColor);
            _context.TextBrush = new SolidBrush(textIdleColor);
            _context.PressedTextBrush = new SolidBrush(textPressedColor);
            _context.BackgroundBrush = new SolidBrush(bgColor);
            
            // Create pens
            _context.KeyBorderPen = new Pen(borderIdleColor, 2);
            _context.PressedKeyBorderPen = new Pen(borderPressedColor, 3);
        }
        
        public void SetGamingMode(bool gamingMode)
        {
            if (_isGamingMode != gamingMode)
            {
                _isGamingMode = gamingMode;
                UpdateKeyboardLayout();
                System.Diagnostics.Debug.WriteLine($"Gaming mode changed to: {gamingMode}, Keys count: {_keyboardLayout.Keys.Count}");
            }
        }
        
        public void SetLayoutType(KeyboardLayoutType layoutType)
        {
            if (_layoutType != layoutType)
            {
                _layoutType = layoutType;
                UpdateKeyboardLayout();
                System.Diagnostics.Debug.WriteLine($"Layout type changed to: {layoutType}, Keys count: {_keyboardLayout.Keys.Count}");
            }
        }
        
        public void SetKeyboardSize(KeyboardSize size)
        {
            if (_keyboardSize != size)
            {
                _keyboardSize = size;
                UpdateKeyboardLayout();
                System.Diagnostics.Debug.WriteLine($"Keyboard size changed to: {size}, Keys count: {_keyboardLayout.Keys.Count}");
            }
        }
        
        public void SetGameConfig(GameConfig config)
        {
            if (_gameConfig != config)
            {
                _gameConfig = config;
                UpdateKeyboardLayout();
                System.Diagnostics.Debug.WriteLine($"Game config changed to: {config}, Keys count: {_keyboardLayout.Keys.Count}");
            }
        }
        
        public void SetMousePosition(bool onRight)
        {
            if (_mouseOnRight != onRight)
            {
                _mouseOnRight = onRight;
                UpdateKeyboardLayout();
                // Recreate mouse renderer with new position
                string savedStyle = "Gaming";
                if (_mouseRenderer != null)
                {
                    // Try to preserve style by checking settings
                    var settings = SettingsManager.LoadSettings();
                    savedStyle = settings.MouseStyle ?? "Gaming";
                }
                _mouseRenderer = new GDIRendererMouse(_context, _keyboardLayout, _mouseOnRight);
                _mouseRenderer.SetMouseStyle(savedStyle);
                System.Diagnostics.Debug.WriteLine($"Mouse position changed to: {(onRight ? "Right" : "Left")}");
            }
        }
        
        public void SetStyle(OverlayStyle style)
        {
            if (_context.CurrentStyle != style)
            {
                _context.CurrentStyle = style;
                _context.Theme = StyleManager.GetTheme(style);
                UpdateTheme();
            }
        }

        public void SetTheme(OverlayTheme theme)
        {
            _context.Theme = theme ?? throw new ArgumentNullException(nameof(theme));
            UpdateTheme();
        }
        
        /// <summary>
        /// Set mouse rendering style
        /// </summary>
        public void SetMouseStyle(string style)
        {
            if (_mouseRenderer != null)
            {
                _mouseRenderer.SetMouseStyle(style);
            }
        }
        
        /// <summary>
        /// Set a custom logo image.
        /// </summary>
        public void SetCustomLogo(string imagePath)
        {
            _interfaceRenderer?.SetCustomLogo(imagePath);
        }
        
        /// <summary>
        /// Enable or disable the animated background.
        /// </summary>
        public void SetAnimatedBackground(bool enabled)
        {
            _interfaceRenderer?.SetAnimatedBackground(enabled);
        }
        
        /// <summary>
        /// Calculate total width needed for keyboard + mouse layout
        /// </summary>
        public (int totalWidth, int totalHeight) CalculateRequiredSize()
        {
            // Calculate keyboard dimensions
            int keyboardWidth = 0;
            int keyboardHeight = 0;
            if (_keyboardLayout != null && _keyboardLayout.Keys.Count > 0)
            {
                foreach (var key in _keyboardLayout.Keys)
                {
                    int keyRight = key.X + key.Width;
                    int keyBottom = key.Y + KEY_HEIGHT;
                    if (keyRight > keyboardWidth)
                    {
                        keyboardWidth = keyRight;
                    }
                    if (keyBottom > keyboardHeight)
                    {
                        keyboardHeight = keyBottom;
                    }
                }
                keyboardWidth = keyboardWidth - KEYBOARD_X;
            }
            else
            {
                keyboardWidth = 1200;
                keyboardHeight = 300;
            }
            
            // Check if mouse should be displayed
            string mouseStyle = "Gaming";
            if (_mouseRenderer != null)
            {
                // Get mouse style from settings
                var settings = SettingsManager.LoadSettings();
                mouseStyle = settings.MouseStyle ?? "Gaming";
            }
            bool showMouse = mouseStyle != "None";
            
            // Calculate total width: keyboard + spacing + mouse + global padding
            int totalWidth;
            if (showMouse)
            {
                if (_mouseOnRight)
                {
                    // Right: padding + keyboard + spacing + mouse + padding
                    totalWidth = GLOBAL_PADDING + keyboardWidth + KEYBOARD_MOUSE_SPACING + MOUSE_WIDTH + GLOBAL_PADDING;
                }
                else
                {
                    // Left: padding + side buttons + mouse + spacing + keyboard + padding
                    int mouseX = GLOBAL_PADDING + SIDE_BUTTON_OFFSET;
                    int keyboardX = mouseX + MOUSE_WIDTH + KEYBOARD_MOUSE_SPACING;
                    int keyboardRightX = keyboardX + keyboardWidth;
                    totalWidth = keyboardRightX + GLOBAL_PADDING;
                }
            }
            else
            {
                // No mouse: just keyboard + padding
                totalWidth = GLOBAL_PADDING + keyboardWidth + GLOBAL_PADDING;
            }
            
            // Calculate total height: take the maximum of keyboard height and mouse height + global padding
            int tilesHeight = TILE_HEIGHT + 10;
            int mouseHeight = showMouse ? MOUSE_HEIGHT : 0;
            int maxContentHeight = Math.Max(keyboardHeight, mouseHeight);
            // Top padding + tile + spacing + content + bottom padding
            int totalHeight = GLOBAL_PADDING + tilesHeight + maxContentHeight + GLOBAL_PADDING;
            
            return (totalWidth, totalHeight);
        }
        
        public unsafe void Render(InputStateSnapshot snapshot, IntPtr hdc)
        {
            if (_disposed || snapshot == null) return;
            
            try
            {
                // Update animation time
                _context.AnimationTime = Environment.TickCount;
                
                using (Graphics g = Graphics.FromHdc(hdc))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    
                    // Get window dimensions from graphics
                    int width = (int)g.VisibleClipBounds.Width;
                    int height = (int)g.VisibleClipBounds.Height;
                    
                    // Render background (animated or static)
                    _interfaceRenderer.RenderBackground(g, width, height);
                    
                    // Render keyboard
                    _keyboardRenderer.RenderKeyboard(g, snapshot);
                    
                    // Render Last Input
                    _keyboardRenderer.RenderLastInput(g, snapshot);
                    
                    // Render Active Keys (currently disabled, but available)
                    // _keyboardRenderer.RenderActiveKeys(g, snapshot);
                    
                    // Render mouse (last, so it's on top)
                    _mouseRenderer.RenderMouse(g, snapshot);
                    
                    // Render the custom logo when configured.
                    _interfaceRenderer.RenderCustomLogo(g, width, height);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GDI render error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Enable/disable transparent background mode (for OBS/TikTok Studio capture)
        /// </summary>
        public void SetTransparentBackground(bool transparent)
        {
            if (_context != null)
            {
                _context.UseTransparentBackground = transparent;
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            // Dispose context resources
            _context.KeyFont?.Dispose();
            _context.TitleFont?.Dispose();
            _context.KeyBrush?.Dispose();
            _context.PressedKeyBrush?.Dispose();
            _context.TextBrush?.Dispose();
            _context.PressedTextBrush?.Dispose();
            _context.BackgroundBrush?.Dispose();
            _context.KeyBorderPen?.Dispose();
            _context.PressedKeyBorderPen?.Dispose();
            
            // Dispose interface renderer (handles logo)
            _interfaceRenderer?.Dispose();
        }
    }
}
