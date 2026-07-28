using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using GamingKeypressOverlay.Input;
using GamingKeypressOverlay.Overlay;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Particle class for mouse click effects
    /// </summary>
    internal class MouseParticle
    {
        public PointF Position;
        public PointF Velocity;
        public float Life; // 0.0 to 1.0
        public Color Color;
    }
    
    /// <summary>
    /// Mouse rendering module for GDI renderer
    /// Handles mouse body, buttons, wheel, side buttons, and particle effects
    /// </summary>
    internal class GDIRendererMouse
    {
        private readonly GDIRenderContext _context;
        private readonly Win32KeyboardLayout _keyboardLayout;
        private readonly bool _mouseOnRight;
        private GlassMorphMouseRenderer _glassMorphRenderer;
        private string _mouseStyle = "Gaming";
        
        // Layout constants
        private const int KEY_HEIGHT = 40;
        private const int KEYBOARD_X = 50;
        private const int KEYBOARD_Y = 100;
        private const int MOUSE_WIDTH = 280;
        private const int MOUSE_HEIGHT = 200;
        private const int KEYBOARD_MOUSE_SPACING = 60;
        
        // Particle system for mouse click effects
        private List<MouseParticle> _mouseParticles = new List<MouseParticle>();
        private bool[] _previousMouseButtonStates = new bool[5]; // Track previous button states for particle spawning
        
        public GDIRendererMouse(GDIRenderContext context, Win32KeyboardLayout keyboardLayout, bool mouseOnRight)
        {
            _context = context;
            _keyboardLayout = keyboardLayout;
            _mouseOnRight = mouseOnRight;
            _glassMorphRenderer = new GlassMorphMouseRenderer();
        }
        
        /// <summary>
        /// Set mouse rendering style
        /// </summary>
        public void SetMouseStyle(string style)
        {
            _mouseStyle = style switch
            {
                "None" => "None",
                "Minimal" or "Standard-2" => "Minimal",
                _ => "Gaming"
            };
        }
        
        /// <summary>
        /// Render mouse with all buttons and effects
        /// </summary>
        public unsafe void RenderMouse(Graphics g, InputStateSnapshot snapshot)
        {
            // Calculate keyboard position and dimensions from layout
            int keyboardX = KEYBOARD_X;
            int keyboardWidth = 0;
            int keyboardHeight = 0;
            
            if (_keyboardLayout != null && _keyboardLayout.Keys.Count > 0)
            {
                // Trouver la position X réelle du clavier (première touche)
                keyboardX = _keyboardLayout.Keys[0].X;
                
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
                // Adjust to relative width (from keyboardX réel)
                keyboardWidth = keyboardWidth - keyboardX;
            }
            else
            {
                // Fallback: estimate keyboard width
                keyboardWidth = 1200;
                keyboardHeight = 300;
            }
            
            // Calculate mouse position based on horizontal layout
            const int GLOBAL_PADDING = 50; // Global padding around application
            const int SIDE_BUTTON_OFFSET = 30; // Distance from mouse left edge to side buttons
            int mouseX;
            int mouseY;
            int mouseWidth = MOUSE_WIDTH;
            int mouseHeight = MOUSE_HEIGHT;
            
            // Calculate mouse Y position with global padding (same as keyboard)
            int mouseStartY = GLOBAL_PADDING + 60 + 10; // padding + tile height + spacing
            
            if (_mouseOnRight)
            {
                // Right side: mouseX = keyboardX + keyboardWidth + spacing
                mouseX = keyboardX + keyboardWidth + KEYBOARD_MOUSE_SPACING;
                mouseY = mouseStartY;
            }
            else
            {
                // Left side: positionner la souris à gauche du clavier
                // Start from global padding + space for side buttons
                mouseX = GLOBAL_PADDING + SIDE_BUTTON_OFFSET;
                mouseY = mouseStartY;
            }
            
            // Don't render mouse if style is "None"
            if (_mouseStyle == "None")
            {
                return;
            }
            
            // Otherwise use standard renderer (existing code continues below)
            
            // Standard mouse rendering (existing implementation)
            RenderStandardMouse(g, snapshot, mouseX, mouseY, mouseWidth, mouseHeight);
        }
        
        /// <summary>
        /// Render glassmorphism mouse style
        /// </summary>
        private unsafe void RenderGlassMorphMouse(Graphics g, InputStateSnapshot snapshot, int mouseX, int mouseY)
        {
            // Get mouse button states
            bool leftPressed = snapshot.MouseButtons[0];
            bool rightPressed = snapshot.MouseButtons[1];
            bool middlePressed = snapshot.MouseButtons[2];
            
            // Get side buttons
            var sideButtons = new Dictionary<string, bool>();
            if (snapshot.MouseButtons.Length > 3) sideButtons["X1"] = snapshot.MouseButtons[3];
            if (snapshot.MouseButtons.Length > 4) sideButtons["X2"] = snapshot.MouseButtons[4];
            if (snapshot.MouseButtons.Length > 5) sideButtons["X3"] = snapshot.MouseButtons[5];
            if (snapshot.MouseButtons.Length > 6) sideButtons["X4"] = snapshot.MouseButtons[6];
            
            // Get wheel delta
            int wheelDelta = snapshot.WheelDelta;
            
            // Render with glassmorphism renderer
            _glassMorphRenderer.RenderMouse(
                g,
                leftPressed, rightPressed, middlePressed,
                sideButtons, wheelDelta,
                _context.Theme,
                new PointF(mouseX, mouseY),
                _context
            );
        }
        
        /// <summary>
        /// Render standard mouse style
        /// </summary>
        private unsafe void RenderStandardMouse(Graphics g, InputStateSnapshot snapshot, int mouseX, int mouseY, int mouseWidth, int mouseHeight)
        {
            const int GLOBAL_PADDING = 50;
            const int SIDE_BUTTON_OFFSET = 30;
            
            // Subtle glow effect background (softer, more neon-like)
            Color primaryColor = _context.BrushToColor(_context.Theme.PrimaryColor);
            GraphicsPath glowPath = new GraphicsPath();
            glowPath.AddEllipse(mouseX - 20, mouseY + 10, mouseWidth + 40, mouseHeight + 30);
            using (PathGradientBrush glowBrush = new PathGradientBrush(glowPath))
            {
                // Softer glow - cyan/neon theme
                Color glowColor = Color.FromArgb(40, primaryColor);
                glowBrush.CenterColor = glowColor;
                glowBrush.SurroundColors = new Color[] { Color.FromArgb(0, primaryColor) };
                g.FillPath(glowBrush, glowPath);
            }
            glowPath.Dispose();
            
            // Main mouse body - More rounded, trackpad-like shape (futuristic)
            GraphicsPath mousePath = new GraphicsPath();
            int centerX = mouseX + mouseWidth / 2;
            int centerY = mouseY + mouseHeight / 2;
            
            // More rounded top curve (trapezoidal, wider at top)
            float topWidth = mouseWidth * 0.95f;
            float topOffset = (mouseWidth - topWidth) / 2;
            mousePath.AddBezier(
                new PointF(mouseX + 20, mouseY + 10),
                new PointF(mouseX + 40, mouseY + 2),
                new PointF(mouseX + mouseWidth - 40, mouseY + 2),
                new PointF(mouseX + mouseWidth - 20, mouseY + 10)
            );
            
            // Right side - more rounded, less bulge
            mousePath.AddBezier(
                new PointF(mouseX + mouseWidth - 20, mouseY + 10),
                new PointF(mouseX + mouseWidth - 12, mouseY + mouseHeight * 0.3f),
                new PointF(mouseX + mouseWidth - 12, mouseY + mouseHeight * 0.7f),
                new PointF(mouseX + mouseWidth - 25, mouseY + mouseHeight - 15)
            );
            
            // Bottom curve - wider, more rounded
            mousePath.AddBezier(
                new PointF(mouseX + mouseWidth - 25, mouseY + mouseHeight - 15),
                new PointF(mouseX + mouseWidth - 50, mouseY + mouseHeight - 5),
                new PointF(mouseX + 50, mouseY + mouseHeight - 5),
                new PointF(mouseX + 25, mouseY + mouseHeight - 15)
            );
            
            // Left side - more rounded
            mousePath.AddBezier(
                new PointF(mouseX + 25, mouseY + mouseHeight - 15),
                new PointF(mouseX + 12, mouseY + mouseHeight * 0.7f),
                new PointF(mouseX + 12, mouseY + mouseHeight * 0.3f),
                new PointF(mouseX + 20, mouseY + 10)
            );
            
            mousePath.CloseAllFigures();
            
            // Enhanced realistic 3D shadow with multiple layers
            _context.DrawRealisticShadow(g, mousePath, Color.Black, 8);
            
            // Enhanced mouse body with depth (inner shadow effect)
            Color mouseBgColor = _context.BrushToColor(_context.Theme.MouseBackground);
            Color mouseBgDark = Color.FromArgb(
                Math.Max(0, mouseBgColor.R - 50),
                Math.Max(0, mouseBgColor.G - 50),
                Math.Max(0, mouseBgColor.B - 50)
            );
            Color mouseBgLight = Color.FromArgb(
                Math.Min(255, mouseBgColor.R + 60),
                Math.Min(255, mouseBgColor.G + 60),
                Math.Min(255, mouseBgColor.B + 60)
            );
            
            // Main body gradient (top to bottom with more contrast)
            using (PathGradientBrush mouseBgBrush = new PathGradientBrush(mousePath))
            {
                mouseBgBrush.CenterPoint = new PointF(centerX, mouseY + mouseHeight * 0.25f);
                mouseBgBrush.CenterColor = mouseBgLight;
                mouseBgBrush.SurroundColors = new Color[] { mouseBgDark };
                g.FillPath(mouseBgBrush, mousePath);
            }
            
            // Inner shadow for depth (darken bottom edges)
            GraphicsPath innerShadowPath = (GraphicsPath)mousePath.Clone();
            using (Matrix innerMatrix = new Matrix())
            {
                innerMatrix.Scale(0.98f, 0.98f);
                innerMatrix.Translate(mouseWidth * 0.01f, mouseHeight * 0.02f);
                innerShadowPath.Transform(innerMatrix);
                
                using (SolidBrush innerShadowBrush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                {
                    g.FillPath(innerShadowBrush, innerShadowPath);
                }
            }
            innerShadowPath.Dispose();
            
            // Soft highlight effect.
            GraphicsPath highlightPath = new GraphicsPath();
            int highlightX = mouseX + 25;
            int highlightY = mouseY + 12;
            int highlightWidth = mouseWidth - 50;
            int highlightHeight = (int)(mouseHeight * 0.4f);
            
            highlightPath.AddEllipse(highlightX, highlightY, highlightWidth, highlightHeight);
            
            using (PathGradientBrush highlightBrush = new PathGradientBrush(highlightPath))
            {
                highlightBrush.CenterPoint = new PointF(centerX, mouseY + mouseHeight * 0.18f);
                highlightBrush.CenterColor = Color.FromArgb(80, 255, 255, 255);
                highlightBrush.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                g.FillPath(highlightBrush, highlightPath);
            }
            highlightPath.Dispose();
            
            Color mouseBorderColor = _context.BrushToColor(_context.Theme.MouseBorder);
            
            // Side buttons (thumb area) - always on left side of mouse
            // Add padding to ensure they don't go off-screen when mouse is on left
            // Note: SIDE_BUTTON_OFFSET and GLOBAL_PADDING are already defined above
            int sideBtnWidth = 20;
            int sideBtnHeight = 25;
            
            // Side buttons always on left side of mouse
            int sideBtnX = mouseX - SIDE_BUTTON_OFFSET;
            
            // Ensure side buttons don't go off-screen when mouse is on left
            if (!_mouseOnRight)
            {
                // Ensure minimum padding from left edge
                if (sideBtnX < GLOBAL_PADDING)
                {
                    sideBtnX = GLOBAL_PADDING;
                }
            }
            
            int sideBtnY1 = mouseY + 40;
            int sideBtnY2 = mouseY + 75;
            int sideBtnY3 = mouseY + 110;
            int sideBtnY4 = mouseY + 145;
            
            // Button 1 (top) - XButton1 (Back button, index 3)
            bool sideBtn1Pressed = snapshot != null && snapshot.MouseButtons != null && 
                                   snapshot.MouseButtons.Length > 3 && snapshot.MouseButtons[3];
            DrawSideButton(g, sideBtnX, sideBtnY1, sideBtnWidth, sideBtnHeight, sideBtn1Pressed, "X1", mouseBorderColor);
            
            // Button 2 - XButton2 (Forward button, index 4)
            bool sideBtn2Pressed = snapshot != null && snapshot.MouseButtons != null && 
                                   snapshot.MouseButtons.Length > 4 && snapshot.MouseButtons[4];
            DrawSideButton(g, sideBtnX, sideBtnY2, sideBtnWidth, sideBtnHeight, sideBtn2Pressed, "X2", mouseBorderColor);
            
            // Button 3 - XButton3
            DrawSideButton(g, sideBtnX, sideBtnY3, sideBtnWidth, sideBtnHeight, false, "X3", mouseBorderColor);
            
            // Button 4 (bottom) - XButton4
            DrawSideButton(g, sideBtnX, sideBtnY4, sideBtnWidth, sideBtnHeight, false, "X4", mouseBorderColor);
            
            // Outer glow border (softer, neon-like)
            Color mouseGlowColor = Color.FromArgb(30, primaryColor);
            using (Pen glowBorderPen = new Pen(mouseGlowColor, 4))
            {
                g.DrawPath(glowBorderPen, mousePath);
            }
            
            // Main border (thinner, cleaner)
            using (Pen mouseBorderPen = new Pen(mouseBorderColor, 2))
            {
                g.DrawPath(mouseBorderPen, mousePath);
            }
            
            // Inner highlight border (subtle)
            using (Pen innerBorderPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
            {
                GraphicsPath innerPath = (GraphicsPath)mousePath.Clone();
                using (Matrix innerMatrix = new Matrix())
                {
                    innerMatrix.Translate(1, 1);
                    innerMatrix.Scale(0.99f, 0.99f, MatrixOrder.Append);
                    innerPath.Transform(innerMatrix);
                    g.DrawPath(innerBorderPen, innerPath);
                }
                innerPath.Dispose();
            }
            
            // Separation line between L and R buttons (light gradient, not solid line)
            int separationX = mouseX + mouseWidth / 2;
            int separationY = mouseY + 20;
            int separationHeight = 50;
            using (LinearGradientBrush separationBrush = new LinearGradientBrush(
                new PointF(separationX, separationY),
                new PointF(separationX, separationY + separationHeight),
                Color.FromArgb(40, primaryColor),
                Color.Transparent))
            {
                using (Pen separationPen = new Pen(separationBrush, 1.5f))
                {
                    g.DrawLine(separationPen, separationX, separationY, separationX, separationY + separationHeight);
                }
            }
            
            // Button radius (shared for both buttons)
            int btnRadius = 8;
            
            // Left button with rounded rectangle shape
            GraphicsPath leftButtonPath = new GraphicsPath();
            int btnX = mouseX + 25;
            int btnY = mouseY + 20;
            int btnWidth = (mouseWidth / 2) - 35;
            int btnHeight = 50;
            
            // Rounded rectangle pour bouton gauche
            leftButtonPath.AddArc(btnX, btnY, btnRadius * 2, btnRadius * 2, 180, 90);
            leftButtonPath.AddArc(btnX + btnWidth - btnRadius * 2, btnY, btnRadius * 2, btnRadius * 2, 270, 90);
            leftButtonPath.AddArc(btnX + btnWidth - btnRadius * 2, btnY + btnHeight - btnRadius * 2, btnRadius * 2, btnRadius * 2, 0, 90);
            leftButtonPath.AddArc(btnX, btnY + btnHeight - btnRadius * 2, btnRadius * 2, btnRadius * 2, 90, 90);
            leftButtonPath.CloseAllFigures();
            
            bool leftPressed = snapshot.MouseButtons[0];
            Color leftButtonColor = leftPressed 
                ? _context.BrushToColor(_context.Theme.MouseButtonPressed) 
                : _context.BrushToColor(_context.Theme.MouseButtonIdle);
            
            // Button shadow
            if (!leftPressed)
            {
                using (GraphicsPath shadowBtnPath = (GraphicsPath)leftButtonPath.Clone())
                using (Matrix shadowBtnMatrix = new Matrix())
                {
                    shadowBtnMatrix.Translate(2, 2);
                    shadowBtnPath.Transform(shadowBtnMatrix);
                    using (SolidBrush shadowBtn = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                    {
                        g.FillPath(shadowBtn, shadowBtnPath);
                    }
                }
            }
            
            // Enhanced glow effect when pressed (animated pulse)
            if (leftPressed)
            {
                float pulseIntensity = (float)(Math.Sin(_context.AnimationTime * 0.003 * 3.0) * 0.5 + 0.5);
                Color pressedColor = _context.BrushToColor(_context.Theme.MouseButtonPressed);
                
                // Multi-layer glow for blur effect
                for (int i = 5; i > 0; i--)
                {
                    int alpha = (int)(50 * pulseIntensity / i);
                    using (Pen glowPen = new Pen(Color.FromArgb(alpha, pressedColor), i * 2))
                    {
                        g.DrawPath(glowPen, leftButtonPath);
                    }
                }
            }
            
            using (SolidBrush leftBrush = new SolidBrush(leftButtonColor))
            {
                g.FillPath(leftBrush, leftButtonPath);
                
                // Highlight on button
                if (!leftPressed)
                {
                    GraphicsPath btnHighlightPath = new GraphicsPath();
                    btnHighlightPath.AddBezier(
                        new PointF(btnX, btnY + 8),
                        new PointF(btnX + 15, btnY),
                        new PointF(btnX + btnWidth - 15, btnY),
                        new PointF(btnX + btnWidth, btnY + 8)
                    );
                    btnHighlightPath.AddLine(new PointF(btnX + btnWidth, btnY + 8),
                                            new PointF(btnX + btnWidth, btnY + btnHeight / 2));
                    btnHighlightPath.AddLine(new PointF(btnX + btnWidth, btnY + btnHeight / 2),
                                            new PointF(btnX, btnY + btnHeight / 2));
                    btnHighlightPath.AddLine(new PointF(btnX, btnY + btnHeight / 2),
                                            new PointF(btnX, btnY + 8));
                    btnHighlightPath.CloseAllFigures();
                    
                    using (LinearGradientBrush btnHighlightBrush = new LinearGradientBrush(
                        new PointF(btnX, btnY),
                        new PointF(btnX, btnY + btnHeight / 2),
                        Color.FromArgb(100, 255, 255, 255),
                        Color.FromArgb(30, 255, 255, 255)))
                    {
                        g.FillPath(btnHighlightBrush, btnHighlightPath);
                    }
                    btnHighlightPath.Dispose();
                }
                
                // Border
                Color btnBorder = leftPressed ? _context.BrushToColor(_context.Theme.MouseButtonPressed) : mouseBorderColor;
                using (Pen btnPen = new Pen(btnBorder, 2))
                {
                    g.DrawPath(btnPen, leftButtonPath);
                }
                
                // Label "L"
                Color btnTextColor = leftPressed 
                    ? _context.BrushToColor(_context.Theme.KeyPressedForeground)
                    : _context.BrushToColor(_context.Theme.KeyIdleForeground);
                using (SolidBrush btnTextBrush = new SolidBrush(btnTextColor))
                using (Font btnFont = new Font("Consolas", 18, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString("L", btnFont);
                    float textX = btnX + (btnWidth - textSize.Width) / 2;
                    float textY = btnY + (btnHeight - textSize.Height) / 2;
                    g.DrawString("L", btnFont, btnTextBrush, textX, textY);
                }
            }
            
            // Particles disabled (removed for cleaner design)
            
            // Right button with rounded rectangle shape
            GraphicsPath rightButtonPath = new GraphicsPath();
            int rightBtnX = mouseX + mouseWidth / 2 + 10;
            int rightBtnY = mouseY + 20;
            int rightBtnWidth = (mouseWidth / 2) - 35;
            int rightBtnHeight = 50;
            
            // Rounded rectangle pour bouton droit
            rightButtonPath.AddArc(rightBtnX, rightBtnY, btnRadius * 2, btnRadius * 2, 180, 90);
            rightButtonPath.AddArc(rightBtnX + rightBtnWidth - btnRadius * 2, rightBtnY, btnRadius * 2, btnRadius * 2, 270, 90);
            rightButtonPath.AddArc(rightBtnX + rightBtnWidth - btnRadius * 2, rightBtnY + rightBtnHeight - btnRadius * 2, btnRadius * 2, btnRadius * 2, 0, 90);
            rightButtonPath.AddArc(rightBtnX, rightBtnY + rightBtnHeight - btnRadius * 2, btnRadius * 2, btnRadius * 2, 90, 90);
            rightButtonPath.CloseAllFigures();
            
            bool rightPressed = snapshot.MouseButtons[1];
            Color rightButtonColor = rightPressed 
                ? _context.BrushToColor(_context.Theme.MouseButtonPressed) 
                : _context.BrushToColor(_context.Theme.MouseButtonIdle);
            
            // Button shadow
            if (!rightPressed)
            {
                using (GraphicsPath shadowBtnPath = (GraphicsPath)rightButtonPath.Clone())
                using (Matrix shadowBtnMatrix = new Matrix())
                {
                    shadowBtnMatrix.Translate(2, 2);
                    shadowBtnPath.Transform(shadowBtnMatrix);
                    using (SolidBrush shadowBtn = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                    {
                        g.FillPath(shadowBtn, shadowBtnPath);
                    }
                }
            }
            
            // Enhanced glow effect when pressed (animated pulse)
            if (rightPressed)
            {
                float pulseIntensity = (float)(Math.Sin(_context.AnimationTime * 0.003 * 3.0) * 0.5 + 0.5);
                Color pressedColor = _context.BrushToColor(_context.Theme.MouseButtonPressed);
                
                // Multi-layer glow for blur effect
                for (int i = 5; i > 0; i--)
                {
                    int alpha = (int)(50 * pulseIntensity / i);
                    using (Pen glowPen = new Pen(Color.FromArgb(alpha, pressedColor), i * 2))
                    {
                        g.DrawPath(glowPen, rightButtonPath);
                    }
                }
            }
            
            using (SolidBrush rightBrush = new SolidBrush(rightButtonColor))
            {
                g.FillPath(rightBrush, rightButtonPath);
                
                // Highlight on button
                if (!rightPressed)
                {
                    GraphicsPath btnHighlightPath = new GraphicsPath();
                    btnHighlightPath.AddBezier(
                        new PointF(rightBtnX, rightBtnY + 8),
                        new PointF(rightBtnX + 15, rightBtnY),
                        new PointF(rightBtnX + rightBtnWidth - 15, rightBtnY),
                        new PointF(rightBtnX + rightBtnWidth, rightBtnY + 8)
                    );
                    btnHighlightPath.AddLine(new PointF(rightBtnX + rightBtnWidth, rightBtnY + 8),
                                            new PointF(rightBtnX + rightBtnWidth, rightBtnY + rightBtnHeight / 2));
                    btnHighlightPath.AddLine(new PointF(rightBtnX + rightBtnWidth, rightBtnY + rightBtnHeight / 2),
                                            new PointF(rightBtnX, rightBtnY + rightBtnHeight / 2));
                    btnHighlightPath.AddLine(new PointF(rightBtnX, rightBtnY + rightBtnHeight / 2),
                                            new PointF(rightBtnX, rightBtnY + 8));
                    btnHighlightPath.CloseAllFigures();
                    
                    using (LinearGradientBrush btnHighlightBrush = new LinearGradientBrush(
                        new PointF(rightBtnX, rightBtnY),
                        new PointF(rightBtnX, rightBtnY + rightBtnHeight / 2),
                        Color.FromArgb(100, 255, 255, 255),
                        Color.FromArgb(30, 255, 255, 255)))
                    {
                        g.FillPath(btnHighlightBrush, btnHighlightPath);
                    }
                    btnHighlightPath.Dispose();
                }
                
                // Border
                Color btnBorder = rightPressed ? _context.BrushToColor(_context.Theme.MouseButtonPressed) : mouseBorderColor;
                using (Pen btnPen = new Pen(btnBorder, 2))
                {
                    g.DrawPath(btnPen, rightButtonPath);
                }
                
                // Label "R"
                Color btnTextColor = rightPressed 
                    ? _context.BrushToColor(_context.Theme.KeyPressedForeground)
                    : _context.BrushToColor(_context.Theme.KeyIdleForeground);
                using (SolidBrush btnTextBrush = new SolidBrush(btnTextColor))
                using (Font btnFont = new Font("Consolas", 18, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString("R", btnFont);
                    float textX = rightBtnX + (rightBtnWidth - textSize.Width) / 2;
                    float textY = rightBtnY + (rightBtnHeight - textSize.Height) / 2;
                    g.DrawString("R", btnFont, btnTextBrush, textX, textY);
                }
            }
            
            // Particles disabled (removed for cleaner design)
            
            // Middle button (wheel) - Smaller, more refined with radial gradient
            int wheelX = mouseX + mouseWidth / 2 - 20; // Smaller: was 25, now 20
            int wheelY = mouseY + 75;
            int wheelWidth = 40; // Smaller: was 50, now 40
            int wheelHeight = 32; // Smaller: was 40, now 32
            GraphicsPath wheelPath = new GraphicsPath();
            int wheelRadius = 10; // Smaller: was 12, now 10
            wheelPath.AddArc(wheelX, wheelY, wheelRadius * 2, wheelRadius * 2, 180, 90);
            wheelPath.AddArc(wheelX + wheelWidth - wheelRadius * 2, wheelY, wheelRadius * 2, wheelRadius * 2, 270, 90);
            wheelPath.AddArc(wheelX + wheelWidth - wheelRadius * 2, wheelY + wheelHeight - wheelRadius * 2, wheelRadius * 2, wheelRadius * 2, 0, 90);
            wheelPath.AddArc(wheelX, wheelY + wheelHeight - wheelRadius * 2, wheelRadius * 2, wheelRadius * 2, 90, 90);
            wheelPath.CloseAllFigures();
            
            // Check both middle button click AND wheel scroll
            bool middlePressed = snapshot.MouseButtons[2];
            bool wheelScrolled = snapshot.WheelDelta != 0;
            bool wheelActive = middlePressed || wheelScrolled;
            
            // Button shadow
            if (!wheelActive)
            {
                using (GraphicsPath shadowBtnPath = (GraphicsPath)wheelPath.Clone())
                using (Matrix shadowBtnMatrix = new Matrix())
                {
                    shadowBtnMatrix.Translate(2, 2);
                    shadowBtnPath.Transform(shadowBtnMatrix);
                    using (SolidBrush shadowBtn = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                    {
                        g.FillPath(shadowBtn, shadowBtnPath);
                    }
                }
            }
            
            // Simple wheel color (no RGB animation)
            Color wheelColor;
            
            if (wheelActive)
            {
                wheelColor = _context.BrushToColor(_context.Theme.MouseButtonPressed);
                // Use radial gradient for pressed state
                using (PathGradientBrush middleBrush = new PathGradientBrush(wheelPath))
                {
                    middleBrush.CenterPoint = new PointF(wheelX + wheelWidth / 2, wheelY + wheelHeight / 2);
                    middleBrush.CenterColor = Color.FromArgb(255, wheelColor);
                    middleBrush.SurroundColors = new Color[] { Color.FromArgb(180, wheelColor) };
                    g.FillPath(middleBrush, wheelPath);
                }
            }
            else
            {
                // Simple idle color (no animation)
                wheelColor = _context.BrushToColor(_context.Theme.MouseButtonIdle);
                using (PathGradientBrush radialBrush = new PathGradientBrush(wheelPath))
                {
                    radialBrush.CenterPoint = new PointF(wheelX + wheelWidth / 2, wheelY + wheelHeight / 2);
                    Color wheelLight = Color.FromArgb(
                        Math.Min(255, wheelColor.R + 30),
                        Math.Min(255, wheelColor.G + 30),
                        Math.Min(255, wheelColor.B + 30)
                    );
                    radialBrush.CenterColor = wheelLight;
                    radialBrush.SurroundColors = new Color[] { wheelColor };
                    g.FillPath(radialBrush, wheelPath);
                }
            }
            
            // Draw wheel content (texture lines or label)
            {
                // Scroll wheel texture lines (subtle)
                if (!wheelActive)
                {
                    using (Pen texturePen = new Pen(Color.FromArgb(100, wheelColor), 1.5f))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int lineY = wheelY + 12 + i * 8;
                            g.DrawLine(texturePen, wheelX + 8, lineY, wheelX + wheelWidth - 8, lineY);
                        }
                    }
                }
                else if (wheelScrolled)
                {
                    // Animated scroll effect
                    int scrollOffset = (snapshot.WheelDelta > 0) ? -2 : 2;
                    using (Pen activePen = new Pen(Color.FromArgb(255, wheelColor), 2.5f))
                    using (Pen inactivePen = new Pen(Color.FromArgb(80, wheelColor), 1.5f))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            int lineY = wheelY + 12 + i * 8 + scrollOffset;
                            Pen penToUse = (i == 1 || i == 2) ? activePen : inactivePen;
                            g.DrawLine(penToUse, wheelX + 8, lineY, wheelX + wheelWidth - 8, lineY);
                        }
                    }
                }
                
                // Border with glow when active
                Color btnBorder = wheelActive ? wheelColor : Color.FromArgb(150, wheelColor);
                using (Pen btnPen = new Pen(btnBorder, wheelActive ? 3 : 2))
                {
                    g.DrawPath(btnPen, wheelPath);
                }
                
                // Simple glow effect when scrolling (no RGB)
                if (wheelScrolled)
                {
                    using (Pen wheelGlowPen = new Pen(Color.FromArgb(120, wheelColor), 4))
                    {
                        GraphicsPath wheelGlowPath = (GraphicsPath)wheelPath.Clone();
                        using (Matrix wheelGlowMatrix = new Matrix())
                        {
                            wheelGlowMatrix.Translate(-2, -2);
                            wheelGlowPath.Transform(wheelGlowMatrix);
                            g.DrawPath(wheelGlowPen, wheelGlowPath);
                        }
                        wheelGlowPath.Dispose();
                    }
                }
                
                // Label "M" or scroll indicator (smaller font for smaller wheel)
                Color btnTextColor = wheelActive 
                    ? _context.BrushToColor(_context.Theme.KeyPressedForeground)
                    : Color.White;
                using (SolidBrush btnTextBrush = new SolidBrush(btnTextColor))
                using (Font btnFont = new Font("Consolas", 10, FontStyle.Bold)) // Smaller: was 12, now 10
                {
                    string label = wheelScrolled ? (snapshot.WheelDelta > 0 ? "↑" : "↓") : "M";
                    SizeF textSize = g.MeasureString(label, btnFont);
                    float textX = wheelX + (wheelWidth - textSize.Width) / 2;
                    float textY = wheelY + (wheelHeight - textSize.Height) / 2;
                    g.DrawString(label, btnFont, btnTextBrush, textX, textY);
                }
            }
            
            // Cleanup paths
            mousePath?.Dispose();
            leftButtonPath?.Dispose();
            rightButtonPath?.Dispose();
            wheelPath?.Dispose();
        }
        
        /// <summary>
        /// Draw side button (X1, X2, X3, X4)
        /// </summary>
        private void DrawSideButton(Graphics g, int x, int y, int width, int height, bool pressed, string label, Color borderColor)
        {
            GraphicsPath btnPath = new GraphicsPath();
            int radius = 3;
            btnPath.AddArc(x, y, radius * 2, radius * 2, 180, 90);
            btnPath.AddLine(x + radius, y, x + width - radius, y);
            btnPath.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
            btnPath.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
            btnPath.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
            btnPath.CloseAllFigures();
            
            Color btnColor = pressed 
                ? Color.FromArgb(255, 168, 85, 247) 
                : Color.FromArgb(255, 42, 42, 62);
            Color btnBorder = pressed 
                ? Color.FromArgb(255, 200, 120, 255) 
                : Color.FromArgb(255, 74, 74, 110);
            
            using (SolidBrush btnBrush = new SolidBrush(btnColor))
            {
                g.FillPath(btnBrush, btnPath);
                
                if (pressed)
                {
                    // Glow effect
                    using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(80, btnColor)))
                    {
                        GraphicsPath glowPath = (GraphicsPath)btnPath.Clone();
                        using (Matrix glowMatrix = new Matrix())
                        {
                            glowMatrix.Translate(-1, -1);
                            glowPath.Transform(glowMatrix);
                            g.FillPath(glowBrush, glowPath);
                        }
                        glowPath.Dispose();
                    }
                }
            }
            
            using (Pen btnPen = new Pen(btnBorder, 1.5f))
            {
                g.DrawPath(btnPen, btnPath);
            }
            
            // Only draw label if provided
            if (!string.IsNullOrEmpty(label))
            {
                using (Font btnFont = new Font("Consolas", 8, FontStyle.Bold))
                using (SolidBrush textBrush = new SolidBrush(pressed ? Color.White : Color.FromArgb(255, 138, 138, 138)))
                {
                    SizeF textSize = g.MeasureString(label, btnFont);
                    float textX = x + (width - textSize.Width) / 2;
                    float textY = y + (height - textSize.Height) / 2;
                    g.DrawString(label, btnFont, textBrush, textX, textY);
                }
            }
            
            btnPath.Dispose();
        }
        
        /// <summary>
        /// Update and render mouse particles
        /// </summary>
        private void UpdateAndRenderParticles(Graphics g, InputStateSnapshot snapshot, PointF buttonCenter, Color particleColor)
        {
            // Update particles
            for (int i = _mouseParticles.Count - 1; i >= 0; i--)
            {
                var p = _mouseParticles[i];
                p.Life -= 0.03f; // Decay
                p.Position.X += p.Velocity.X;
                p.Position.Y += p.Velocity.Y;
                p.Velocity.Y += 0.2f; // Gravity
                
                if (p.Life <= 0)
                {
                    _mouseParticles.RemoveAt(i);
                    continue;
                }
                
                // Draw particle
                int alpha = (int)(255 * p.Life);
                using (var brush = new SolidBrush(Color.FromArgb(alpha, p.Color)))
                {
                    g.FillEllipse(brush, p.Position.X - 2, p.Position.Y - 2, 4, 4);
                }
            }
            
            // Spawn new particles when button is pressed
            if (snapshot != null && snapshot.MouseButtons != null)
            {
                for (int buttonIndex = 0; buttonIndex < Math.Min(5, snapshot.MouseButtons.Length); buttonIndex++)
                {
                    bool isPressed = snapshot.MouseButtons[buttonIndex];
                    bool wasPressed = _previousMouseButtonStates[buttonIndex];
                    
                    // Spawn particles on button press (transition from not pressed to pressed)
                    if (isPressed && !wasPressed)
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            float angle = (float)((i / 8.0) * Math.PI * 2);
                            _mouseParticles.Add(new MouseParticle
                            {
                                Position = new PointF(buttonCenter.X, buttonCenter.Y),
                                Velocity = new PointF(
                                    (float)Math.Cos(angle) * 3f,
                                    (float)Math.Sin(angle) * 3f
                                ),
                                Life = 1.0f,
                                Color = particleColor
                            });
                        }
                    }
                    
                    _previousMouseButtonStates[buttonIndex] = isPressed;
                }
            }
        }
    }
}
