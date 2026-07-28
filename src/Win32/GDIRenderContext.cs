using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using GamingKeypressOverlay.Overlay;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Shared rendering context for keyboard, mouse, and interface renderers
    /// Contains theme, fonts, and helper methods
    /// </summary>
    public class GDIRenderContext
    {
        // Theme
        public OverlayTheme Theme { get; set; }
        public OverlayStyle CurrentStyle { get; set; }
        
        // Fonts
        public Font KeyFont { get; set; }
        public Font TitleFont { get; set; }
        
        // Brushes and Pens
        public SolidBrush KeyBrush { get; set; }
        public SolidBrush PressedKeyBrush { get; set; }
        public SolidBrush TextBrush { get; set; }
        public SolidBrush PressedTextBrush { get; set; }
        public SolidBrush BackgroundBrush { get; set; }
        public Pen KeyBorderPen { get; set; }
        public Pen PressedKeyBorderPen { get; set; }
        
        // Animation
        public long AnimationTime { get; set; }
        
        // Transparency mode for OBS/TikTok Studio capture
        public bool UseTransparentBackground { get; set; } = false;
        
        /// <summary>
        /// Convert GDI Brush to GDI Color
        /// </summary>
        public Color BrushToColor(Brush brush)
        {
            if (brush == null) return Color.FromArgb(40, 40, 40);
            
            if (brush is SolidBrush solidBrush)
            {
                return solidBrush.Color;
            }
            else if (brush is LinearGradientBrush gradientBrush)
            {
                // Use start color of gradient
                return gradientBrush.LinearColors[0];
            }
            
            // Default fallback
            return Color.FromArgb(40, 40, 40);
        }
        
        /// <summary>
        /// Convert HSV to RGB color
        /// </summary>
        public Color HsvToRgb(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
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
            
            return Color.FromArgb(255,
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255));
        }
        
        /// <summary>
        /// Convert HSL to RGB color
        /// </summary>
        public Color ColorFromHSL(float h, float s, float l)
        {
            h = h / 360f;
            float r, g, b;
            
            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                float q = l < 0.5f ? l * (1 + s) : l + s - l * s;
                float p = 2 * l - q;
                r = HueToRGB(p, q, h + 1f/3f);
                g = HueToRGB(p, q, h);
                b = HueToRGB(p, q, h - 1f/3f);
            }
            
            return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }
        
        private float HueToRGB(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1f/6f) return p + (q - p) * 6 * t;
            if (t < 1f/2f) return q;
            if (t < 2f/3f) return p + (q - p) * (2f/3f - t) * 6;
            return p;
        }
        
        /// <summary>
        /// Draw realistic multi-layer shadow for 3D depth effect
        /// </summary>
        public void DrawRealisticShadow(Graphics g, GraphicsPath path, Color shadowColor, int layers = 8)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            for (int i = layers; i > 0; i--)
            {
                using (var shadowPath = (GraphicsPath)path.Clone())
                {
                    // Progressive offset (more blur = more offset)
                    System.Drawing.Drawing2D.Matrix matrix = new System.Drawing.Drawing2D.Matrix();
                    matrix.Translate(i * 0.5f, i * 1.2f);
                    shadowPath.Transform(matrix);
                    
                    // Progressive alpha (more blur = more transparent)
                    int alpha = 30 - (i * 3);
                    if (alpha > 0)
                    {
                        using (var brush = new SolidBrush(Color.FromArgb(Math.Max(0, alpha), shadowColor)))
                        {
                            g.FillPath(brush, shadowPath);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Check if a key is pressed in the snapshot
        /// </summary>
        public bool IsKeyPressed(Input.InputStateSnapshot snapshot, byte vkCode)
        {
            if (snapshot == null) return false;
            return snapshot.Keys[vkCode];
        }
    }
}
