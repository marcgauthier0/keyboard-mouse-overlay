using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using GamingKeypressOverlay.Input;
using GamingKeypressOverlay.Overlay;
using GamingKeypressOverlay.Win32.Direct2D;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Direct2D renderer for ultra-low latency keyboard/mouse overlay
    /// Uses Vortice.Windows for GPU-accelerated rendering (240 FPS capable)
    /// Replaces GDI for professional gaming overlay look (XAML-style but fast)
    /// </summary>
    public class Direct2DRenderer : IDisposable
    {
        private readonly IntPtr _hwnd;
        private readonly int _width;
        private readonly int _height;
        private bool _disposed = false;
        
        // Direct2D core
        private ID2D1Factory _d2dFactory;
        private ID2D1HwndRenderTarget _renderTarget;
        private ID2D1DeviceContext _deviceContext;
        
        // DirectWrite for text
        private IDWriteFactory _writeFactory;
        private IDWriteTextFormat _textFormat;
        private IDWriteTextFormat _titleFormat;
        
        // Brushes (reused, created once)
        private ID2D1SolidColorBrush _neonGreenBrush;
        private ID2D1SolidColorBrush _whiteBrush;
        private ID2D1SolidColorBrush _blackBrush;
        private ID2D1SolidColorBrush _transparentBrush;
        
        // Theme
        private OverlayTheme _theme;
        private GDIRenderContext _context;
        
        // Mouse renderer
        private Direct2DMouseRenderer _mouseRenderer;
        
        // Animation
        private float _animationTime = 0f;

        // Mouse position (left/right/none)
        private bool _mouseOnRight = true;
        private bool _mouseVisible = true; // Show/hide mouse
        
        // Last input display (like GDI)
        private string _lastDisplayedInput = "---";
        private long _lastInputDisplayTime = 0;
        private const long LAST_INPUT_DISPLAY_DURATION_MS = 2000;
        
        // Layout constants
        private const int GLOBAL_PADDING = 20;
        private const int KEYBOARD_MOUSE_SPACING = 60; // Fixed spacing (same as GDI, both sides)
        private const int SIDE_BUTTON_OFFSET = 30; // Distance from mouse left edge to side buttons (same as GDI)
        private const int MOUSE_WIDTH = 260; // Direct2D mouse width (240 + padding) - narrower
        private const int MOUSE_HEIGHT = 300; // Direct2D mouse height (280 + padding) - longer
        private const int KEYBOARD_WIDTH = 900;
        private const int KEYBOARD_HEIGHT = 380;

        // Keyboard layout system (modular, extensible)
        private IDirect2DKeyboardLayout _keyboardLayout;
        private KeyboardLayoutType _currentLayoutType = KeyboardLayoutType.QWERTY;
        private GameConfig _currentGameConfig = GameConfig.General;

        private const int KEY_SIZE = 32; // Reduced from 38 for thinner keys
        private const int KEY_SPACING = 6; // Increased for more spacing between keys
        private const float KEY_ROUNDNESS = 6f; // Rounded corners for keys
        private float glowPhase = 0;
        
        public Direct2DRenderer(IntPtr hwnd, int width, int height, OverlayTheme theme, GDIRenderContext context)
        {
            _hwnd = hwnd;
            _width = width;
            _height = height;
            _theme = theme;
            _context = context;
            
            // Create Direct2D factory (single-threaded for performance)
            _d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory>(Vortice.Direct2D1.FactoryType.SingleThreaded);
            
            // Create DirectWrite factory
            _writeFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();
            
            // Create render target
            CreateRenderTarget(width, height);
            
            // Create resources
            CreateResources();
            
            // Create mouse renderer (deviceContext can be null, will use renderTarget)
            _mouseRenderer = new Direct2DMouseRenderer(_d2dFactory, _writeFactory, null);
            
            // Initialize keyboard layout
            _keyboardLayout = Direct2DKeyboardLayoutFactory.CreateLayout(_currentLayoutType, _currentGameConfig);
        }
        
        private void CreateRenderTarget(int width, int height)
        {
            // Render target properties - use default pixel format for now
            var renderTargetProperties = new RenderTargetProperties
            {
                DpiX = 96.0f,
                DpiY = 96.0f,
                Type = RenderTargetType.Hardware, // GPU acceleration
                MinLevel = FeatureLevel.Level_10
            };

            // Hwnd render target properties
            // PresentOptions.Immediately ensures immediate rendering without waiting for VSync
            // This reduces flickering by presenting frames as soon as they're ready
            var hwndProperties = new HwndRenderTargetProperties
            {
                Hwnd = _hwnd,
                PixelSize = new System.Drawing.Size(width, height),
                PresentOptions = PresentOptions.Immediately // Immediate presentation to reduce flicker
            };

            // Create Hwnd render target
            _renderTarget = _d2dFactory.CreateHwndRenderTarget(renderTargetProperties, hwndProperties);

            // Get device context for advanced features
            _deviceContext = _renderTarget.QueryInterface<ID2D1DeviceContext>();
        }
        
        private void CreateResources()
        {
            // Create text formats
            _textFormat = _writeFactory.CreateTextFormat(
                "Consolas",
                null,
                FontWeight.Bold,
                FontStyle.Normal,
                FontStretch.Normal,
                12.0f);
            _textFormat.TextAlignment = TextAlignment.Center;
            _textFormat.ParagraphAlignment = ParagraphAlignment.Center;
            
            _titleFormat = _writeFactory.CreateTextFormat(
                "Segoe UI",
                null,
                FontWeight.Bold,
                FontStyle.Normal,
                FontStretch.Normal,
                16.0f);
            _titleFormat.TextAlignment = TextAlignment.Center;
            _titleFormat.ParagraphAlignment = ParagraphAlignment.Center;
            
            // Create brushes
            _neonGreenBrush = _renderTarget.CreateSolidColorBrush(new Color4(0f, 1f, 0.3f, 1f));
            _whiteBrush = _renderTarget.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
            _blackBrush = _renderTarget.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0.4f));
            _transparentBrush = _renderTarget.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0f));
        }
        
        public void Resize(int width, int height)
        {
            if (_renderTarget != null)
            {
                _renderTarget.Resize(new System.Drawing.Size(width, height));
            }
        }
        
        public void Render(InputStateSnapshot snapshot)
        {
            if (_disposed || _renderTarget == null)
                return;
            
            // Update animation time
            _animationTime += 0.016f; // ~60fps increment (will be 240fps in practice)
            
            // Begin drawing
            _renderTarget.BeginDraw();
            
            // Respect the user's background color unless transparent capture is enabled.
            System.Drawing.Color backgroundColor = _context.BrushToColor(_theme.BackgroundBrush);
            _renderTarget.Clear(_context.UseTransparentBackground
                ? new Color4(0f, 0f, 0f, 0f)
                : new Color4(backgroundColor.R / 255f, backgroundColor.G / 255f,
                    backgroundColor.B / 255f, backgroundColor.A / 255f));
            
            // Calculate actual keyboard dimensions (width and height)
            var (actualKeyboardWidth, actualKeyboardHeight) = CalculateKeyboardDimensions();
            
            // Calculate positions based on mouse position and visibility
            int keyboardX, keyboardY;
            int mouseX, mouseY;
            
            if (!_mouseVisible)
            {
                // Mouse hidden: Keyboard centered or left-aligned
                keyboardX = GLOBAL_PADDING;
                keyboardY = GLOBAL_PADDING;
                mouseX = 0; // Not used
                mouseY = 0; // Not used
            }
            else if (_mouseOnRight)
            {
                // Mouse on right: Keyboard left, Mouse right
                // Use actual keyboard width instead of fixed KEYBOARD_WIDTH
                keyboardX = GLOBAL_PADDING;
                keyboardY = GLOBAL_PADDING;
                mouseX = GLOBAL_PADDING + actualKeyboardWidth + KEYBOARD_MOUSE_SPACING;
                
                // Center mouse vertically relative to keyboard
                // Keyboard total height = actualKeyboardHeight (includes last input tile)
                // Mouse should be centered: keyboardY + (keyboardHeight / 2) - (mouseHeight / 2)
                mouseY = keyboardY + (actualKeyboardHeight / 2) - (MOUSE_HEIGHT / 2);
            }
            else
            {
                // Mouse on left: Mouse left, Keyboard right
                // Add SIDE_BUTTON_OFFSET to account for side buttons extending to the left of mouse
                mouseX = GLOBAL_PADDING;
                keyboardX = GLOBAL_PADDING + MOUSE_WIDTH + SIDE_BUTTON_OFFSET + KEYBOARD_MOUSE_SPACING;
                keyboardY = GLOBAL_PADDING;
                
                // Center mouse vertically relative to keyboard
                mouseY = keyboardY + (actualKeyboardHeight / 2) - (MOUSE_HEIGHT / 2);
            }
            
            System.Diagnostics.Debug.WriteLine($"Direct2DRenderer.Render: MouseOnRight={_mouseOnRight}, MouseX={mouseX}, MouseY={mouseY}, KeyboardX={keyboardX}, KeyboardY={keyboardY}");
            
            // Render mouse first (if on left and visible) or keyboard first (if on right)
            // This ensures proper layering
            if (!_mouseOnRight && _mouseVisible && _mouseRenderer != null && snapshot != null)
            {
                // Save current transform
                var originalTransform = _renderTarget.Transform;
                var mouseTransform = Matrix3x2.CreateTranslation(mouseX, mouseY);
                _renderTarget.Transform = mouseTransform;

                _mouseRenderer.Render(
                    _renderTarget,
                    snapshot,
                    _theme,
                    _context,
                    _animationTime
                );

                _renderTarget.Transform = originalTransform;
            }
            
            // Render keyboard
            if (snapshot != null)
            {
                System.Diagnostics.Debug.WriteLine($"Direct2DRenderer: Rendering keyboard at ({keyboardX}, {keyboardY})");
                RenderKeyboard(_renderTarget, snapshot, keyboardX, keyboardY);
            }

            // Render mouse (if on right and visible)
            if (_mouseOnRight && _mouseVisible && _mouseRenderer != null && snapshot != null)
            {
                System.Diagnostics.Debug.WriteLine($"Direct2DRenderer: Rendering mouse on RIGHT at ({mouseX}, {mouseY})");
                // Save current transform
                var originalTransform = _renderTarget.Transform;
                var mouseTransform = Matrix3x2.CreateTranslation(mouseX, mouseY);
                _renderTarget.Transform = mouseTransform;

                _mouseRenderer.Render(
                    _renderTarget,
                    snapshot,
                    _theme,
                    _context,
                    _animationTime
                );

                _renderTarget.Transform = originalTransform;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Direct2DRenderer: NOT rendering mouse - OnRight={_mouseOnRight}, Visible={_mouseVisible}, Renderer={_mouseRenderer != null}, Snapshot={snapshot != null}");
            }
            
            // End drawing
            try
            {
                _renderTarget.EndDraw();
            }
            catch (Exception ex)
                when (ex.HResult == unchecked((int)0x8899000C)) // D2DERR_RECREATE_TARGET
            {
                // Handle device lost - would need to recreate resources
                // For now, just skip this frame
            }
        }

        /// <summary>
        /// Calculate actual keyboard width and height from layout
        /// </summary>
        private (int width, int height) CalculateKeyboardDimensions()
        {
            if (_keyboardLayout == null)
            {
                return (KEYBOARD_WIDTH, KEYBOARD_HEIGHT); // Fallback to constants
            }
            
            var layout = _keyboardLayout.GetLayout();
            if (layout == null || layout.Length == 0)
            {
                return (KEYBOARD_WIDTH, KEYBOARD_HEIGHT); // Fallback to constants
            }
            
            int maxWidth = 0;
            int maxHeight = 0;
            const int KEYBOARD_START_OFFSET = 10; // Keys start at startX + 10
            
            for(int row = 0; row < layout.Length; row++)
            {
                int x = KEYBOARD_START_OFFSET;
                int y = row * (KEY_SIZE + KEY_SPACING);
                maxHeight = y + KEY_SIZE; // Track actual keyboard height

                for(int col = 0; col < layout[row].Length; col++)
                {
                    string key = layout[row][col];
                    if(string.IsNullOrEmpty(key)) {
                        x += KEY_SPACING * 2;
                        continue;
                    }

                    // Get key width from layout
                    int widthMultiplier = _keyboardLayout.GetKeyWidth(key);
                    int width = KEY_SIZE * widthMultiplier;
                    
                    int keyRight = x + width;
                    if (keyRight > maxWidth)
                    {
                        maxWidth = keyRight;
                    }

                    x += width + KEY_SPACING;
                }
            }
            
            // Add space for last input tile
            maxHeight += 75; // 60px tile + 15px spacing
            
            return (maxWidth, maxHeight);
        }

        private void RenderKeyboard(ID2D1RenderTarget renderTarget, InputStateSnapshot snapshot, int startX, int startY)
        {
            if (_context == null || _keyboardLayout == null) 
            {
                System.Diagnostics.Debug.WriteLine($"Direct2DRenderer.RenderKeyboard: _context={_context != null}, _keyboardLayout={_keyboardLayout != null}");
                return;
            }

            glowPhase += 0.08f; // Animation phase

            // Get theme colors
            System.Drawing.Color primaryColor = _context.BrushToColor(_theme.PrimaryColor);
            System.Drawing.Color keyPressedColor = _context.BrushToColor(_theme.KeyPressedBackground);
            System.Drawing.Color keyIdleColor = _context.BrushToColor(_theme.KeyIdleBackground);
            System.Drawing.Color textColor = _context.BrushToColor(_theme.KeyIdleForeground);
            System.Drawing.Color pressedTextColor = _context.BrushToColor(_theme.KeyPressedForeground);
            
            // Create brushes from theme colors
            var primaryBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColor.R / 255f, primaryColor.G / 255f, primaryColor.B / 255f, primaryColor.A / 255f));
            var pressedBrush = renderTarget.CreateSolidColorBrush(new Color4(
                keyPressedColor.R / 255f, keyPressedColor.G / 255f, keyPressedColor.B / 255f, keyPressedColor.A / 255f));
            var idleBrush = renderTarget.CreateSolidColorBrush(new Color4(
                keyIdleColor.R / 255f, keyIdleColor.G / 255f, keyIdleColor.B / 255f, keyIdleColor.A / 255f));
            var textBrush = renderTarget.CreateSolidColorBrush(new Color4(
                textColor.R / 255f, textColor.G / 255f, textColor.B / 255f, textColor.A / 255f));
            var pressedTextBrush = renderTarget.CreateSolidColorBrush(new Color4(
                pressedTextColor.R / 255f, pressedTextColor.G / 255f, pressedTextColor.B / 255f, pressedTextColor.A / 255f));

            // Render all keys using the current layout (no title, no border)
            if (_keyboardLayout == null)
            {
                System.Diagnostics.Debug.WriteLine("Direct2DRenderer.RenderKeyboard: _keyboardLayout is null! Recreating...");
                _keyboardLayout = Direct2DKeyboardLayoutFactory.CreateLayout(_currentLayoutType);
            }
            
            var layout = _keyboardLayout.GetLayout();
            if (layout == null || layout.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"Direct2DRenderer.RenderKeyboard: Layout is null or empty! CurrentLayoutType={_currentLayoutType}");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"Direct2DRenderer.RenderKeyboard: Using layout {_currentLayoutType} with {layout.Length} rows");
            int keyboardHeight = 0;
            
            for(int row = 0; row < layout.Length; row++)
            {
                int x = startX + 10;
                int y = startY + row * (KEY_SIZE + KEY_SPACING);
                keyboardHeight = y + KEY_SIZE; // Track actual keyboard height

                for(int col = 0; col < layout[row].Length; col++)
                {
                    string key = layout[row][col];
                    if(string.IsNullOrEmpty(key)) {
                        x += KEY_SPACING * 2;
                        continue;
                    }

                    // Get key width from layout
                    int widthMultiplier = _keyboardLayout.GetKeyWidth(key);
                    int width = KEY_SIZE * widthMultiplier;

                    bool isPressed = IsKeyPressed(snapshot, key);
                    RenderGamingKey(renderTarget, x, y, width, KEY_SIZE, key, isPressed,
                        primaryBrush, pressedBrush, idleBrush, textBrush, pressedTextBrush);

                    x += width + KEY_SPACING;
                }
            }

            // Render last input tile (below keyboard, like GDI style)
            string lastInput = GetLastInput(snapshot);
            int tileY = keyboardHeight + 15; // Small spacing after keyboard
            int tileHeight = 60; // Match GDI TILE_HEIGHT
            
            // Calculate SPACE key width to limit tile width
            int spaceWidth = 0;
            var layoutForSpace = _keyboardLayout.GetLayout();
            for (int row = 0; row < layoutForSpace.Length; row++)
            {
                for (int col = 0; col < layoutForSpace[row].Length; col++)
                {
                    if (layoutForSpace[row][col] == "SPACE")
                    {
                        int widthMultiplier = _keyboardLayout.GetKeyWidth("SPACE");
                        spaceWidth = KEY_SIZE * widthMultiplier;
                        break;
                    }
                }
                if (spaceWidth > 0) break;
            }
            
            // Tile width = SPACE width + padding (max 300px like GDI)
            int tileWidth = Math.Min(spaceWidth + 40, 300);
            
            // Align tile with keyboard keys start (keys start at startX + 10)
            int tileX = startX + 10;
            
            // Draw tile background (like GDI)
            var tileRect = new System.Drawing.RectangleF(tileX, tileY, tileWidth, tileHeight);
            renderTarget.FillRectangle(tileRect, idleBrush);
            
            // Draw tile border
            renderTarget.DrawRectangle(tileRect, primaryBrush, 2.0f);
            
            // Draw "Last Input:" label on top (like GDI) - left aligned
            var labelRect = new System.Drawing.RectangleF(tileX + 10, tileY + 5, tileWidth - 20, 20);
            renderTarget.DrawText("Last Input:", _textFormat, labelRect, primaryBrush);
            
            // Draw last input value below label (larger font, like GDI) - left aligned, may truncate if too long
            var valueRect = new System.Drawing.RectangleF(tileX + 10, tileY + 28, tileWidth - 20, 30);
            // Truncate text if too long to fit in tile width
            string displayText = lastInput;
            if (displayText.Length > 20) // Rough estimate, adjust based on font size
            {
                displayText = displayText.Substring(0, 17) + "...";
            }
            renderTarget.DrawText(displayText, _titleFormat, valueRect, primaryBrush);
            
            // Dispose temporary brushes
            primaryBrush?.Dispose();
            pressedBrush?.Dispose();
            idleBrush?.Dispose();
            textBrush?.Dispose();
            pressedTextBrush?.Dispose();
        }

        private void RenderGamingKey(ID2D1RenderTarget renderTarget, int x, int y, int w, int h, string label, bool active,
            ID2D1SolidColorBrush primaryBrush, ID2D1SolidColorBrush pressedBrush, ID2D1SolidColorBrush idleBrush,
            ID2D1SolidColorBrush textBrush, ID2D1SolidColorBrush pressedTextBrush)
        {
            var keyRect = new RoundedRectangle(new System.Drawing.RectangleF(x, y, w, h), KEY_ROUNDNESS, KEY_ROUNDNESS);

            // Minimal glow effect if active (very subtle, rounded)
            if(active)
            {
                for(int i = 2; i > 0; i--)
                {
                    float alpha = (2 - i) / 2f * 0.3f;
                    using (var glowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                        primaryBrush.Color.R,
                        primaryBrush.Color.G,
                        primaryBrush.Color.B,
                        alpha)))
                    {
                        var glowRect = new RoundedRectangle(
                            new System.Drawing.RectangleF(x - i, y - i, w + i * 2, h + i * 2),
                            KEY_ROUNDNESS + i, KEY_ROUNDNESS + i);
                        renderTarget.DrawRoundedRectangle(glowRect, glowBrush, 1f);
                    }
                }
            }

            renderTarget.FillRoundedRectangle(keyRect, active ? pressedBrush : idleBrush);

            // Thin border only (minimalist style, rounded)
            var borderBrush = primaryBrush;
            float borderWidth = active ? 1.5f : 1f; // Thin borders
            renderTarget.DrawRoundedRectangle(keyRect, borderBrush, borderWidth);

            // No inner border - cleaner look

            RenderText(renderTarget, label, x + w/2, y + h/2, _textFormat,
                active ? pressedTextBrush : textBrush);
        }

        private bool IsKeyPressed(InputStateSnapshot snapshot, string keyLabel)
        {
            if (snapshot == null || _context == null || _keyboardLayout == null) return false;

            byte vkCode = _keyboardLayout.KeyLabelToVkCode(keyLabel);
            
            // Special handling for SHIFT keys (can be Left Shift 0xA0 or Right Shift 0xA1)
            if (keyLabel == "SHIFT" && vkCode == 0x10)
            {
                // Check both Left Shift (0xA0) and Right Shift (0xA1)
                return _context.IsKeyPressed(snapshot, 0xA0) || _context.IsKeyPressed(snapshot, 0xA1);
            }
            
            return vkCode != 0 && _context.IsKeyPressed(snapshot, vkCode);
        }

        private string GetLastInput(InputStateSnapshot snapshot)
        {
            if (snapshot == null) return "---";

            long currentTime = Environment.TickCount;
            string currentInput = "---";
            
            // Build last input string from snapshot (like GDI)
            if (snapshot.SecondLastKey != 0 && snapshot.LastKey != 0)
            {
                string secondKey = VkCodeToName(snapshot.SecondLastKey);
                string lastKey = VkCodeToName(snapshot.LastKey);
                if (!string.IsNullOrEmpty(secondKey) && !string.IsNullOrEmpty(lastKey))
                {
                    currentInput = $"{secondKey} → {lastKey}";
                }
            }
            else if (snapshot.LastKey != 0)
            {
                string lastKey = VkCodeToName(snapshot.LastKey);
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
            
            return currentInput;
        }

        private string VkCodeToName(byte vkCode)
        {
            return vkCode switch
            {
                0x08 => "Backspace",
                0x09 => "Tab",
                0x0D => "Enter",
                0x10 => "Shift", // Generic Shift (fallback)
                0xA0 => "Left Shift", // Left Shift
                0xA1 => "Right Shift", // Right Shift
                0x11 => "Ctrl",
                0x12 => "Alt",
                0x1B => "Esc",
                0x20 => "Space",
                0x25 => "Left",
                0x26 => "Up",
                0x27 => "Right",
                0x28 => "Down",
                >= 0x30 and <= 0x39 => ((char)('0' + (vkCode - 0x30))).ToString(), // 0-9
                >= 0x41 and <= 0x5A => ((char)('A' + (vkCode - 0x41))).ToString(), // A-Z
                _ => $"VK{vkCode:X2}"
            };
        }

        private void RenderText(ID2D1RenderTarget renderTarget, string text, float centerX, float centerY,
                               IDWriteTextFormat textFormat, ID2D1Brush brush)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Simple text rendering at center position
            var textRect = new System.Drawing.RectangleF(centerX - 20, centerY - 10, 40, 20);
            renderTarget.DrawText(text, textFormat, textRect, brush);
        }

        // Interface compatibility methods
        public void SetStyle(OverlayStyle style)
        {
            _theme = StyleManager.GetTheme(style);
            _context.CurrentStyle = style;
            // TODO: Recreate brushes with new theme
        }

        public void SetTheme(OverlayTheme theme)
        {
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _context.Theme = _theme;
        }

        public void SetGameConfig(GameConfig config)
        {
            if (_currentGameConfig != config)
            {
                _currentGameConfig = config;
                _keyboardLayout = Direct2DKeyboardLayoutFactory.CreateLayout(_currentLayoutType, _currentGameConfig);
                System.Diagnostics.Debug.WriteLine($"Direct2DRenderer: Game config changed to {config}, Layout object: {_keyboardLayout != null}");
            }
        }

        public void SetLayoutType(KeyboardLayoutType layoutType)
        {
            if (_currentLayoutType != layoutType)
            {
                _currentLayoutType = layoutType;
                _keyboardLayout = Direct2DKeyboardLayoutFactory.CreateLayout(_currentLayoutType, _currentGameConfig);
                System.Diagnostics.Debug.WriteLine($"Direct2DRenderer: Layout type changed to {layoutType}, Layout object: {_keyboardLayout != null}");
            }
        }

        public void SetMousePosition(bool onRight)
        {
            if (_mouseOnRight != onRight || !_mouseVisible)
            {
                _mouseOnRight = onRight;
                _mouseVisible = true; // Show mouse when position is set
                System.Diagnostics.Debug.WriteLine($"Direct2DRenderer: Mouse position changed to {(onRight ? "Right" : "Left")}, visible={_mouseVisible}");
            }
        }

        public void SetMouseVisible(bool visible)
        {
            if (_mouseVisible != visible)
            {
                _mouseVisible = visible;
                System.Diagnostics.Debug.WriteLine($"Direct2DRenderer: Mouse visibility changed to {visible}");
            }
        }

        public void SetMouseStyle(string style)
        {
            // Handle "None" style to hide mouse
            if (style == "None")
            {
                SetMouseVisible(false);
            }
            else
            {
                SetMouseVisible(true);
                // Pass style to mouse renderer
                if (_mouseRenderer != null)
                {
                    _mouseRenderer.SetMouseStyle(style);
                }
            }
        }

        public void SetCustomLogo(string imagePath)
        {
            // TODO: Implement custom logo for Direct2D
        }

        public void SetAnimatedBackground(bool enabled)
        {
            // TODO: Implement animated background for Direct2D
        }

        public void SetTransparentBackground(bool transparent)
        {
            _context.UseTransparentBackground = transparent;
        }

        public bool IsTransparentBackgroundEnabled() => _context.UseTransparentBackground;

        public OverlayStyle CurrentStyle => _context.CurrentStyle;
        public KeyboardLayoutType CurrentLayoutType => _currentLayoutType;
        public GameConfig CurrentGameConfig => _currentGameConfig;
        public bool MouseOnRight => _mouseOnRight;
        public bool MouseVisible => _mouseVisible;

        public (int totalWidth, int totalHeight) CalculateRequiredSize()
        {
            // Calculate actual keyboard dimensions
            var (actualKeyboardWidth, actualKeyboardHeight) = CalculateKeyboardDimensions();
            
            // Calculate total width based on mouse position and visibility
            int totalWidth = GLOBAL_PADDING * 2; // Left and right padding
            
            if (!_mouseVisible)
            {
                // Mouse hidden: Only keyboard width
                totalWidth += actualKeyboardWidth;
            }
            else if (_mouseOnRight)
            {
                // Mouse on right: Keyboard + Spacing + Mouse
                totalWidth += actualKeyboardWidth + KEYBOARD_MOUSE_SPACING + MOUSE_WIDTH;
            }
            else
            {
                // Mouse on left: Mouse + Side Buttons + Spacing + Keyboard
                totalWidth += MOUSE_WIDTH + SIDE_BUTTON_OFFSET + KEYBOARD_MOUSE_SPACING + actualKeyboardWidth;
            }
            
            // Calculate total height: max of keyboard and mouse height + padding (no title space)
            int totalHeight = GLOBAL_PADDING * 2; // Top/bottom padding only
            if (_mouseVisible)
            {
                totalHeight += Math.Max(actualKeyboardHeight, MOUSE_HEIGHT); // Max of keyboard or mouse height
            }
            else
            {
                totalHeight += actualKeyboardHeight; // Only keyboard height
            }
            
            return (totalWidth, totalHeight);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Dispose mouse renderer
            _mouseRenderer?.Dispose();

            // Dispose brushes
            _neonGreenBrush?.Dispose();
            _whiteBrush?.Dispose();
            _blackBrush?.Dispose();
            _transparentBrush?.Dispose();

            // Dispose text formats
            _textFormat?.Dispose();
            _titleFormat?.Dispose();

            // Dispose render targets
            _deviceContext?.Dispose();
            _renderTarget?.Dispose();

            // Dispose factories
            _writeFactory?.Dispose();
            _d2dFactory?.Dispose();
        }
    }
}
