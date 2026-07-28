using System;
using GamingKeypressOverlay.App;

namespace GamingKeypressOverlay
{
    /// <summary>
    /// Application entry point - Win32/GDI version
    /// </summary>
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Run Win32 overlay (ultra-low latency, <1ms)
            Win32App.RunWin32();
        }
    }
}
