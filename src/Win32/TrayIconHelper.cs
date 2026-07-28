using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Helper class to create a simple keyboard icon for the system tray
    /// Creates a 16x16 icon programmatically with a simple keyboard design
    /// </summary>
    public static class TrayIconHelper
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        /// <summary>
        /// Creates a simple keyboard icon (16x16) for the system tray
        /// </summary>
        public static Icon CreateKeyboardIcon()
        {
            const int size = 16;
            IntPtr hicon;
            using (var bitmap = new Bitmap(size, size))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                // Clear with transparent background
                graphics.Clear(Color.Transparent);

                // Enable anti-aliasing for smooth edges
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw keyboard outline (rounded rectangle)
                using (var pen = new Pen(Color.White, 1))
                using (var brush = new SolidBrush(Color.FromArgb(180, 70, 130, 180))) // Semi-transparent purple-blue
                {
                    // Main keyboard body
                    var keyboardRect = new Rectangle(1, 3, 14, 10);
                    graphics.FillRectangle(brush, keyboardRect);
                    graphics.DrawRectangle(pen, keyboardRect);

                    // Draw some key outlines (3x3 grid simplified)
                    pen.Color = Color.FromArgb(150, 255, 255, 255);
                    pen.Width = 1;

                    // Top row keys
                    graphics.DrawRectangle(pen, 2, 4, 3, 3);
                    graphics.DrawRectangle(pen, 6, 4, 3, 3);
                    graphics.DrawRectangle(pen, 10, 4, 3, 3);

                    // Middle row keys
                    graphics.DrawRectangle(pen, 2, 7, 3, 3);
                    graphics.DrawRectangle(pen, 6, 7, 3, 3);
                    graphics.DrawRectangle(pen, 10, 7, 3, 3);

                    // Bottom row keys (slightly offset)
                    graphics.DrawRectangle(pen, 3, 10, 3, 3);
                    graphics.DrawRectangle(pen, 7, 10, 3, 3);
                }
                hicon = bitmap.GetHicon();
            }

            return Icon.FromHandle(hicon);
        }

        /// <summary>
        /// Creates an alternative mouse icon for the system tray
        /// </summary>
        public static Icon CreateMouseIcon()
        {
            const int size = 16;
            IntPtr hicon;
            using (var bitmap = new Bitmap(size, size))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (var pen = new Pen(Color.White, 1))
                using (var brush = new SolidBrush(Color.FromArgb(180, 130, 180, 70))) // Semi-transparent green
                {
                    // Mouse body (oval shape)
                    var mouseRect = new Rectangle(3, 2, 10, 12);
                    graphics.FillEllipse(brush, mouseRect);
                    graphics.DrawEllipse(pen, mouseRect);

                    // Mouse buttons
                    pen.Color = Color.FromArgb(150, 255, 255, 255);
                    graphics.DrawLine(pen, 8, 2, 8, 6); // Left button line
                    graphics.DrawLine(pen, 8, 10, 8, 14); // Right button line

                    // Scroll wheel
                    graphics.DrawRectangle(pen, 7, 7, 2, 2);
                }
                hicon = bitmap.GetHicon();
            }

            return Icon.FromHandle(hicon);
        }

        /// <summary>
        /// Creates a gaming controller icon for the system tray
        /// </summary>
        public static Icon CreateGamepadIcon()
        {
            const int size = 16;
            IntPtr hicon;
            using (var bitmap = new Bitmap(size, size))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (var pen = new Pen(Color.White, 1))
                using (var brush = new SolidBrush(Color.FromArgb(180, 180, 70, 130))) // Semi-transparent magenta
                {
                    // Controller body
                    var bodyRect = new Rectangle(2, 4, 12, 8);
                    graphics.FillRectangle(brush, bodyRect);
                    graphics.DrawRectangle(pen, bodyRect);

                    // Left analog stick
                    graphics.FillEllipse(brush, 3, 5, 3, 3);
                    graphics.DrawEllipse(pen, 3, 5, 3, 3);

                    // Right analog stick
                    graphics.FillEllipse(brush, 10, 5, 3, 3);
                    graphics.DrawEllipse(pen, 10, 5, 3, 3);

                    // D-pad (simplified)
                    pen.Color = Color.FromArgb(150, 255, 255, 255);
                    graphics.DrawLine(pen, 6, 4, 6, 2); // Up
                    graphics.DrawLine(pen, 6, 12, 6, 14); // Down
                    graphics.DrawLine(pen, 4, 6, 2, 6); // Left
                    graphics.DrawLine(pen, 12, 6, 14, 6); // Right
                }
                hicon = bitmap.GetHicon();
            }

            return Icon.FromHandle(hicon);
        }

        /// <summary>
        /// Saves the created icon to a file (optional utility)
        /// </summary>
        public static void SaveIconToFile(string filePath, Icon icon)
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                icon.Save(stream);
            }
        }
    }
}