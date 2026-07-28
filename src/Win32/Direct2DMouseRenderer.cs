using System;
using System.Collections.Generic;
using System.Numerics;
using System.Drawing;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using GamingKeypressOverlay.Input;
using GamingKeypressOverlay.Overlay;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Direct2D mouse renderer - Advanced gaming mouse with XAML-style visuals
    /// Features: glassmorphism, RGB strip, neon glow, smooth animations
    /// Designed for 240 FPS with GPU acceleration
    /// </summary>
    public class Direct2DMouseRenderer : IDisposable
    {
        private readonly ID2D1Factory _d2dFactory;
        private readonly IDWriteFactory _writeFactory;
        
        private bool _disposed = false;
        
        // Geometry cache (reused, created once)
        private ID2D1PathGeometry _mouseBodyGeometry;
        private ID2D1PathGeometry _leftButtonGeometry;
        private ID2D1PathGeometry _rightButtonGeometry;
        private ID2D1PathGeometry _scrollWheelGeometry;
        
        // Brushes (reused, updated per frame)
        private ID2D1SolidColorBrush _neonGreenBrush;
        private ID2D1SolidColorBrush _whiteBrush;
        private ID2D1SolidColorBrush _glassBrush;
        private ID2D1SolidColorBrush _glowBrush;
        private ID2D1LinearGradientBrush _bodyGradientBrush;
        private ID2D1LinearGradientBrush _rgbStripBrush;
        
        // Text format
        private IDWriteTextFormat _buttonTextFormat;
        
        // Mouse dimensions (fixed, gaming mouse size) - more elongated and narrower
        private const float MOUSE_WIDTH = 240f;  // Further reduced (narrower)
        private const float MOUSE_HEIGHT = 280f; // Further increased (longer)

        // RGB glow effects
        
        // Scroll wheel animation state (persist scroll animation)
        private int _lastWheelDelta = 0;
        private long _lastWheelTime = 0;
        private const long WHEEL_ANIMATION_DURATION_MS = 300; // Animation duration in milliseconds
        
        // Mouse style
        private string _mouseStyle = "Gaming";
        
        public Direct2DMouseRenderer(ID2D1Factory d2dFactory, IDWriteFactory writeFactory, ID2D1DeviceContext deviceContext)
        {
            _d2dFactory = d2dFactory;
            _writeFactory = writeFactory;
            
            // Create geometries (once, reused)
            CreateGeometries();
            
            // Create text format
            _buttonTextFormat = _writeFactory.CreateTextFormat(
                "Segoe UI",
                null,
                FontWeight.Bold,
                Vortice.DirectWrite.FontStyle.Normal,
                FontStretch.Normal,
                14.0f);
            _buttonTextFormat.TextAlignment = TextAlignment.Center;
            _buttonTextFormat.ParagraphAlignment = ParagraphAlignment.Center;
        }
        
        private void CreateGeometries()
        {
            // Mouse body - ergonomic gaming shape (more elongated and narrower)
            _mouseBodyGeometry = _d2dFactory.CreatePathGeometry();
            using (var sink = _mouseBodyGeometry.Open())
            {
                // Top curve (narrower at front) - adjusted for narrower width
                sink.BeginFigure(new Vector2(50, 20), FigureBegin.Filled);
                
                // Left side - ergonomic curve (adjusted for longer height)
                sink.AddBezier(new BezierSegment(
                    new Vector2(15, 40),
                    new Vector2(5, 140),  // Extended down for longer mouse
                    new Vector2(25, 260))); // Extended from 220 to 260
                
                // Bottom curve (adjusted for narrower overall)
                sink.AddBezier(new BezierSegment(
                    new Vector2(40, 270),  // Extended from 230 to 270
                    new Vector2(200, 270), // Adjusted from 215 to 200 (narrower)
                    new Vector2(215, 260))); // Adjusted from 235 to 215 (narrower)
                
                // Right side - ergonomic curve (adjusted for narrower width)
                sink.AddBezier(new BezierSegment(
                    new Vector2(230, 140), // Adjusted from 252 to 230 (narrower), extended down
                    new Vector2(220, 40),  // Adjusted from 242 to 220 (narrower)
                    new Vector2(190, 20))); // Adjusted from 205 to 190 (narrower)
                
                // Top closing curve (adjusted for narrower width)
                sink.AddBezier(new BezierSegment(
                    new Vector2(150, 10),  // Adjusted from 165 to 150 (narrower)
                    new Vector2(90, 10),   // Adjusted from 95 to 90 (narrower)
                    new Vector2(50, 20))); // Adjusted from 55 to 50 (narrower)
                
                sink.EndFigure(FigureEnd.Closed);
                sink.Close();
            }
            
            // Left button (wider, same height)
            _leftButtonGeometry = _d2dFactory.CreatePathGeometry();
            using (var sink = _leftButtonGeometry.Open())
            {
                sink.BeginFigure(new Vector2(60, 35), FigureBegin.Filled); // Moved left to make wider
                sink.AddBezier(new BezierSegment(
                    new Vector2(55, 38),
                    new Vector2(52, 60),   // Same height
                    new Vector2(55, 85))); // Same height (~50px)
                sink.AddLine(new Vector2(55, 85));
                sink.AddLine(new Vector2(110, 85)); // Wider (was 100, now ~55px width)
                sink.AddLine(new Vector2(110, 35));
                sink.EndFigure(FigureEnd.Closed);
                sink.Close();
            }
            
            // Right button (wider, same height)
            _rightButtonGeometry = _d2dFactory.CreatePathGeometry();
            using (var sink = _rightButtonGeometry.Open())
            {
                sink.BeginFigure(new Vector2(130, 35), FigureBegin.Filled); // Moved left to make wider
                sink.AddLine(new Vector2(185, 35)); // Wider (was 175, now ~55px width)
                sink.AddBezier(new BezierSegment(
                    new Vector2(188, 38),  // Adjusted
                    new Vector2(191, 60),  // Same height
                    new Vector2(188, 85))); // Same height (~50px)
                sink.AddLine(new Vector2(130, 85));
                sink.EndFigure(FigureEnd.Closed);
                sink.Close();
            }
            
            // Scroll wheel (reduced by half, centered between the two buttons)
            // Button L ends at x=110, Button R starts at x=130, center = 120
            _scrollWheelGeometry = _d2dFactory.CreatePathGeometry();
            using (var sink = _scrollWheelGeometry.Open())
            {
                // Centered at x=120, width 7px, so from 116.5 to 123.5
                sink.BeginFigure(new Vector2(116.5f, 50), FigureBegin.Filled);
                sink.AddArc(new ArcSegment(new Vector2(123.5f, 50), new SizeF(3.5f, 3.5f), 0, SweepDirection.Clockwise, ArcSize.Small)); // Half size (was 7)
                sink.AddLine(new Vector2(123.5f, 70)); // Reduced height (was 125, now ~20px height)
                sink.AddArc(new ArcSegment(new Vector2(116.5f, 70), new SizeF(3.5f, 3.5f), 0, SweepDirection.Clockwise, ArcSize.Small)); // Half size
                sink.EndFigure(FigureEnd.Closed);
                sink.Close();
            }
        }
        
        public void SetMouseStyle(string style)
        {
            _mouseStyle = style switch
            {
                "None" => "None",
                "Minimal" or "Standard-2" => "Minimal",
                _ => "Gaming"
            };
        }
        
        public void Render(
            ID2D1RenderTarget renderTarget,
            InputStateSnapshot snapshot,
            OverlayTheme theme,
            GDIRenderContext context,
            float animationTime)
        {
            if (_disposed || renderTarget == null || snapshot == null)
                return;
            
            // Get mouse button states
            bool leftPressed = snapshot.MouseButtons != null && snapshot.MouseButtons.Length > 0 && snapshot.MouseButtons[0];
            bool rightPressed = snapshot.MouseButtons != null && snapshot.MouseButtons.Length > 1 && snapshot.MouseButtons[1];
            bool middlePressed = snapshot.MouseButtons != null && snapshot.MouseButtons.Length > 2 && snapshot.MouseButtons[2];
            int wheelDelta = snapshot.WheelDelta;
            
            // Get theme colors
            System.Drawing.Color primaryColorGdi = context.BrushToColor(theme.PrimaryColor);
            System.Drawing.Color pressedColorGdi = context.BrushToColor(theme.MouseButtonPressed);
            System.Drawing.Color idleColorGdi = context.BrushToColor(theme.MouseButtonIdle);
            
            // Render based on style
            if (_mouseStyle == "None")
            {
                return;
            }
            if (_mouseStyle == "Minimal")
            {
                RenderStandard2(renderTarget, snapshot, theme, context, animationTime, leftPressed, rightPressed, middlePressed, wheelDelta, primaryColorGdi, pressedColorGdi, idleColorGdi);
            }
            else
            {
                // Gaming style: solid ergonomic shell with distinct control panels.
                // Create/update brushes if needed
                EnsureBrushes(renderTarget, primaryColorGdi, pressedColorGdi, idleColorGdi, animationTime);
                
                // Parent (Direct2DRenderer) handles positioning via transform
                // We just render the mouse at the current transform position
                // No need to save/restore transform - parent manages it
                
                // 1. RGB Glow halo (animated, behind mouse) - TEMPORARILY DISABLED
                // DrawRGBGlow(renderTarget, new Vector2(MOUSE_WIDTH / 2, MOUSE_HEIGHT / 2), animationTime);
                
                // 2. Mouse body (glassmorphism)
                DrawMouseBody(renderTarget, primaryColorGdi);

                // Filled panels make the silhouette feel like a modern gaming
                // mouse without using a flashing circular backdrop.
                DrawGamingShellDetails(renderTarget, primaryColorGdi);
                
                // 3. Left button (with glow when pressed) - adjusted for wider button
                DrawButton(renderTarget, _leftButtonGeometry, leftPressed, "L", 
                    new Vector2(82.5f, 60f), pressedColorGdi, idleColorGdi, animationTime); // Centered in wider button
                
                // 4. Right button (with glow when pressed) - adjusted for wider button
                DrawButton(renderTarget, _rightButtonGeometry, rightPressed, "R", 
                    new Vector2(157.5f, 60f), pressedColorGdi, idleColorGdi, animationTime); // Centered in wider button
                
                // 5. Scroll wheel
                DrawScrollWheel(renderTarget, middlePressed, wheelDelta, primaryColorGdi, animationTime);
                
                // 6. RGB strip at bottom (signature gaming)
                DrawRGBStrip(renderTarget, animationTime, primaryColorGdi);
                
                // 7. Side buttons (X1, X2, X3, X4)
                DrawSideButtons(renderTarget, snapshot, primaryColorGdi, pressedColorGdi);
            }
        }
        
        private void EnsureBrushes(ID2D1RenderTarget renderTarget, System.Drawing.Color primary, System.Drawing.Color pressed, System.Drawing.Color idle, float time)
        {
            // Primary color brush (from theme)
            if (_neonGreenBrush == null)
            {
                _neonGreenBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    primary.R / 255f, primary.G / 255f, primary.B / 255f, primary.A / 255f));
            }
            else
            {
                // Update color to match theme
                _neonGreenBrush.Color = new Color4(
                    primary.R / 255f, primary.G / 255f, primary.B / 255f, primary.A / 255f);
            }
            
            // White brush
            if (_whiteBrush == null)
            {
                _whiteBrush = renderTarget.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
            }
            
            // Glass brush (semi-transparent, using theme color)
            if (_glassBrush == null)
            {
                _glassBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    primary.R / 255f * 0.3f, primary.G / 255f * 0.3f, primary.B / 255f * 0.3f, 0.6f));
            }
            else
            {
                _glassBrush.Color = new Color4(
                    primary.R / 255f * 0.3f, primary.G / 255f * 0.3f, primary.B / 255f * 0.3f, 0.6f);
            }
            
            // Glow brush (animated RGB based on theme)
            if (_glowBrush == null)
            {
                _glowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    primary.R / 255f, primary.G / 255f, primary.B / 255f, 0.3f));
            }
            
            // Update glow color based on animation (hue shift from theme color)
            float hue = (time * 50f) % 360f;
            Color4 rgbColor = HsvToRgb(hue, 1f, 1f);
            // Blend theme color with animated RGB
            float blend = 0.5f;
            _glowBrush.Color = new Color4(
                rgbColor.R * blend + (primary.R / 255f) * (1 - blend),
                rgbColor.G * blend + (primary.G / 255f) * (1 - blend),
                rgbColor.B * blend + (primary.B / 255f) * (1 - blend),
                0.4f);
            
            // Body gradient brush (vertical, center lighter)
            if (_bodyGradientBrush == null)
            {
                var gradientStops = new[]
                {
                    new GradientStop(0f, new Color4(0.2f, 0.2f, 0.25f, 0.8f)),
                    new GradientStop(0.5f, new Color4(0.15f, 0.15f, 0.2f, 0.6f)),
                    new GradientStop(1f, new Color4(0.1f, 0.1f, 0.15f, 0.5f))
                };
                
                var stopCollection = renderTarget.CreateGradientStopCollection(gradientStops);
                try
                {
                    _bodyGradientBrush = renderTarget.CreateLinearGradientBrush(
                        new LinearGradientBrushProperties
                        {
                            StartPoint = new Vector2(MOUSE_WIDTH / 2, 0),
                            EndPoint = new Vector2(MOUSE_WIDTH / 2, MOUSE_HEIGHT)
                        },
                        stopCollection);
                }
                finally
                {
                    stopCollection?.Dispose();
                }
            }
            
            // RGB strip brush (animated, using theme colors)
            if (_rgbStripBrush == null)
            {
                // Use theme primary color with variations for RGB effect
                float r = primary.R / 255f;
                float g = primary.G / 255f;
                float b = primary.B / 255f;
                
                var rgbStops = new[]
                {
                    new GradientStop(0f, new Color4(r, g * 0.5f, b, 1f)),      // Theme color variant 1
                    new GradientStop(0.33f, new Color4(r, g, b, 1f)),           // Full theme color
                    new GradientStop(0.66f, new Color4(r * 0.5f, g, b, 1f)),   // Theme color variant 2
                    new GradientStop(1f, new Color4(r, g, b * 0.5f, 1f))      // Theme color variant 3
                };
                
                var stopCollection = renderTarget.CreateGradientStopCollection(rgbStops);
                try
                {
                    _rgbStripBrush = renderTarget.CreateLinearGradientBrush(
                        new LinearGradientBrushProperties
                        {
                            StartPoint = new Vector2(50, MOUSE_HEIGHT - 15),
                            EndPoint = new Vector2(MOUSE_WIDTH - 50, MOUSE_HEIGHT - 15)
                        },
                        stopCollection);
                }
                finally
                {
                    stopCollection?.Dispose();
                }
            }
            else
            {
                // Update RGB strip colors to match theme (recreate if needed)
                // For now, we'll update the gradient stops dynamically
                float r = primary.R / 255f;
                float g = primary.G / 255f;
                float b = primary.B / 255f;
                
                // Recreate gradient stops with theme colors
                var rgbStops = new[]
                {
                    new GradientStop(0f, new Color4(r, g * 0.5f, b, 1f)),
                    new GradientStop(0.33f, new Color4(r, g, b, 1f)),
                    new GradientStop(0.66f, new Color4(r * 0.5f, g, b, 1f)),
                    new GradientStop(1f, new Color4(r, g, b * 0.5f, 1f))
                };
                
                // Note: Direct2D doesn't allow updating gradient stops directly
                // We'd need to recreate the brush, but for performance we'll keep the existing one
                // The animation will still work with the theme-based colors
            }
            
            // Animate RGB strip (shift gradient)
            float offset = (time * 0.1f) % 1f;
            _rgbStripBrush.StartPoint = new Vector2(50 - offset * 50, MOUSE_HEIGHT - 15);
            _rgbStripBrush.EndPoint = new Vector2(MOUSE_WIDTH - 50 - offset * 50, MOUSE_HEIGHT - 15);
        }
        
        private void DrawRGBGlow(ID2D1RenderTarget renderTarget, Vector2 center, float time)
        {
            // Multi-pass glow for soft effect
            float baseRadius = 100f + (float)Math.Sin(time * 2f) * 20f;
            
            for (int i = 5; i > 0; i--)
            {
                float radius = baseRadius + i * 15f;
                float alpha = (5 - i) / 5f * 0.15f;
                
                // Animated RGB color
                float hue = (time * 50f + i * 30f) % 360f;
                Color4 glowColor = HsvToRgb(hue, 1f, 1f);
                
                using (var glowBrush = renderTarget.CreateSolidColorBrush(new Color4(glowColor.R, glowColor.G, glowColor.B, alpha)))
                {
                    var ellipse = new Ellipse(center, radius, radius);
                    renderTarget.DrawEllipse(ellipse, glowBrush, 8f);
                }
            }
        }
        
        private void DrawMouseBody(ID2D1RenderTarget renderTarget, System.Drawing.Color primaryColorGdi)
        {
            // Fill with gradient (glassmorphism effect)
            renderTarget.FillGeometry(_mouseBodyGeometry, _bodyGradientBrush);
            
            // Border (theme color, subtle)
            using (var borderBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f,
                primaryColorGdi.G / 255f,
                primaryColorGdi.B / 255f, 0.6f)))
            {
                renderTarget.DrawGeometry(_mouseBodyGeometry, borderBrush, 2f);
            }
        }

        private void DrawGamingShellDetails(ID2D1RenderTarget renderTarget, System.Drawing.Color primaryColorGdi)
        {
            float r = primaryColorGdi.R / 255f;
            float g = primaryColorGdi.G / 255f;
            float b = primaryColorGdi.B / 255f;

            var palmPanel = new RoundedRectangle(new System.Drawing.RectangleF(48, 96, 144, 130), 30, 30);
            using (var panelBrush = renderTarget.CreateSolidColorBrush(new Color4(0.055f, 0.065f, 0.09f, 0.92f)))
            using (var panelBorder = renderTarget.CreateSolidColorBrush(new Color4(r, g, b, 0.32f)))
            {
                renderTarget.FillRoundedRectangle(palmPanel, panelBrush);
                renderTarget.DrawRoundedRectangle(palmPanel, panelBorder, 1.5f);
            }

            var spine = new RoundedRectangle(new System.Drawing.RectangleF(109, 91, 22, 112), 11, 11);
            using (var spineBrush = renderTarget.CreateSolidColorBrush(new Color4(0.11f, 0.125f, 0.17f, 0.95f)))
            using (var accentBrush = renderTarget.CreateSolidColorBrush(new Color4(r, g, b, 0.75f)))
            {
                renderTarget.FillRoundedRectangle(spine, spineBrush);
                renderTarget.DrawRoundedRectangle(spine, accentBrush, 1.25f);

                var logoLight = new RoundedRectangle(new System.Drawing.RectangleF(113, 166, 14, 24), 5, 5);
                renderTarget.FillRoundedRectangle(logoLight, accentBrush);
            }

            using (var gripBrush = renderTarget.CreateSolidColorBrush(new Color4(0.16f, 0.18f, 0.23f, 0.92f)))
            using (var ventBrush = renderTarget.CreateSolidColorBrush(new Color4(r, g, b, 0.55f)))
            {
                renderTarget.FillRoundedRectangle(
                    new RoundedRectangle(new System.Drawing.RectangleF(28, 126, 22, 72), 9, 9), gripBrush);
                renderTarget.FillRoundedRectangle(
                    new RoundedRectangle(new System.Drawing.RectangleF(190, 126, 22, 72), 9, 9), gripBrush);

                for (int i = 0; i < 3; i++)
                {
                    float y = 138 + i * 19;
                    renderTarget.FillRoundedRectangle(
                        new RoundedRectangle(new System.Drawing.RectangleF(33, y, 12, 5), 2.5f, 2.5f), ventBrush);
                    renderTarget.FillRoundedRectangle(
                        new RoundedRectangle(new System.Drawing.RectangleF(195, y, 12, 5), 2.5f, 2.5f), ventBrush);
                }
            }
        }
        
        private void DrawButton(
            ID2D1RenderTarget renderTarget,
            ID2D1PathGeometry geometry,
            bool pressed,
            string label,
            Vector2 textCenter,
            System.Drawing.Color pressedColorGdi,
            System.Drawing.Color idleColorGdi,
            float time)
        {
            if (pressed)
            {
                // PRESSED: Intense glow + bright fill
                Color4 pressedColor = new Color4(pressedColorGdi.R / 255f, pressedColorGdi.G / 255f, pressedColorGdi.B / 255f, 1f);
                
                // Multi-layer glow
                for (int i = 8; i > 0; i--)
                {
                    float alpha = (8 - i) / 8f * 0.4f;
                    using (var glowBrush = renderTarget.CreateSolidColorBrush(
                        new Color4(pressedColor.R, pressedColor.G, pressedColor.B, alpha)))
                    {
                        renderTarget.DrawGeometry(geometry, glowBrush, i * 2f);
                    }
                }
                
                // Bright fill
                using (var fillBrush = renderTarget.CreateSolidColorBrush(
                    new Color4(pressedColor.R, pressedColor.G, pressedColor.B, 0.9f)))
                {
                    renderTarget.FillGeometry(geometry, fillBrush);
                }
                
                // Border (bright)
                using (var borderBrush = renderTarget.CreateSolidColorBrush(
                    new Color4(pressedColor.R, pressedColor.G, pressedColor.B, 1f)))
                {
                    renderTarget.DrawGeometry(geometry, borderBrush, 2.5f);
                }
            }
            else
            {
                // IDLE: Translucent glassmorphism
                renderTarget.FillGeometry(geometry, _glassBrush);
                
                // Subtle border
                using (var borderBrush = renderTarget.CreateSolidColorBrush(new Color4(0.5f, 0.5f, 0.6f, 0.4f)))
                {
                    renderTarget.DrawGeometry(geometry, borderBrush, 1.5f);
                }
            }
            
            // Label text
            var textRect = new System.Drawing.RectangleF(textCenter.X - 20, textCenter.Y - 10, 40, 20);
            Color4 textColor = pressed 
                ? new Color4(1f, 1f, 1f, 1f) 
                : new Color4(0.8f, 0.8f, 0.8f, 0.7f);
            
            using (var textBrush = renderTarget.CreateSolidColorBrush(textColor))
            {
                renderTarget.DrawText(label, _buttonTextFormat, textRect, textBrush);
            }
        }
        
        private void DrawScrollWheel(
            ID2D1RenderTarget renderTarget,
            bool pressed,
            int wheelDelta,
            System.Drawing.Color primaryColorGdi,
            float time)
        {
            // Persist scroll animation - keep showing scroll effect for a duration after wheel stops
            long currentTime = Environment.TickCount;
            if (wheelDelta != 0)
            {
                _lastWheelDelta = wheelDelta;
                _lastWheelTime = currentTime;
            }
            
            // Check if we're still in animation period
            bool isInAnimationPeriod = (currentTime - _lastWheelTime) < WHEEL_ANIMATION_DURATION_MS;
            bool isScrolling = wheelDelta != 0 || isInAnimationPeriod; // Show animation if scrolling now or recently scrolled
            bool isActive = pressed || isScrolling;
            
            // Glow effect when active (full wheel glow, more intense when scrolling)
            if (isActive)
            {
                int glowLayers = isScrolling ? 8 : 5; // More layers when scrolling
                float maxAlpha = isScrolling ? 0.6f : 0.4f; // Brighter when scrolling
                
                for (int i = glowLayers; i > 0; i--)
                {
                    float alpha = (glowLayers - i) / (float)glowLayers * maxAlpha;
                    using (var glowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                        primaryColorGdi.R / 255f,
                        primaryColorGdi.G / 255f,
                        primaryColorGdi.B / 255f,
                        alpha)))
                    {
                        // Adjusted glow rect for smaller wheel (centered at x=120, width 7)
                        var glowRect = new System.Drawing.RectangleF(120 - i * 2 - 3.5f, 50 - i * 2, 7 + i * 4, 20 + i * 4);
                        renderTarget.DrawRoundedRectangle(
                            new RoundedRectangle(glowRect, 2 + i, 2 + i),
                            glowBrush, i * 1.5f);
                    }
                }
            }
            
            // Wheel body (chrome-like gradient, or theme color when active)
            if (isActive)
            {
                // Use theme color when active (brighter when scrolling)
                float brightness = isScrolling ? 1.2f : 1.0f; // 20% brighter when scrolling
                float minBrightness = isScrolling ? 0.8f : 0.7f;
                float maxBrightness = Math.Min(1.0f, brightness);
                
                using (var wheelBrush = renderTarget.CreateLinearGradientBrush(
                    new LinearGradientBrushProperties
                    {
                        StartPoint = new Vector2(145, 50),
                        EndPoint = new Vector2(145, 110)
                    },
                    renderTarget.CreateGradientStopCollection(new[]
                    {
                        new GradientStop(0f, new Color4(
                            primaryColorGdi.R / 255f * minBrightness,
                            primaryColorGdi.G / 255f * minBrightness,
                            primaryColorGdi.B / 255f * minBrightness, 1f)),
                        new GradientStop(0.5f, new Color4(
                            primaryColorGdi.R / 255f * maxBrightness,
                            primaryColorGdi.G / 255f * maxBrightness,
                            primaryColorGdi.B / 255f * maxBrightness, 1f)),
                        new GradientStop(1f, new Color4(
                            primaryColorGdi.R / 255f * minBrightness,
                            primaryColorGdi.G / 255f * minBrightness,
                            primaryColorGdi.B / 255f * minBrightness, 1f))
                    })))
                {
                    renderTarget.FillGeometry(_scrollWheelGeometry, wheelBrush);
                }
            }
            else
            {
                // Normal chrome gradient when idle
                using (var wheelBrush = renderTarget.CreateLinearGradientBrush(
                    new LinearGradientBrushProperties
                    {
                        StartPoint = new Vector2(120, 50), // Adjusted for centered wheel
                        EndPoint = new Vector2(120, 70)   // Adjusted for smaller wheel
                    },
                    renderTarget.CreateGradientStopCollection(new[]
                    {
                        new GradientStop(0f, new Color4(0.7f, 0.7f, 0.75f, 0.9f)),
                        new GradientStop(0.5f, new Color4(0.9f, 0.9f, 0.95f, 1f)),
                        new GradientStop(1f, new Color4(0.5f, 0.5f, 0.55f, 0.8f))
                    })))
                {
                    renderTarget.FillGeometry(_scrollWheelGeometry, wheelBrush);
                }
            }
            
            // Ridges (horizontal lines INSIDE the wheel, theme color when active, brighter when scrolling)
            float ridgeBrightness = isScrolling ? 0.8f : (isActive ? 0.5f : 0.3f);
            using (var ridgeBrush = renderTarget.CreateSolidColorBrush(
                isActive ? new Color4(
                    primaryColorGdi.R / 255f * ridgeBrightness,
                    primaryColorGdi.G / 255f * ridgeBrightness,
                    primaryColorGdi.B / 255f * ridgeBrightness, 1f)
                : new Color4(0.3f, 0.3f, 0.35f, 0.8f)))
            {
                // Wheel is from x=116.5 to x=123.5 (width 7), so lines should be inside (e.g., 118 to 122)
                for (int i = 0; i < 3; i++) // Fewer lines for smaller wheel
                {
                    float y = 55 + i * 7; // Adjusted for smaller wheel (was 65 + i * 10)
                    float ridgeWidth = isScrolling ? 1.5f : (isActive ? 1.2f : 0.8f); // Thinner lines for smaller wheel
                    renderTarget.DrawLine(new Vector2(118, y), new Vector2(122, y), ridgeBrush, ridgeWidth); // Inside the wheel geometry
                }
            }
            
            // Border (theme color when active, thicker and brighter when scrolling)
            float borderBrightness = isScrolling ? 1.3f : 1.0f;
            using (var borderBrush = renderTarget.CreateSolidColorBrush(
                isActive ? new Color4(
                    Math.Min(1f, primaryColorGdi.R / 255f * borderBrightness),
                    Math.Min(1f, primaryColorGdi.G / 255f * borderBrightness),
                    Math.Min(1f, primaryColorGdi.B / 255f * borderBrightness), 1f)
                : new Color4(0.6f, 0.6f, 0.7f, 0.9f)))
            {
                float borderWidth = isScrolling ? 4f : (isActive ? 3f : 2f);
                renderTarget.DrawGeometry(_scrollWheelGeometry, borderBrush, borderWidth);
            }
            
            // Scroll indicator (much smaller, adjusted for half-size wheel) - show during animation period
            if (isScrolling && _lastWheelDelta != 0)
            {
                string arrow = _lastWheelDelta > 0 ? "↑" : "↓";
                
                // Glow behind arrow when scrolling (adjusted for much smaller wheel)
                for (int i = 1; i > 0; i--)
                {
                    using (var arrowGlowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                        primaryColorGdi.R / 255f,
                        primaryColorGdi.G / 255f,
                        primaryColorGdi.B / 255f,
                        (1 - i) / 1f * 0.4f)))
                    {
                        var glowRect = new System.Drawing.RectangleF(118 - i, 58 - i, 7 + i * 2, 12 + i * 2); // Adjusted for half-size wheel
                        renderTarget.DrawText(arrow, _buttonTextFormat, glowRect, arrowGlowBrush);
                    }
                }
                
                var textRect = new System.Drawing.RectangleF(118, 58, 7, 12); // Adjusted for half-size wheel
                using (var arrowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    primaryColorGdi.R / 255f,
                    primaryColorGdi.G / 255f,
                    primaryColorGdi.B / 255f, 1f)))
                {
                    renderTarget.DrawText(arrow, _buttonTextFormat, textRect, arrowBrush);
                }
            }
        }
        
        private void DrawRGBStrip(ID2D1RenderTarget renderTarget, float time, System.Drawing.Color primaryColorGdi)
        {
            // RGB strip at bottom of mouse
            var stripRect = new RoundedRectangle(
                new System.Drawing.RectangleF(50, MOUSE_HEIGHT - 15, MOUSE_WIDTH - 100, 12),
                6, 6);
            
            renderTarget.FillRoundedRectangle(stripRect, _rgbStripBrush);
            
            // Glow below strip (using theme color)
            for (int i = 3; i > 0; i--)
            {
                float alpha = (3 - i) / 3f * 0.3f;
                using (var glowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    primaryColorGdi.R / 255f,
                    primaryColorGdi.G / 255f,
                    primaryColorGdi.B / 255f,
                    alpha)))
                {
                    var glowRect = new RoundedRectangle(
                        new System.Drawing.RectangleF(45 - i * 2, MOUSE_HEIGHT - 12 + i * 2, MOUSE_WIDTH - 90 + i * 4, 12 + i * 2),
                        6 + i, 6 + i);
                    renderTarget.DrawRoundedRectangle(glowRect, glowBrush, 2f);
                }
            }
        }
        
        private void DrawSideButtons(
            ID2D1RenderTarget renderTarget,
            InputStateSnapshot snapshot,
            System.Drawing.Color primaryColorGdi,
            System.Drawing.Color pressedColorGdi)
        {
            // Side buttons (X1, X2, X3, X4) on left side
            string[] buttonNames = { "X1", "X2", "X3", "X4" };
            float startY = 60f;
            float spacing = 25f;
            
            for (int i = 0; i < buttonNames.Length; i++)
            {
                bool pressed = snapshot.MouseButtons != null && 
                              snapshot.MouseButtons.Length > 3 + i && 
                              snapshot.MouseButtons[3 + i];
                
                var btnRect = new RoundedRectangle(
                    new System.Drawing.RectangleF(10, startY + i * spacing, 20, 20),
                    3, 3);
                
                Color4 btnColor = pressed
                    ? new Color4(pressedColorGdi.R / 255f, pressedColorGdi.G / 255f, pressedColorGdi.B / 255f, 0.8f)
                    : new Color4(0.2f, 0.2f, 0.25f, 0.5f);
                
                using (var btnBrush = renderTarget.CreateSolidColorBrush(btnColor))
                {
                    renderTarget.FillRoundedRectangle(btnRect, btnBrush);
                }
                
                // Border
                using (var borderBrush = renderTarget.CreateSolidColorBrush(new Color4(0.5f, 0.5f, 0.6f, 0.6f)))
                {
                    renderTarget.DrawRoundedRectangle(btnRect, borderBrush, 1.5f);
                }
                
                // Label
                var textRect = new System.Drawing.RectangleF(10, startY + i * spacing, 20, 20);
                using (var textBrush = renderTarget.CreateSolidColorBrush(new Color4(0.9f, 0.9f, 0.9f, 0.8f)))
                {
                    using (var smallFont = _writeFactory.CreateTextFormat(
                        "Segoe UI", null, FontWeight.Bold, Vortice.DirectWrite.FontStyle.Normal, FontStretch.Normal, 8f))
                    {
                        smallFont.TextAlignment = TextAlignment.Center;
                        smallFont.ParagraphAlignment = ParagraphAlignment.Center;
                        renderTarget.DrawText(buttonNames[i], smallFont, textRect, textBrush);
                    }
                }
            }
        }
        
        private Color4 HsvToRgb(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1 - Math.Abs((h / 60f) % 2f - 1f));
            float m = v - c;
            
            float r = 0, g = 0, b = 0;
            
            if (h >= 0 && h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h >= 60 && h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h >= 120 && h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h >= 180 && h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h >= 240 && h < 300)
            {
                r = x; g = 0; b = c;
            }
            else if (h >= 300 && h < 360)
            {
                r = c; g = 0; b = x;
            }
            
            return new Color4(r + m, g + m, b + m, 1f);
        }
        
        /// <summary>
        /// Render Standard-2 style mouse (simplified, rounded, based on GDI+ example)
        /// </summary>
        private void RenderStandard2(
            ID2D1RenderTarget renderTarget,
            InputStateSnapshot snapshot,
            OverlayTheme theme,
            GDIRenderContext context,
            float animationTime,
            bool leftPressed,
            bool rightPressed,
            bool middlePressed,
            int wheelDelta,
            System.Drawing.Color primaryColorGdi,
            System.Drawing.Color pressedColorGdi,
            System.Drawing.Color idleColorGdi)
        {
            float centerX = MOUSE_WIDTH / 2;
            float centerY = MOUSE_HEIGHT / 2;
            
            // 1. Mouse body - rounded rectangle with gradient (using theme colors)
            var bodyRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 80, centerY - 100, 160, 200),
                20, 20);
            
            // Body gradient (dark theme-based tones)
            float r = primaryColorGdi.R / 255f;
            float g = primaryColorGdi.G / 255f;
            float b = primaryColorGdi.B / 255f;
            
            var bodyStops = new[]
            {
                new GradientStop(0f, new Color4(r * 0.2f, g * 0.2f, b * 0.2f, 1f)), // Dark variant of theme
                new GradientStop(1f, new Color4(r * 0.1f, g * 0.1f, b * 0.1f, 1f))  // Darker variant
            };
            var bodyStopCollection = renderTarget.CreateGradientStopCollection(bodyStops);
            using (var bodyBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX, centerY - 100),
                    EndPoint = new Vector2(centerX, centerY + 100)
                },
                bodyStopCollection))
            {
                renderTarget.FillRoundedRectangle(bodyRect, bodyBrush);
            }
            bodyStopCollection?.Dispose();
            
            // Outer border (theme color)
            using (var borderBrush = renderTarget.CreateSolidColorBrush(new Color4(r, g, b, 0.8f)))
            {
                renderTarget.DrawRoundedRectangle(bodyRect, borderBrush, 2f);
            }
            
            // 2. Central divider line (vertical separation between L and R) - theme color
            using (var dividerBrush = renderTarget.CreateSolidColorBrush(new Color4(r, g, b, 0.7f)))
            {
                renderTarget.DrawLine(
                    new Vector2(centerX, centerY - 100),
                    new Vector2(centerX, centerY - 30),
                    dividerBrush, 1.5f);
            }
            
            // 3. Left button (L)
            var leftButtonRect = new System.Drawing.RectangleF(centerX - 70, centerY - 95, 60, 70);
            if (leftPressed)
            {
                using (var pressedBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    pressedColorGdi.R / 255f,
                    pressedColorGdi.G / 255f,
                    pressedColorGdi.B / 255f,
                    0.8f)))
                {
                    renderTarget.FillRoundedRectangle(new RoundedRectangle(leftButtonRect, 5, 5), pressedBrush);
                }
            }
            
            // Button text - use theme color
            using (var textBrush = renderTarget.CreateSolidColorBrush(new Color4(r * 0.6f, g * 0.6f, b * 0.6f, 1f)))
            {
                var textRect = new System.Drawing.RectangleF(leftButtonRect.X, leftButtonRect.Y, leftButtonRect.Width, leftButtonRect.Height);
                renderTarget.DrawText("L", _buttonTextFormat, textRect, textBrush);
            }
            
            // 4. Right button (R)
            var rightButtonRect = new System.Drawing.RectangleF(centerX + 10, centerY - 95, 60, 70);
            if (rightPressed)
            {
                using (var pressedBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    pressedColorGdi.R / 255f,
                    pressedColorGdi.G / 255f,
                    pressedColorGdi.B / 255f,
                    0.8f)))
                {
                    renderTarget.FillRoundedRectangle(new RoundedRectangle(rightButtonRect, 5, 5), pressedBrush);
                }
            }
            
            // Button text - use theme color
            using (var textBrush = renderTarget.CreateSolidColorBrush(new Color4(r * 0.6f, g * 0.6f, b * 0.6f, 1f)))
            {
                var textRect = new System.Drawing.RectangleF(rightButtonRect.X, rightButtonRect.Y, rightButtonRect.Width, rightButtonRect.Height);
                renderTarget.DrawText("R", _buttonTextFormat, textRect, textBrush);
            }
            
            // 5. Scroll wheel (simple rounded rectangle) - using theme colors
            var scrollWheelRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 15, centerY - 80, 30, 50),
                5, 5);
            
            // Wheel fill - dark variant of theme
            using (var wheelBrush = renderTarget.CreateSolidColorBrush(new Color4(r * 0.15f, g * 0.15f, b * 0.15f, 1f)))
            {
                renderTarget.FillRoundedRectangle(scrollWheelRect, wheelBrush);
            }
            
            // Wheel border - theme color
            using (var wheelPen = renderTarget.CreateSolidColorBrush(new Color4(r * 0.4f, g * 0.4f, b * 0.4f, 0.8f)))
            {
                renderTarget.DrawRoundedRectangle(scrollWheelRect, wheelPen, 1.5f);
            }
            
            // Horizontal lines on wheel - theme color
            using (var lineBrush = renderTarget.CreateSolidColorBrush(new Color4(r * 0.3f, g * 0.3f, b * 0.3f, 0.7f)))
            {
                renderTarget.DrawLine(new Vector2(centerX - 10, centerY - 70), new Vector2(centerX + 10, centerY - 70), lineBrush, 1f);
                renderTarget.DrawLine(new Vector2(centerX - 10, centerY - 55), new Vector2(centerX + 10, centerY - 55), lineBrush, 1f);
                renderTarget.DrawLine(new Vector2(centerX - 10, centerY - 40), new Vector2(centerX + 10, centerY - 40), lineBrush, 1f);
            }
            
            // 6. Accent strip at bottom (theme-based gradient with animation)
            var rgbRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 70, centerY + 80, 140, 15),
                7.5f, 7.5f);
            
            // Theme-based gradient (variations of primary color)
            var rgbStops = new[]
            {
                new GradientStop(0f, new Color4(r * 0.5f, g * 0.5f, b * 0.5f, 1f)),      // Darker theme
                new GradientStop(0.33f, new Color4(r, g, b, 1f)),                         // Full theme color
                new GradientStop(0.66f, new Color4(r * 0.7f, g * 0.7f, b * 0.7f, 1f)),  // Lighter theme
                new GradientStop(1f, new Color4(r * 0.4f, g * 0.4f, b * 0.4f, 1f))       // Darker theme
            };
            var rgbStopCollection = renderTarget.CreateGradientStopCollection(rgbStops);
            
            // Animate RGB strip
            float offset = (animationTime * 0.1f) % 1f;
            using (var rgbBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX - 70 - offset * 140, centerY + 87.5f),
                    EndPoint = new Vector2(centerX + 70 - offset * 140, centerY + 87.5f)
                },
                rgbStopCollection))
            {
                renderTarget.FillRoundedRectangle(rgbRect, rgbBrush);
            }
            rgbStopCollection?.Dispose();
            
            // 8. Side buttons (X1, X2, X3, X4) - same as Standard style
            DrawSideButtons(renderTarget, snapshot, primaryColorGdi, pressedColorGdi);
        }
        
        /// <summary>
        /// Render GlassMorph style mouse (glassmorphism with particles, grid, translucent effects)
        /// </summary>
        private void RenderGlassMorph(
            ID2D1RenderTarget renderTarget,
            InputStateSnapshot snapshot,
            OverlayTheme theme,
            GDIRenderContext context,
            float animationTime,
            bool leftPressed,
            bool rightPressed,
            bool middlePressed,
            int wheelDelta,
            System.Drawing.Color primaryColorGdi,
            System.Drawing.Color pressedColorGdi,
            System.Drawing.Color idleColorGdi)
        {
            float centerX = MOUSE_WIDTH / 2;
            float centerY = MOUSE_HEIGHT / 2;
            
            // Extract theme color components
            float r = primaryColorGdi.R / 255f;
            float g = primaryColorGdi.G / 255f;
            float b = primaryColorGdi.B / 255f;
            
            // 1. Background gradient (animated, behind mouse area)
            var bgRect = new System.Drawing.RectangleF(-50, -50, MOUSE_WIDTH + 100, MOUSE_HEIGHT + 100);
            var bgStops = new[]
            {
                new GradientStop(0f, new Color4(0.06f, 0.02f, 0.12f, 1f)), // Color.FromArgb(15, 5, 30)
                new GradientStop(1f, new Color4(0.02f, 0.06f, 0.14f, 1f))  // Color.FromArgb(5, 15, 35)
            };
            var bgStopCollection = renderTarget.CreateGradientStopCollection(bgStops);
            using (var bgBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(0, 0),
                    EndPoint = new Vector2(MOUSE_WIDTH + 100, MOUSE_HEIGHT + 100)
                },
                bgStopCollection))
            {
                renderTarget.FillRectangle(bgRect, bgBrush);
            }
            bgStopCollection?.Dispose();
            
            // 2. Grid pattern (futuristic grid)
            using (var gridBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f * 0.4f,
                primaryColorGdi.G / 255f * 0.4f,
                primaryColorGdi.B / 255f * 0.4f,
                0.2f)))
            {
                for (int x = 0; x < MOUSE_WIDTH + 100; x += 40)
                {
                    renderTarget.DrawLine(
                        new Vector2(x - 50, 0),
                        new Vector2(x - 50, MOUSE_HEIGHT + 100),
                        gridBrush, 1f);
                }
                for (int y = 0; y < MOUSE_HEIGHT + 100; y += 40)
                {
                    renderTarget.DrawLine(
                        new Vector2(0, y - 50),
                        new Vector2(MOUSE_WIDTH + 100, y - 50),
                        gridBrush, 1f);
                }
            }
            
            // 3. RGB Glow halo (animated, behind mouse)
            DrawRGBGlow(renderTarget, new Vector2(centerX, centerY), animationTime);
            
            // 4. Mouse body - glassmorphism (highly translucent)
            var bodyRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 75, centerY - 95, 150, 195),
                20, 20);
            
            // Glassmorphism effect - very translucent with theme color
            using (var glassBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f * 0.15f,
                primaryColorGdi.G / 255f * 0.15f,
                primaryColorGdi.B / 255f * 0.15f,
                0.3f))) // Very transparent
            {
                renderTarget.FillRoundedRectangle(bodyRect, glassBrush);
            }
            
            // Glass border (theme color, subtle)
            using (var borderBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f,
                primaryColorGdi.G / 255f,
                primaryColorGdi.B / 255f,
                0.5f)))
            {
                renderTarget.DrawRoundedRectangle(bodyRect, borderBrush, 2f);
            }
            
            // 5. Glass highlight (top edge - glassmorphism effect)
            var highlightRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 73, centerY - 93, 146, 40),
                18, 18);
            var highlightStops = new[]
            {
                new GradientStop(0f, new Color4(1f, 1f, 1f, 0.4f)),
                new GradientStop(1f, new Color4(1f, 1f, 1f, 0f))
            };
            var highlightStopCollection = renderTarget.CreateGradientStopCollection(highlightStops);
            using (var highlightBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX, centerY - 93),
                    EndPoint = new Vector2(centerX, centerY - 53)
                },
                highlightStopCollection))
            {
                renderTarget.FillRoundedRectangle(highlightRect, highlightBrush);
            }
            highlightStopCollection?.Dispose();
            
            // 6. Central divider line (vertical separation)
            using (var dividerBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f,
                primaryColorGdi.G / 255f,
                primaryColorGdi.B / 255f,
                0.4f)))
            {
                renderTarget.DrawLine(
                    new Vector2(centerX, centerY - 95),
                    new Vector2(centerX, centerY - 30),
                    dividerBrush, 1.5f);
            }
            
            // 7. Left button (L) - glassmorphism style
            var leftButtonRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 70, centerY - 90, 65, 75),
                8, 8);
            
            if (leftPressed)
            {
                using (var pressedBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    pressedColorGdi.R / 255f,
                    pressedColorGdi.G / 255f,
                    pressedColorGdi.B / 255f,
                    0.6f))) // More visible when pressed
                {
                    renderTarget.FillRoundedRectangle(leftButtonRect, pressedBrush);
                }
            }
            else
            {
                using (var idleBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    idleColorGdi.R / 255f,
                    idleColorGdi.G / 255f,
                    idleColorGdi.B / 255f,
                    0.2f))) // Very transparent when idle
                {
                    renderTarget.FillRoundedRectangle(leftButtonRect, idleBrush);
                }
            }
            
            // Button border
            using (var leftBorderBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f,
                primaryColorGdi.G / 255f,
                primaryColorGdi.B / 255f,
                0.6f)))
            {
                renderTarget.DrawRoundedRectangle(leftButtonRect, leftBorderBrush, leftPressed ? 2f : 1.5f);
            }
            
            // Button text
            using (var textBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f,
                primaryColorGdi.G / 255f,
                primaryColorGdi.B / 255f,
                leftPressed ? 1f : 0.8f)))
            {
                var textRect = new System.Drawing.RectangleF(centerX - 70, centerY - 90, 65, 75);
                renderTarget.DrawText("L", _buttonTextFormat, textRect, textBrush);
            }
            
            // 8. Right button (R) - glassmorphism style
            var rightButtonRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX + 5, centerY - 90, 65, 75),
                8, 8);
            
            if (rightPressed)
            {
                using (var pressedBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    pressedColorGdi.R / 255f,
                    pressedColorGdi.G / 255f,
                    pressedColorGdi.B / 255f,
                    0.6f))) // More visible when pressed
                {
                    renderTarget.FillRoundedRectangle(rightButtonRect, pressedBrush);
                }
            }
            else
            {
                using (var idleBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    idleColorGdi.R / 255f,
                    idleColorGdi.G / 255f,
                    idleColorGdi.B / 255f,
                    0.2f))) // Very transparent when idle
                {
                    renderTarget.FillRoundedRectangle(rightButtonRect, idleBrush);
                }
            }
            
            // Button border
            using (var rightBorderBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f,
                primaryColorGdi.G / 255f,
                primaryColorGdi.B / 255f,
                0.6f)))
            {
                renderTarget.DrawRoundedRectangle(rightButtonRect, rightBorderBrush, rightPressed ? 2f : 1.5f);
            }
            
            // Button text
            using (var textBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f,
                primaryColorGdi.G / 255f,
                primaryColorGdi.B / 255f,
                rightPressed ? 1f : 0.8f)))
            {
                var textRect = new System.Drawing.RectangleF(centerX + 5, centerY - 90, 65, 75);
                renderTarget.DrawText("R", _buttonTextFormat, textRect, textBrush);
            }
            
            // 9. Scroll wheel - glassmorphism style
            var scrollWheelRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 12, centerY - 75, 24, 45),
                6, 6);
            
            // Wheel glassmorphism fill
            using (var wheelBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f * 0.2f,
                primaryColorGdi.G / 255f * 0.2f,
                primaryColorGdi.B / 255f * 0.2f,
                0.4f)))
            {
                renderTarget.FillRoundedRectangle(scrollWheelRect, wheelBrush);
            }
            
            // Wheel border
            using (var wheelBorderBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f,
                primaryColorGdi.G / 255f,
                primaryColorGdi.B / 255f,
                0.6f)))
            {
                renderTarget.DrawRoundedRectangle(scrollWheelRect, wheelBorderBrush, 1.5f);
            }
            
            // Horizontal lines on wheel
            using (var lineBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f * 0.5f,
                primaryColorGdi.G / 255f * 0.5f,
                primaryColorGdi.B / 255f * 0.5f,
                0.6f)))
            {
                renderTarget.DrawLine(new Vector2(centerX - 8, centerY - 65), new Vector2(centerX + 8, centerY - 65), lineBrush, 1f);
                renderTarget.DrawLine(new Vector2(centerX - 8, centerY - 52), new Vector2(centerX + 8, centerY - 52), lineBrush, 1f);
                renderTarget.DrawLine(new Vector2(centerX - 8, centerY - 40), new Vector2(centerX + 8, centerY - 40), lineBrush, 1f);
            }
            
            // 10. Central glow effect (theme color)
            for (int i = 5; i > 0; i--)
            {
                float alpha = (5 - i) / 5f * 0.25f;
                float radiusX = 50 - i * 8;
                float radiusY = 35 - i * 6;
                var glowEllipse = new Ellipse(new Vector2(centerX, centerY - 5), radiusX, radiusY);
                using (var glowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    primaryColorGdi.R / 255f,
                    primaryColorGdi.G / 255f,
                    primaryColorGdi.B / 255f,
                    alpha)))
                {
                    renderTarget.FillEllipse(glowEllipse, glowBrush);
                }
            }
            
            // 11. RGB strip at bottom (theme-based animated gradient)
            var rgbRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 70, centerY + 85, 140, 18),
                9, 9);
            
            // Theme-based animated gradient
            float rgbOffset = (animationTime * 0.12f) % 1f;
            var rgbStops = new[]
            {
                new GradientStop(0f, new Color4(r * 0.4f, g * 0.4f, b * 0.4f, 1f)),
                new GradientStop(0.33f, new Color4(r, g, b, 1f)),
                new GradientStop(0.66f, new Color4(r * 0.7f, g * 0.7f, b * 0.7f, 1f)),
                new GradientStop(1f, new Color4(r * 0.3f, g * 0.3f, b * 0.3f, 1f))
            };
            var rgbStopCollection = renderTarget.CreateGradientStopCollection(rgbStops);
            
            using (var rgbBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX - 70 - rgbOffset * 140, centerY + 94),
                    EndPoint = new Vector2(centerX + 70 - rgbOffset * 140, centerY + 94)
                },
                rgbStopCollection))
            {
                renderTarget.FillRoundedRectangle(rgbRect, rgbBrush);
            }
            rgbStopCollection?.Dispose();
            
            // RGB strip glow
            for (int i = 2; i > 0; i--)
            {
                float stripGlowAlpha = (2 - i) / 2f * 0.3f;
                using (var stripGlowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    primaryColorGdi.R / 255f,
                    primaryColorGdi.G / 255f,
                    primaryColorGdi.B / 255f,
                    stripGlowAlpha)))
                {
                    var stripGlowRect = new RoundedRectangle(
                        new System.Drawing.RectangleF(centerX - 70 - i, centerY + 85 - i, 140 + i * 2, 18 + i * 2),
                        9 + i, 9 + i);
                    renderTarget.DrawRoundedRectangle(stripGlowRect, stripGlowBrush, 1f);
                }
            }
        }
        
        /// <summary>
        /// Render Gaming Advanced style mouse (XAML-style with advanced effects: glow, depth, shadows, complex gradients)
        /// </summary>
        private void RenderGamingAdvanced(
            ID2D1RenderTarget renderTarget,
            InputStateSnapshot snapshot,
            OverlayTheme theme,
            GDIRenderContext context,
            float animationTime,
            bool leftPressed,
            bool rightPressed,
            bool middlePressed,
            int wheelDelta,
            System.Drawing.Color primaryColorGdi,
            System.Drawing.Color pressedColorGdi,
            System.Drawing.Color idleColorGdi)
        {
            float centerX = MOUSE_WIDTH / 2;
            float centerY = MOUSE_HEIGHT / 2;
            
            // 1. Outer glow halo (animated RGB, behind mouse) - XAML-style advanced glow
            for (int i = 8; i > 0; i--)
            {
                float radius = 120 + i * 20 + (float)Math.Sin(animationTime * 2f + i) * 15f;
                float alpha = (8 - i) / 8f * 0.25f;
                
                // Animated RGB color cycling
                float hue = (animationTime * 60f + i * 45f) % 360f;
                Color4 glowColor = HsvToRgb(hue, 1f, 1f);
                
                using (var glowBrush = renderTarget.CreateSolidColorBrush(new Color4(glowColor.R, glowColor.G, glowColor.B, alpha)))
                {
                    var glowEllipse = new Ellipse(new Vector2(centerX, centerY), radius, radius);
                    renderTarget.DrawEllipse(glowEllipse, glowBrush, 6f + i * 0.5f);
                }
            }
            
            // 2. Shadow layer (depth effect)
            var shadowRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 75 + 3, centerY - 95 + 3, 150, 195),
                20, 20);
            using (var shadowBrush = renderTarget.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0.4f)))
            {
                renderTarget.FillRoundedRectangle(shadowRect, shadowBrush);
            }
            
            // 3. Mouse body - advanced gradient with multiple stops (XAML-style)
            var bodyRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 75, centerY - 95, 150, 195),
                20, 20);
            
            // Complex multi-stop gradient (XAML-style)
            var bodyStops = new[]
            {
                new GradientStop(0f, new Color4(0.12f, 0.15f, 0.22f, 1f)),      // Top dark
                new GradientStop(0.3f, new Color4(0.18f, 0.22f, 0.30f, 1f)),    // Mid-light
                new GradientStop(0.5f, new Color4(0.25f, 0.30f, 0.40f, 1f)),   // Center bright
                new GradientStop(0.7f, new Color4(0.18f, 0.22f, 0.30f, 1f)),    // Mid-light
                new GradientStop(1f, new Color4(0.10f, 0.12f, 0.18f, 1f))      // Bottom dark
            };
            var bodyStopCollection = renderTarget.CreateGradientStopCollection(bodyStops);
            using (var bodyBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX, centerY - 95),
                    EndPoint = new Vector2(centerX, centerY + 100)
                },
                bodyStopCollection))
            {
                renderTarget.FillRoundedRectangle(bodyRect, bodyBrush);
            }
            bodyStopCollection?.Dispose();
            
            // 4. Inner highlight (top edge glow - XAML-style)
            var highlightRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 73, centerY - 93, 146, 30),
                18, 18);
            var highlightStops = new[]
            {
                new GradientStop(0f, new Color4(1f, 1f, 1f, 0.3f)),
                new GradientStop(1f, new Color4(1f, 1f, 1f, 0f))
            };
            var highlightStopCollection = renderTarget.CreateGradientStopCollection(highlightStops);
            using (var highlightBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX, centerY - 93),
                    EndPoint = new Vector2(centerX, centerY - 63)
                },
                highlightStopCollection))
            {
                renderTarget.FillRoundedRectangle(highlightRect, highlightBrush);
            }
            highlightStopCollection?.Dispose();
            
            // 5. Outer border with animated glow (XAML-style neon border)
            float borderGlowIntensity = 0.6f + (float)Math.Sin(animationTime * 3f) * 0.2f;
            using (var borderBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f * borderGlowIntensity,
                primaryColorGdi.G / 255f * borderGlowIntensity,
                primaryColorGdi.B / 255f * borderGlowIntensity,
                0.9f)))
            {
                renderTarget.DrawRoundedRectangle(bodyRect, borderBrush, 3f);
            }
            
            // Inner border (subtle)
            var innerRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 72, centerY - 94, 144, 193),
                18, 18);
            using (var innerBorderBrush = renderTarget.CreateSolidColorBrush(new Color4(0.3f, 0.3f, 0.4f, 0.5f)))
            {
                renderTarget.DrawRoundedRectangle(innerRect, innerBorderBrush, 1f);
            }
            
            // 6. Central divider line with glow (vertical separation)
            for (int i = 2; i > 0; i--)
            {
                float lineAlpha = (2 - i) / 2f * 0.8f;
                using (var dividerBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    primaryColorGdi.R / 255f,
                    primaryColorGdi.G / 255f,
                    primaryColorGdi.B / 255f,
                    lineAlpha)))
                {
                    renderTarget.DrawLine(
                        new Vector2(centerX - i, centerY - 95),
                        new Vector2(centerX - i, centerY - 30),
                        dividerBrush, 2f - i * 0.5f);
                }
            }
            
            // 7. Left button (L) - Advanced XAML-style with depth
            var leftButtonRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 70, centerY - 90, 65, 75),
                8, 8);
            
            // Button shadow
            var leftButtonShadow = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 69, centerY - 89, 65, 75),
                8, 8);
            using (var shadowBrush = renderTarget.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0.3f)))
            {
                renderTarget.FillRoundedRectangle(leftButtonShadow, shadowBrush);
            }
            
            // Button gradient
            var leftButtonStops = new[]
            {
                new GradientStop(0f, leftPressed ? 
                    new Color4(pressedColorGdi.R / 255f * 0.9f, pressedColorGdi.G / 255f * 0.9f, pressedColorGdi.B / 255f * 0.9f, 1f) :
                    new Color4(0.15f, 0.18f, 0.25f, 1f)),
                new GradientStop(0.5f, leftPressed ?
                    new Color4(pressedColorGdi.R / 255f, pressedColorGdi.G / 255f, pressedColorGdi.B / 255f, 1f) :
                    new Color4(0.20f, 0.24f, 0.32f, 1f)),
                new GradientStop(1f, leftPressed ?
                    new Color4(pressedColorGdi.R / 255f * 0.8f, pressedColorGdi.G / 255f * 0.8f, pressedColorGdi.B / 255f * 0.8f, 1f) :
                    new Color4(0.12f, 0.15f, 0.22f, 1f))
            };
            var leftButtonStopCollection = renderTarget.CreateGradientStopCollection(leftButtonStops);
            using (var leftButtonBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX - 37.5f, centerY - 90),
                    EndPoint = new Vector2(centerX - 37.5f, centerY - 15)
                },
                leftButtonStopCollection))
            {
                renderTarget.FillRoundedRectangle(leftButtonRect, leftButtonBrush);
            }
            leftButtonStopCollection?.Dispose();
            
            // Button border with glow when pressed
            float leftBorderIntensity = leftPressed ? 1.2f : 0.6f;
            using (var leftBorderBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f * leftBorderIntensity,
                primaryColorGdi.G / 255f * leftBorderIntensity,
                primaryColorGdi.B / 255f * leftBorderIntensity,
                0.9f)))
            {
                renderTarget.DrawRoundedRectangle(leftButtonRect, leftBorderBrush, leftPressed ? 2.5f : 1.5f);
            }
            
            // Button text with glow when pressed
            var leftTextColor = leftPressed ? 
                new Color4(1f, 1f, 1f, 1f) : 
                new Color4(primaryColorGdi.R / 255f, primaryColorGdi.G / 255f, primaryColorGdi.B / 255f, 0.9f);
            if (leftPressed)
            {
                // Text glow
                for (int i = 2; i > 0; i--)
                {
                    using (var textGlowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                        primaryColorGdi.R / 255f,
                        primaryColorGdi.G / 255f,
                        primaryColorGdi.B / 255f,
                        (2 - i) / 2f * 0.5f)))
                    {
                        var glowTextRect = new System.Drawing.RectangleF(
                            centerX - 70 - i, centerY - 90 - i, 65 + i * 2, 75 + i * 2);
                        renderTarget.DrawText("L", _buttonTextFormat, glowTextRect, textGlowBrush);
                    }
                }
            }
            using (var textBrush = renderTarget.CreateSolidColorBrush(leftTextColor))
            {
                var textRect = new System.Drawing.RectangleF(centerX - 70, centerY - 90, 65, 75);
                renderTarget.DrawText("L", _buttonTextFormat, textRect, textBrush);
            }
            
            // 8. Right button (R) - Same advanced style
            var rightButtonRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX + 5, centerY - 90, 65, 75),
                8, 8);
            
            // Button shadow
            var rightButtonShadow = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX + 6, centerY - 89, 65, 75),
                8, 8);
            using (var shadowBrush = renderTarget.CreateSolidColorBrush(new Color4(0f, 0f, 0f, 0.3f)))
            {
                renderTarget.FillRoundedRectangle(rightButtonShadow, shadowBrush);
            }
            
            // Button gradient
            var rightButtonStops = new[]
            {
                new GradientStop(0f, rightPressed ?
                    new Color4(pressedColorGdi.R / 255f * 0.9f, pressedColorGdi.G / 255f * 0.9f, pressedColorGdi.B / 255f * 0.9f, 1f) :
                    new Color4(0.15f, 0.18f, 0.25f, 1f)),
                new GradientStop(0.5f, rightPressed ?
                    new Color4(pressedColorGdi.R / 255f, pressedColorGdi.G / 255f, pressedColorGdi.B / 255f, 1f) :
                    new Color4(0.20f, 0.24f, 0.32f, 1f)),
                new GradientStop(1f, rightPressed ?
                    new Color4(pressedColorGdi.R / 255f * 0.8f, pressedColorGdi.G / 255f * 0.8f, pressedColorGdi.B / 255f * 0.8f, 1f) :
                    new Color4(0.12f, 0.15f, 0.22f, 1f))
            };
            var rightButtonStopCollection = renderTarget.CreateGradientStopCollection(rightButtonStops);
            using (var rightButtonBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX + 37.5f, centerY - 90),
                    EndPoint = new Vector2(centerX + 37.5f, centerY - 15)
                },
                rightButtonStopCollection))
            {
                renderTarget.FillRoundedRectangle(rightButtonRect, rightButtonBrush);
            }
            rightButtonStopCollection?.Dispose();
            
            // Button border with glow when pressed
            float rightBorderIntensity = rightPressed ? 1.2f : 0.6f;
            using (var rightBorderBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f * rightBorderIntensity,
                primaryColorGdi.G / 255f * rightBorderIntensity,
                primaryColorGdi.B / 255f * rightBorderIntensity,
                0.9f)))
            {
                renderTarget.DrawRoundedRectangle(rightButtonRect, rightBorderBrush, rightPressed ? 2.5f : 1.5f);
            }
            
            // Button text with glow when pressed
            var rightTextColor = rightPressed ?
                new Color4(1f, 1f, 1f, 1f) :
                new Color4(primaryColorGdi.R / 255f, primaryColorGdi.G / 255f, primaryColorGdi.B / 255f, 0.9f);
            if (rightPressed)
            {
                // Text glow
                for (int i = 2; i > 0; i--)
                {
                    using (var textGlowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                        primaryColorGdi.R / 255f,
                        primaryColorGdi.G / 255f,
                        primaryColorGdi.B / 255f,
                        (2 - i) / 2f * 0.5f)))
                    {
                        var glowTextRect = new System.Drawing.RectangleF(
                            centerX + 5 - i, centerY - 90 - i, 65 + i * 2, 75 + i * 2);
                        renderTarget.DrawText("R", _buttonTextFormat, glowTextRect, textGlowBrush);
                    }
                }
            }
            using (var textBrush = renderTarget.CreateSolidColorBrush(rightTextColor))
            {
                var textRect = new System.Drawing.RectangleF(centerX + 5, centerY - 90, 65, 75);
                renderTarget.DrawText("R", _buttonTextFormat, textRect, textBrush);
            }
            
            // 9. Scroll wheel - Advanced XAML-style with chrome effect
            var scrollWheelRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 12, centerY - 75, 24, 45),
                6, 6);
            
            // Wheel glow when active
            bool isWheelActive = middlePressed || wheelDelta != 0;
            if (isWheelActive)
            {
                for (int i = 3; i > 0; i--)
                {
                    float glowAlpha = (3 - i) / 3f * 0.4f;
                    using (var wheelGlowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                        primaryColorGdi.R / 255f,
                        primaryColorGdi.G / 255f,
                        primaryColorGdi.B / 255f,
                        glowAlpha)))
                    {
                        var glowWheelRect = new RoundedRectangle(
                            new System.Drawing.RectangleF(centerX - 12 - i, centerY - 75 - i, 24 + i * 2, 45 + i * 2),
                            6 + i, 6 + i);
                        renderTarget.DrawRoundedRectangle(glowWheelRect, wheelGlowBrush, 2f);
                    }
                }
            }
            
            // Chrome-like gradient for wheel
            var wheelStops = new[]
            {
                new GradientStop(0f, new Color4(0.5f, 0.5f, 0.55f, 0.9f)),
                new GradientStop(0.3f, new Color4(0.8f, 0.8f, 0.85f, 1f)),
                new GradientStop(0.5f, new Color4(0.95f, 0.95f, 1f, 1f)),
                new GradientStop(0.7f, new Color4(0.8f, 0.8f, 0.85f, 1f)),
                new GradientStop(1f, new Color4(0.4f, 0.4f, 0.45f, 0.8f))
            };
            var wheelStopCollection = renderTarget.CreateGradientStopCollection(wheelStops);
            using (var wheelBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX, centerY - 75),
                    EndPoint = new Vector2(centerX, centerY - 30)
                },
                wheelStopCollection))
            {
                renderTarget.FillRoundedRectangle(scrollWheelRect, wheelBrush);
            }
            wheelStopCollection?.Dispose();
            
            // Wheel border
            float wheelBorderIntensity = isWheelActive ? 1.3f : 0.7f;
            using (var wheelBorderBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f * wheelBorderIntensity,
                primaryColorGdi.G / 255f * wheelBorderIntensity,
                primaryColorGdi.B / 255f * wheelBorderIntensity,
                0.9f)))
            {
                renderTarget.DrawRoundedRectangle(scrollWheelRect, wheelBorderBrush, isWheelActive ? 2.5f : 1.5f);
            }
            
            // Horizontal lines on wheel (with glow when active)
            float lineIntensity = isWheelActive ? 0.9f : 0.5f;
            using (var lineBrush = renderTarget.CreateSolidColorBrush(new Color4(
                primaryColorGdi.R / 255f * lineIntensity,
                primaryColorGdi.G / 255f * lineIntensity,
                primaryColorGdi.B / 255f * lineIntensity,
                0.8f)))
            {
                renderTarget.DrawLine(new Vector2(centerX - 8, centerY - 65), new Vector2(centerX + 8, centerY - 65), lineBrush, 1.2f);
                renderTarget.DrawLine(new Vector2(centerX - 8, centerY - 52), new Vector2(centerX + 8, centerY - 52), lineBrush, 1.2f);
                renderTarget.DrawLine(new Vector2(centerX - 8, centerY - 40), new Vector2(centerX + 8, centerY - 40), lineBrush, 1.2f);
            }
            
            // 10. Central glow effect (animated, XAML-style)
            for (int i = 5; i > 0; i--)
            {
                float glowRadius = 50 - i * 8;
                float glowAlpha = (5 - i) / 5f * 0.3f;
                float hue = (animationTime * 40f + i * 20f) % 360f;
                Color4 glowColor = HsvToRgb(hue, 0.8f, 1f);
                
                using (var centerGlowBrush = renderTarget.CreateSolidColorBrush(new Color4(
                    glowColor.R * 0.5f + primaryColorGdi.R / 255f * 0.5f,
                    glowColor.G * 0.5f + primaryColorGdi.G / 255f * 0.5f,
                    glowColor.B * 0.5f + primaryColorGdi.B / 255f * 0.5f,
                    glowAlpha)))
                {
                    var centerGlowEllipse = new Ellipse(new Vector2(centerX, centerY - 5), glowRadius, glowRadius * 0.7f);
                    renderTarget.FillEllipse(centerGlowEllipse, centerGlowBrush);
                }
            }
            
            // 11. RGB strip at bottom (advanced animated rainbow)
            var rgbRect = new RoundedRectangle(
                new System.Drawing.RectangleF(centerX - 70, centerY + 85, 140, 18),
                9, 9);
            
            // Animated rainbow gradient
            float rgbOffset = (animationTime * 0.15f) % 1f;
            var rgbStops = new[]
            {
                new GradientStop(0f, new Color4(1f, 0f, 0f, 1f)),      // Red
                new GradientStop(0.16f, new Color4(1f, 0.5f, 0f, 1f)), // Orange
                new GradientStop(0.33f, new Color4(1f, 1f, 0f, 1f)),   // Yellow
                new GradientStop(0.5f, new Color4(0f, 1f, 0f, 1f)),    // Green
                new GradientStop(0.66f, new Color4(0f, 1f, 1f, 1f)),   // Cyan
                new GradientStop(0.83f, new Color4(0f, 0f, 1f, 1f)),  // Blue
                new GradientStop(1f, new Color4(1f, 0f, 1f, 1f))      // Magenta
            };
            var rgbStopCollection = renderTarget.CreateGradientStopCollection(rgbStops);
            
            using (var rgbBrush = renderTarget.CreateLinearGradientBrush(
                new LinearGradientBrushProperties
                {
                    StartPoint = new Vector2(centerX - 70 - rgbOffset * 140, centerY + 94),
                    EndPoint = new Vector2(centerX + 70 - rgbOffset * 140, centerY + 94)
                },
                rgbStopCollection))
            {
                renderTarget.FillRoundedRectangle(rgbRect, rgbBrush);
            }
            rgbStopCollection?.Dispose();
            
            // RGB strip glow
            for (int i = 2; i > 0; i--)
            {
                float stripGlowAlpha = (2 - i) / 2f * 0.4f;
                using (var stripGlowBrush = renderTarget.CreateSolidColorBrush(new Color4(1f, 1f, 1f, stripGlowAlpha)))
                {
                    var stripGlowRect = new RoundedRectangle(
                        new System.Drawing.RectangleF(centerX - 70 - i, centerY + 85 - i, 140 + i * 2, 18 + i * 2),
                        9 + i, 9 + i);
                    renderTarget.DrawRoundedRectangle(stripGlowRect, stripGlowBrush, 1.5f);
                }
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            // Dispose geometries
            _mouseBodyGeometry?.Dispose();
            _leftButtonGeometry?.Dispose();
            _rightButtonGeometry?.Dispose();
            _scrollWheelGeometry?.Dispose();
            
            // Dispose brushes
            _neonGreenBrush?.Dispose();
            _whiteBrush?.Dispose();
            _glassBrush?.Dispose();
            _glowBrush?.Dispose();
            _bodyGradientBrush?.Dispose();
            _rgbStripBrush?.Dispose();
            
            // Dispose text format
            _buttonTextFormat?.Dispose();
        }
    }
}
