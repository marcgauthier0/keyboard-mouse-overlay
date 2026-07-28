using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using GamingKeypressOverlay.Overlay;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Glassmorphism mouse renderer - exact implementation from GamingMouseForm
    /// </summary>
    internal class GlassMorphMouseRenderer
    {
        private float _glowPhase = 0;
        private float _particlePhase = 0;
        private Random _rand = new Random();
        private Point[] _particles;
        private int _particleCount = 50;
        
        public GlassMorphMouseRenderer()
        {
            _particles = new Point[_particleCount];
            for (int i = 0; i < _particles.Length; i++)
                _particles[i] = new Point(_rand.Next(1000), _rand.Next(800));
        }
        
        public void RenderMouse(Graphics g, 
            bool leftPressed, bool rightPressed, bool middlePressed,
            Dictionary<string, bool> sideButtons, int wheelDelta,
            OverlayTheme theme, PointF position, GDIRenderContext context)
        {
            _glowPhase += 0.05f;
            _particlePhase += 0.02f;
            
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            
            float mouseX = position.X;
            float mouseY = position.Y;
            float mouseWidth = 280f;
            float mouseHeight = 250f;
            
            // Calculate center for mouse rendering
            int cx = (int)(mouseX + mouseWidth / 2);
            int cy = (int)(mouseY + mouseHeight / 2);
            
            // Background gradient animé (dans la zone de la souris)
            RectangleF mouseRect = new RectangleF(mouseX - 50, mouseY - 50, mouseWidth + 100, mouseHeight + 100);
            using (LinearGradientBrush bg = new LinearGradientBrush(
                mouseRect,
                Color.FromArgb(15, 5, 30),
                Color.FromArgb(5, 15, 35),
                45f))
            {
                g.FillRectangle(bg, mouseRect);
            }
            
            // Particules ambiantes (dans la zone de la souris)
            DrawParticles(g, (int)mouseX, (int)mouseY, (int)mouseWidth, (int)mouseHeight);
            
            // Grille futuriste (dans la zone de la souris)
            DrawGrid(g, (int)mouseX, (int)mouseY, (int)mouseWidth, (int)mouseHeight);
            
            // Halo lumineux RGB
            DrawRGBGlow(g, cx, cy);
            
            // Corps de la souris (glassmorphism translucide)
            DrawMouseBody(g, cx, cy, mouseWidth, mouseHeight, theme, context);
            
            // Détails glassmorphism
            DrawGlassDetails(g, cx, cy);
            
            // Molette avec effet chrome
            DrawScrollWheel(g, cx, cy - 30, middlePressed, wheelDelta);
            
            // Boutons gaming avec glow GDI+ intense (comme XAML mais performant)
            DrawButtons(g, cx, cy, leftPressed, rightPressed, theme, context);
            
            // LED RGB animées
            DrawRGBLeds(g, cx, cy);
            
            // Reflets lumineux
            DrawReflections(g, cx, cy);
            
            // Logo gaming
            DrawLogo(g, cx, cy + 40);
        }
        
        private void DrawParticles(Graphics g, int x, int y, int width, int height)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                float alpha = (float)Math.Sin(_particlePhase + i * 0.1f) * 0.5f + 0.5f;
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(alpha * 100), 100, 150, 255)))
                {
                    int px = _particles[i].X;
                    int py = _particles[i].Y;
                    
                    // Wrap particles within mouse area
                    if (px < x) px = x + width;
                    if (px > x + width) px = x;
                    if (py < y) py = y + height;
                    if (py > y + height) py = y;
                    
                    g.FillEllipse(b, px, py, 3, 3);
                    
                    _particles[i] = new Point(px, py + 1);
                    if (_particles[i].Y > y + height)
                        _particles[i] = new Point(_rand.Next(width) + x, y);
                }
            }
        }
        
        private void DrawGrid(Graphics g, int x, int y, int width, int height)
        {
            using (Pen p = new Pen(Color.FromArgb(20, 100, 150, 200)))
            {
                for (int gridX = x; gridX < x + width; gridX += 40)
                    g.DrawLine(p, gridX, y, gridX, y + height);
                for (int gridY = y; gridY < y + height; gridY += 40)
                    g.DrawLine(p, x, gridY, x + width, gridY);
            }
        }
        
        private void DrawRGBGlow(Graphics g, int cx, int cy)
        {
            float r = 200 + (float)Math.Sin(_glowPhase) * 30;
            
            for (int i = 8; i > 0; i--)
            {
                float alpha = (8 - i) / 8f * 0.15f;
                float hue = (_glowPhase * 50 + i * 30) % 360;
                Color c = HSVToRGB(hue, 1f, 1f);
                
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(alpha * 255), c)))
                {
                    float radius = r * i / 4;
                    g.FillEllipse(b, cx - radius, cy - radius, radius * 2, radius * 2);
                }
            }
        }
        
        private void DrawMouseBody(Graphics g, int cx, int cy, float mouseWidth, float mouseHeight, 
            OverlayTheme theme, GDIRenderContext context)
        {
            // Corps principal avec courbes ergonomiques
            GraphicsPath body = new GraphicsPath();
            float scaleX = mouseWidth / 160f;
            float scaleY = mouseHeight / 180f;
            
            body.AddBezier(
                cx - 80 * scaleX, cy + 80 * scaleY,
                cx - 100 * scaleX, cy - 20 * scaleY,
                cx - 80 * scaleX, cy - 80 * scaleY,
                cx, cy - 100 * scaleY
            );
            body.AddBezier(
                cx, cy - 100 * scaleY,
                cx + 80 * scaleX, cy - 80 * scaleY,
                cx + 100 * scaleX, cy - 20 * scaleY,
                cx + 80 * scaleX, cy + 80 * scaleY
            );
            body.AddBezier(
                cx + 80 * scaleX, cy + 80 * scaleY,
                cx + 60 * scaleX, cy + 100 * scaleY,
                cx - 60 * scaleX, cy + 100 * scaleY,
                cx - 80 * scaleX, cy + 80 * scaleY
            );
            body.CloseFigure();
            
            // Effet glassmorphism XAML-like - très translucide avec blur
            // Couleur basée sur le thème mais très transparente
            Color primaryColor = context.BrushToColor(theme.PrimaryColor);
            Color glassColor = Color.FromArgb(60, primaryColor.R, primaryColor.G, primaryColor.B);
            
            using (PathGradientBrush pb = new PathGradientBrush(body))
            {
                // Centre plus opaque, bords très transparents (effet glass)
                pb.CenterColor = Color.FromArgb(90, 30, 30, 45);
                pb.SurroundColors = new Color[] { Color.FromArgb(40, 15, 15, 25) };
                pb.CenterPoint = new PointF(cx, cy - 20 * scaleY);
                g.FillPath(pb, body);
            }
            
            // Bordure brillante subtile (glassmorphism)
            using (Pen pen = new Pen(Color.FromArgb(120, 150, 200, 255), 2))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawPath(pen, body);
            }
            
            // Bordure intérieure très subtile
            using (Pen pen2 = new Pen(Color.FromArgb(60, 200, 220, 255), 1))
            {
                g.DrawPath(pen2, body);
            }
            
            body.Dispose();
        }
        
        private void DrawGlassDetails(Graphics g, int cx, int cy)
        {
            // Panneaux de verre latéraux
            GraphicsPath leftPanel = new GraphicsPath();
            leftPanel.AddCurve(new PointF[] {
                new PointF(cx - 70, cy - 40),
                new PointF(cx - 85, cy),
                new PointF(cx - 70, cy + 40)
            });
            
            using (LinearGradientBrush lb = new LinearGradientBrush(
                new Point(cx - 90, cy - 50),
                new Point(cx - 60, cy + 50),
                Color.FromArgb(60, 100, 200, 255),
                Color.FromArgb(30, 50, 100, 200)))
            {
                using (Pen p = new Pen(lb, 15))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawPath(p, leftPanel);
                }
            }
            
            leftPanel.Dispose();
            
            // Panneau droit
            GraphicsPath rightPanel = new GraphicsPath();
            rightPanel.AddCurve(new PointF[] {
                new PointF(cx + 70, cy - 40),
                new PointF(cx + 85, cy),
                new PointF(cx + 70, cy + 40)
            });
            
            using (LinearGradientBrush rb = new LinearGradientBrush(
                new Point(cx + 60, cy - 50),
                new Point(cx + 90, cy + 50),
                Color.FromArgb(60, 255, 100, 150),
                Color.FromArgb(30, 200, 50, 100)))
            {
                using (Pen p = new Pen(rb, 15))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawPath(p, rightPanel);
                }
            }
            
            rightPanel.Dispose();
        }
        
        private void DrawScrollWheel(Graphics g, int cx, int cy, bool pressed, int wheelDelta)
        {
            Rectangle wheel = new Rectangle(cx - 15, cy - 15, 30, 50);
            
            // Chrome base
            using (LinearGradientBrush gb = new LinearGradientBrush(
                wheel,
                Color.FromArgb(200, 180, 180, 200),
                Color.FromArgb(255, 240, 240, 250),
                90f))
            {
                g.FillRectangle(gb, wheel);
            }
            
            // Rainures
            using (Pen p = new Pen(Color.FromArgb(150, 80, 80, 100), 2))
            {
                for (int i = 0; i < 5; i++)
                {
                    int yy = cy - 10 + i * 10;
                    g.DrawLine(p, cx - 12, yy, cx + 12, yy);
                }
            }
            
            // Reflet
            using (LinearGradientBrush hb = new LinearGradientBrush(
                new Rectangle(cx - 10, cy - 10, 20, 20),
                Color.FromArgb(100, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                45f))
            {
                g.FillEllipse(hb, cx - 8, cy - 8, 16, 16);
            }
            
            // Bordure
            g.DrawRectangle(new Pen(Color.FromArgb(200, 150, 200, 255), 2), wheel);
            
            // Scroll indicator
            if (wheelDelta != 0)
            {
                string arrow = wheelDelta > 0 ? "↑" : "↓";
                using (Font font = new Font("Segoe UI", 10, FontStyle.Bold))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 150, 200, 255)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(arrow, font, brush, cx, cy + 10, sf);
                }
            }
        }
        
        private void DrawButtons(Graphics g, int cx, int cy, bool leftPressed, bool rightPressed,
            OverlayTheme theme, GDIRenderContext context)
        {
            // ==========================================
            // BOUTON GAUCHE - Effet GDI+ intense quand pressé
            // ==========================================
            GraphicsPath leftBtn = new GraphicsPath();
            leftBtn.AddBezier(cx - 70, cy - 80, cx - 50, cy - 95, cx - 20, cy - 95, cx - 5, cy - 70);
            leftBtn.AddLine(cx - 5, cy - 70, cx - 5, cy + 20);
            leftBtn.AddLine(cx - 5, cy + 20, cx - 70, cy + 60);
            leftBtn.CloseFigure();
            
            if (leftPressed)
            {
                // GLOW INTENSE MULTI-COUCHES (comme XAML mais en GDI+)
                // Utilise la couleur du thème pour le bouton pressé
                Color pressedColor = context.BrushToColor(theme.MouseButtonPressed);
                Color glowColor = pressedColor; // Utilise la couleur du thème
                
                // Glow externe - plusieurs couches pour effet blur
                for (int i = 12; i > 0; i--)
                {
                    GraphicsPath glowPath = (GraphicsPath)leftBtn.Clone();
                    using (Matrix glowMatrix = new Matrix())
                    {
                        glowMatrix.Scale(1.0f + i * 0.03f, 1.0f + i * 0.03f);
                        glowMatrix.Translate(-i * 1.5f, -i * 1.5f);
                        glowPath.Transform(glowMatrix);
                        
                        int alpha = Math.Max(10, 180 - i * 12);
                        using (Pen glowPen = new Pen(Color.FromArgb(alpha, glowColor), i * 1.5f))
                        {
                            g.DrawPath(glowPen, glowPath);
                        }
                    }
                    glowPath.Dispose();
                }
                
                // Remplissage du bouton avec gradient lumineux
                using (PathGradientBrush pb = new PathGradientBrush(leftBtn))
                {
                    pb.CenterColor = Color.FromArgb(255, glowColor); // Centre très lumineux
                    pb.SurroundColors = new Color[] { Color.FromArgb(180, glowColor) };
                    pb.CenterPoint = new PointF(cx - 35, cy - 10);
                    g.FillPath(pb, leftBtn);
                }
                
                // Bordure brillante
                using (Pen borderPen = new Pen(Color.FromArgb(255, glowColor), 3))
                {
                    g.DrawPath(borderPen, leftBtn);
                }
                
                // Highlight interne pour effet 3D
                GraphicsPath highlightPath = (GraphicsPath)leftBtn.Clone();
                using (Matrix highlightMatrix = new Matrix())
                {
                    highlightMatrix.Scale(0.85f, 0.85f);
                    highlightMatrix.Translate(-8, -12);
                    highlightPath.Transform(highlightMatrix);
                    
                    using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                        new PointF(cx - 50, cy - 30),
                        new PointF(cx - 35, cy - 10),
                        Color.FromArgb(200, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255)))
                    {
                        g.FillPath(highlightBrush, highlightPath);
                    }
                }
                highlightPath.Dispose();
            }
            else
            {
                // État inactif - translucide glassmorphism
                using (PathGradientBrush pb = new PathGradientBrush(leftBtn))
                {
                    pb.CenterColor = Color.FromArgb(80, 60, 60, 90);
                    pb.SurroundColors = new Color[] { Color.FromArgb(40, 30, 30, 50) };
                    g.FillPath(pb, leftBtn);
                }
                
                using (Pen borderPen = new Pen(Color.FromArgb(100, 150, 200, 255), 2))
                {
                    g.DrawPath(borderPen, leftBtn);
                }
            }
            
            // Label "L" - plus visible quand pressé
            Color textColor = leftPressed 
                ? Color.FromArgb(255, 255, 255, 255) 
                : Color.FromArgb(150, 255, 255, 255);
            
            using (Font font = new Font("Segoe UI", 14, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(textColor))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("L", font, brush, cx - 35, cy - 10, sf);
            }
            
            leftBtn.Dispose();
            
            // ==========================================
            // BOUTON DROIT - Effet GDI+ intense quand pressé
            // ==========================================
            GraphicsPath rightBtn = new GraphicsPath();
            rightBtn.AddBezier(cx + 70, cy - 80, cx + 50, cy - 95, cx + 20, cy - 95, cx + 5, cy - 70);
            rightBtn.AddLine(cx + 5, cy - 70, cx + 5, cy + 20);
            rightBtn.AddLine(cx + 5, cy + 20, cx + 70, cy + 60);
            rightBtn.CloseFigure();
            
            if (rightPressed)
            {
                // GLOW INTENSE MULTI-COUCHES (comme XAML mais en GDI+)
                // Utilise la couleur du thème pour le bouton pressé
                Color pressedColor = context.BrushToColor(theme.MouseButtonPressed);
                Color glowColor = pressedColor; // Utilise la couleur du thème
                
                // Glow externe - plusieurs couches pour effet blur
                for (int i = 12; i > 0; i--)
                {
                    GraphicsPath glowPath = (GraphicsPath)rightBtn.Clone();
                    using (Matrix glowMatrix = new Matrix())
                    {
                        glowMatrix.Scale(1.0f + i * 0.03f, 1.0f + i * 0.03f);
                        glowMatrix.Translate(i * 1.5f, -i * 1.5f);
                        glowPath.Transform(glowMatrix);
                        
                        int alpha = Math.Max(10, 180 - i * 12);
                        using (Pen glowPen = new Pen(Color.FromArgb(alpha, glowColor), i * 1.5f))
                        {
                            g.DrawPath(glowPen, glowPath);
                        }
                    }
                    glowPath.Dispose();
                }
                
                // Remplissage du bouton avec gradient lumineux
                using (PathGradientBrush pb = new PathGradientBrush(rightBtn))
                {
                    pb.CenterColor = Color.FromArgb(255, glowColor); // Centre très lumineux
                    pb.SurroundColors = new Color[] { Color.FromArgb(180, glowColor) };
                    pb.CenterPoint = new PointF(cx + 35, cy - 10);
                    g.FillPath(pb, rightBtn);
                }
                
                // Bordure brillante
                using (Pen borderPen = new Pen(Color.FromArgb(255, glowColor), 3))
                {
                    g.DrawPath(borderPen, rightBtn);
                }
                
                // Highlight interne pour effet 3D
                GraphicsPath highlightPath = (GraphicsPath)rightBtn.Clone();
                using (Matrix highlightMatrix = new Matrix())
                {
                    highlightMatrix.Scale(0.85f, 0.85f);
                    highlightMatrix.Translate(8, -12);
                    highlightPath.Transform(highlightMatrix);
                    
                    using (LinearGradientBrush highlightBrush = new LinearGradientBrush(
                        new PointF(cx + 50, cy - 30),
                        new PointF(cx + 35, cy - 10),
                        Color.FromArgb(200, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255)))
                    {
                        g.FillPath(highlightBrush, highlightPath);
                    }
                }
                highlightPath.Dispose();
            }
            else
            {
                // État inactif - translucide glassmorphism
                using (PathGradientBrush pb = new PathGradientBrush(rightBtn))
                {
                    pb.CenterColor = Color.FromArgb(80, 60, 60, 90);
                    pb.SurroundColors = new Color[] { Color.FromArgb(40, 30, 30, 50) };
                    g.FillPath(pb, rightBtn);
                }
                
                using (Pen borderPen = new Pen(Color.FromArgb(100, 150, 200, 255), 2))
                {
                    g.DrawPath(borderPen, rightBtn);
                }
            }
            
            // Label "R" - plus visible quand pressé
            Color rightTextColor = rightPressed 
                ? Color.FromArgb(255, 255, 255, 255) 
                : Color.FromArgb(150, 255, 255, 255);
            
            using (Font font = new Font("Segoe UI", 14, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(rightTextColor))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString("R", font, brush, cx + 35, cy - 10, sf);
            }
            
            rightBtn.Dispose();
        }
        
        private void DrawRGBLeds(Graphics g, int cx, int cy)
        {
            Point[] ledPos = {
                new Point(cx - 60, cy + 70),
                new Point(cx - 30, cy + 75),
                new Point(cx, cy + 78),
                new Point(cx + 30, cy + 75),
                new Point(cx + 60, cy + 70)
            };
            
            for (int i = 0; i < ledPos.Length; i++)
            {
                float hue = (_glowPhase * 100 + i * 72) % 360;
                Color c = HSVToRGB(hue, 1f, 1f);
                
                // Glow
                for (int j = 5; j > 0; j--)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(30, c)))
                    {
                        g.FillEllipse(b, ledPos[i].X - j * 2, ledPos[i].Y - j * 2, j * 4, j * 4);
                    }
                }
                
                // LED
                using (SolidBrush b = new SolidBrush(c))
                {
                    g.FillEllipse(b, ledPos[i].X - 3, ledPos[i].Y - 3, 6, 6);
                }
            }
        }
        
        private void DrawReflections(Graphics g, int cx, int cy)
        {
            // Reflet principal
            GraphicsPath reflect = new GraphicsPath();
            reflect.AddBezier(cx - 40, cy - 70, cx - 20, cy - 80, cx + 20, cy - 80, cx + 40, cy - 70);
            reflect.AddLine(cx + 40, cy - 70, cx + 30, cy - 50);
            reflect.AddLine(cx + 30, cy - 50, cx - 30, cy - 50);
            reflect.CloseFigure();
            
            using (LinearGradientBrush gb = new LinearGradientBrush(
                new Point(cx, cy - 80),
                new Point(cx, cy - 50),
                Color.FromArgb(120, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255)))
            {
                g.FillPath(gb, reflect);
            }
            
            reflect.Dispose();
            
            // Reflets secondaires
            using (Pen p = new Pen(Color.FromArgb(80, 255, 255, 255), 2))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawCurve(p, new PointF[] {
                    new PointF(cx - 50, cy - 20),
                    new PointF(cx - 40, cy - 30),
                    new PointF(cx - 30, cy - 25)
                });
            }
        }
        
        private void DrawLogo(Graphics g, int cx, int cy)
        {
            // Logo hexagonal gaming
            PointF[] hexagon = new PointF[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = (float)(Math.PI / 3 * i + _glowPhase * 0.5);
                hexagon[i] = new PointF(
                    cx + (float)Math.Cos(angle) * 20,
                    cy + (float)Math.Sin(angle) * 20
                );
            }
            
            float hue = (_glowPhase * 80) % 360;
            Color logoColor = HSVToRGB(hue, 0.8f, 1f);
            
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, logoColor)))
            {
                g.FillPolygon(b, hexagon);
            }
            
            using (Pen p = new Pen(logoColor, 3))
            {
                g.DrawPolygon(p, hexagon);
            }
            
            // Centre du logo
            using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 255, 255)))
            {
                g.FillEllipse(b, cx - 8, cy - 8, 16, 16);
            }
        }
        
        private Color HSVToRGB(float h, float s, float v)
        {
            int hi = Convert.ToInt32(Math.Floor(h / 60)) % 6;
            float f = h / 60 - (float)Math.Floor(h / 60);
            
            int val = Convert.ToInt32(v * 255);
            int p = Convert.ToInt32(v * (1 - s) * 255);
            int q = Convert.ToInt32(v * (1 - f * s) * 255);
            int t = Convert.ToInt32(v * (1 - (1 - f) * s) * 255);
            
            switch (hi)
            {
                case 0: return Color.FromArgb(val, t, p);
                case 1: return Color.FromArgb(q, val, p);
                case 2: return Color.FromArgb(p, val, t);
                case 3: return Color.FromArgb(p, q, val);
                case 4: return Color.FromArgb(t, p, val);
                default: return Color.FromArgb(val, p, q);
            }
        }
    }
}
