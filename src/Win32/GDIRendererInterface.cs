using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Interface rendering module for GDI renderer
    /// Handles background, logo, title, and personalization features.
    /// </summary>
    internal class GDIRendererInterface
    {
        private readonly GDIRenderContext _context;
        
        // Layout constants
        private const int KEYBOARD_X = 50;
        private const int TITLE_Y = 20;
        private const int GLOBAL_PADDING = 50; // Global padding around application
        
        // Personalization features
        private Image _customLogo = null;
        private bool _useAnimatedBackground = false;
        private float _animatedBackgroundHue = 0.0f;
        
        public GDIRendererInterface(GDIRenderContext context)
        {
            _context = context;
        }
        
        /// <summary>
        /// Render background (animated or static)
        /// </summary>
        public void RenderBackground(Graphics g, int width, int height)
        {
            // If transparent background is enabled (for OBS/TikTok Studio), render black
            // Black will be made transparent via SetLayeredWindowAttributes color key
            if (_context.UseTransparentBackground)
            {
                g.Clear(Color.Black);
                return;
            }
            
            if (_useAnimatedBackground)
            {
                RenderAnimatedBackground(g, width, height);
            }
            else
            {
                // Use theme BackgroundBrush directly (supports gradients; fix broken styles)
                var bg = _context.Theme?.BackgroundBrush;
                if (bg != null)
                    g.FillRectangle(bg, 0, 0, width, height);
                else
                    g.Clear(Color.Black);
            }
        }
        
        /// <summary>
        /// Render the animated gradient background.
        /// </summary>
        private void RenderAnimatedBackground(Graphics g, int width, int height)
        {
            // Update hue for animation (cycles through colors)
            _animatedBackgroundHue += 0.5f;
            if (_animatedBackgroundHue >= 360.0f)
                _animatedBackgroundHue = 0.0f;
            
            // Create gradient from hue to hue+60 (smooth color transition)
            float hue1 = _animatedBackgroundHue;
            float hue2 = (_animatedBackgroundHue + 60.0f) % 360.0f;
            
            Color color1 = _context.HsvToRgb(hue1, 0.8f, 0.15f); // Dark, saturated
            Color color2 = _context.HsvToRgb(hue2, 0.8f, 0.25f); // Slightly brighter
            
            // Create diagonal gradient
            using (LinearGradientBrush gradientBrush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(width, height),
                color1,
                color2))
            {
                g.FillRectangle(gradientBrush, 0, 0, width, height);
            }
            
            // Add subtle overlay to maintain readability
            using (SolidBrush overlayBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
            {
                g.FillRectangle(overlayBrush, 0, 0, width, height);
            }
        }
        
        /// <summary>
        /// Render a custom logo in the bottom-right corner.
        /// </summary>
        public void RenderCustomLogo(Graphics g, int width, int height)
        {
            if (_customLogo == null) return;
            
            try
            {
                // Logo size (max 100x100, maintain aspect ratio)
                int logoSize = 100;
                float aspectRatio = (float)_customLogo.Width / _customLogo.Height;
                int logoWidth = aspectRatio > 1 ? logoSize : (int)(logoSize * aspectRatio);
                int logoHeight = aspectRatio > 1 ? (int)(logoSize / aspectRatio) : logoSize;
                
                // Position: bottom-right corner with 10px margin
                int logoX = width - logoWidth - 10;
                int logoY = height - logoHeight - 10;
                
                // Draw with semi-transparency
                using (ImageAttributes imgAttr = new ImageAttributes())
                {
                    // Set opacity (0.8 = 80% visible)
                    ColorMatrix matrix = new ColorMatrix(new float[][]
                    {
                        new float[] {1, 0, 0, 0, 0},
                        new float[] {0, 1, 0, 0, 0},
                        new float[] {0, 0, 1, 0, 0},
                        new float[] {0, 0, 0, 0.8f, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });
                    imgAttr.SetColorMatrix(matrix);
                    
                    g.DrawImage(_customLogo, 
                        new Rectangle(logoX, logoY, logoWidth, logoHeight),
                        0, 0, _customLogo.Width, _customLogo.Height,
                        GraphicsUnit.Pixel, imgAttr);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rendering custom logo: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Render title (optional, currently not used but available)
        /// </summary>
        public void RenderTitle(Graphics g)
        {
            string title = $"Keyboard & Mouse Overlay - {_context.CurrentStyle}";
            SizeF textSize = g.MeasureString(title, _context.TitleFont);
            float textX = GLOBAL_PADDING;
            float textY = GLOBAL_PADDING;
            
            // Draw title with theme primary color
            Color titleColor = _context.BrushToColor(_context.Theme.PrimaryColor);
            using (SolidBrush titleBrush = new SolidBrush(titleColor))
            {
                g.DrawString(title, _context.TitleFont, titleBrush, textX, textY);
            }
        }
        
        /// <summary>
        /// Set a custom logo image.
        /// </summary>
        public void SetCustomLogo(string imagePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    _customLogo?.Dispose();
                    _customLogo = null;
                    return;
                }
                
                if (System.IO.File.Exists(imagePath))
                {
                    _customLogo?.Dispose();
                    _customLogo = Image.FromFile(imagePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading custom logo: {ex.Message}");
                _customLogo = null;
            }
        }
        
        /// <summary>
        /// Enable or disable the animated background.
        /// </summary>
        public void SetAnimatedBackground(bool enabled)
        {
            _useAnimatedBackground = enabled;
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _customLogo?.Dispose();
            _customLogo = null;
        }
    }
}
