using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using GamingKeypressOverlay.Input;
using GamingKeypressOverlay.Win32;
using GamingKeypressOverlay.Overlay;
using GamingKeypressOverlay.Settings;
using GamingKeypressOverlay.Diagnostics;
using GamingKeypressOverlay.Localization;

namespace GamingKeypressOverlay.App
{
    /// <summary>
    /// Win32 application entry point (alternative to XAML/WPF)
    /// Ultra-low latency overlay using Win32 + GDI rendering
    /// </summary>
    public class Win32App
    {
        // Win32 MessageBox
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);
        
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONERROR = 0x00000010;
        
        private unsafe InputStateManager _inputStateManager;
        private unsafe RawInputThreadManager _rawInputThreadManager;
        private Win32OverlayWindow _overlayWindow;
        private bool _disposed = false;
        
        public static void RunWin32()
        {
            try
            {
                // Initialize crash reporting FIRST, before anything else
                // This must be done at the very start to catch any errors
                CrashReporter.Initialize();
                CrashReporter.LogInfo("Application entry point - RunWin32()");
                
                var app = new Win32App();
                app.Run();
            }
            catch (Exception ex)
            {
                // If CrashReporter failed to initialize, try to log manually
                try
                {
                    string logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "GamingKeypressOverlay",
                        "Logs"
                    );
                    Directory.CreateDirectory(logDir);
                    string logFile = Path.Combine(logDir, $"startup_error_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                    File.WriteAllText(logFile, $"Startup Error:\n{ex}\n\nStack Trace:\n{ex.StackTrace}");
                }
                catch { }
                
                // Show error to user
                MessageBox(
                    IntPtr.Zero,
                    UiText.Get(
                        $"Failed to start application:\n\n{ex.Message}\n\nCheck logs in:\n%LocalAppData%\\GamingKeypressOverlay\\Logs",
                        $"Impossible de démarrer l’application :\n\n{ex.Message}\n\nConsultez les journaux dans :\n%LocalAppData%\\GamingKeypressOverlay\\Logs"),
                    UiText.Get("Keyboard & Mouse Overlay - Startup Error", "Keyboard & Mouse Overlay - Erreur de démarrage"),
                    MB_OK | MB_ICONERROR
                );
            }
        }
        
        public unsafe void Run()
        {
            try
            {
                // CrashReporter should already be initialized in RunWin32()
                // But ensure it's initialized here too as a safety measure
                try
                {
                    if (!CrashReporter.IsInitialized)
                    {
                        CrashReporter.Initialize();
                    }
                }
                catch (Exception initEx)
                {
                    // If CrashReporter initialization fails, log manually
                    try
                    {
                        string logDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "GamingKeypressOverlay",
                            "Logs"
                        );
                        Directory.CreateDirectory(logDir);
                        string logFile = Path.Combine(logDir, $"crashreporter_init_error_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                        File.WriteAllText(logFile, $"CrashReporter initialization failed:\n{initEx}\n\nStack Trace:\n{initEx.StackTrace}");
                    }
                    catch { }
                }
                
                CrashReporter.LogInfo("Win32App.Run() - Starting application...");
                
                // Set process priority
                Process currentProcess = Process.GetCurrentProcess();
                currentProcess.PriorityClass = ProcessPriorityClass.High;
                CrashReporter.LogInfo($"Process priority set to: {currentProcess.PriorityClass}");
                
                // Create InputStateManager
                _inputStateManager = new InputStateManager();
                
                // Create RawInputThreadManager
                _rawInputThreadManager = new RawInputThreadManager(
                    enableContinuousPolling: false,
                    useHighestPriority: true,
                    enableSafetyChecks: false,
                    enableCpuAffinity: false,
                    externalInputState: _inputStateManager.State
                );
                
                // Wait for Raw Input to initialize
                int waitCount = 0;
                while (!_rawInputThreadManager.RawInputInitialized && waitCount < 200)
                {
                    Thread.Sleep(10);
                    waitCount++;
                }
                
                if (!_rawInputThreadManager.RawInputInitialized)
                {
                    throw new InvalidOperationException("Failed to initialize Raw Input");
                }
                
                // Load saved settings
                var settings = SettingsManager.LoadSettings();
                
                // Calculate initial size based on default layout (will be adjusted after settings are applied)
                // Use default size if saved size is invalid or too small
                int initialWidth = (settings.WindowWidth > 0 && settings.WindowWidth < 10000) 
                    ? (int)settings.WindowWidth 
                    : 1400;
                int initialHeight = (settings.WindowHeight > 0 && settings.WindowHeight < 10000) 
                    ? (int)settings.WindowHeight 
                    : 600;
                
                // Create Win32 window (normal window, not overlay)
                _overlayWindow = new Win32OverlayWindow(
                    _inputStateManager.State,
                    width: initialWidth,
                    height: initialHeight
                );

                // Start hidden - user controls visibility via tray icon
                // _overlayWindow.Show(); // Commented out - overlay starts hidden
                
                // Apply saved settings to renderer
                if (_overlayWindow.Renderer != null)
                {
                    // Apply the single user-defined color palette.
                    _overlayWindow.Renderer.SetTheme(StyleManager.GetCustomTheme(settings));
                    
                    // Apply keyboard layout type
                    System.Diagnostics.Debug.WriteLine($"Win32App: Loading KeyboardLayoutType from settings = '{settings.KeyboardLayoutType}'");
                    if (!string.IsNullOrEmpty(settings.KeyboardLayoutType) && Enum.TryParse<KeyboardLayoutType>(settings.KeyboardLayoutType, ignoreCase: true, out var layoutType))
                    {
                        _overlayWindow.Renderer.SetLayoutType(layoutType);
                        System.Diagnostics.Debug.WriteLine($"Win32App: Applied KeyboardLayoutType = {layoutType}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Win32App: Could not parse KeyboardLayoutType '{settings.KeyboardLayoutType}', using default = {KeyboardLayoutType.QWERTY}");
                        _overlayWindow.Renderer.SetLayoutType(KeyboardLayoutType.QWERTY);
                    }
                    
                    // Apply game config
                    System.Diagnostics.Debug.WriteLine($"Win32App: Loading GameConfig from settings = '{settings.GameConfig}'");
                    if (!string.IsNullOrEmpty(settings.GameConfig) && Enum.TryParse<GameConfig>(settings.GameConfig, ignoreCase: true, out var gameConfig))
                    {
                        _overlayWindow.Renderer.SetGameConfig(gameConfig);
                        System.Diagnostics.Debug.WriteLine($"Win32App: Applied GameConfig = {gameConfig}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Win32App: Could not parse GameConfig '{settings.GameConfig}', using default = {GameConfig.General}");
                        _overlayWindow.Renderer.SetGameConfig(GameConfig.General);
                    }
                    
                    // Apply mouse position
                    if (settings.MousePosition == "None")
                    {
                        if (_overlayWindow.Renderer is Win32.Direct2DRenderer direct2DRenderer)
                        {
                            direct2DRenderer.SetMouseVisible(false);
                        }
                    }
                    else
                    {
                        bool mouseOnRight = settings.MousePosition == "Right";
                        _overlayWindow.Renderer.SetMousePosition(mouseOnRight);
                    }
                    
                    // Personalization features are available to everyone.
                    if (settings.UseAnimatedBackground)
                    {
                        _overlayWindow.Renderer.SetAnimatedBackground(true);
                    }
                    
                    if (!string.IsNullOrWhiteSpace(settings.CustomLogoPath) && 
                        System.IO.File.Exists(settings.CustomLogoPath))
                    {
                        _overlayWindow.Renderer.SetCustomLogo(settings.CustomLogoPath);
                    }
                    
                    // Wait a bit for renderer to update layout
                    Thread.Sleep(50);
                    
                    // Resize window to fit content after applying all settings
                    _overlayWindow.ResizeWindowToFitContent();
                }
                
                // Message loop (keep alive until window is closed)
                while (!_disposed && _overlayWindow != null && !_overlayWindow.IsDisposed)
                {
                    Thread.Sleep(100);
                }
                
                // Ensure clean exit
                System.Diagnostics.Debug.WriteLine("Win32App: Exiting...");
            }
            catch (Exception ex)
            {
                // Log the crash with full details
                CrashReporter.DumpCrashReport(ex, "Win32App.Run", null, null);
                CrashReporter.LogError($"Application crashed: {ex.Message}");
                CrashReporter.LogError($"Stack trace: {ex.StackTrace}");
                
                // Show user-friendly error message
                string logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GamingKeypressOverlay", "Logs");
                string errorMessage = UiText.Get(
                    $"An error occurred while starting the application.\n\nError: {ex.Message}\n\n" +
                    $"A crash report has been saved to:\n{logPath}\n\nPlease check the crash report for more details.",
                    $"Une erreur est survenue au démarrage de l’application.\n\nErreur : {ex.Message}\n\n" +
                    $"Un rapport a été enregistré dans :\n{logPath}\n\nConsultez-le pour plus de détails.");
                
                MessageBox(IntPtr.Zero, errorMessage,
                    UiText.Get("Keyboard & Mouse Overlay - Error", "Keyboard & Mouse Overlay - Erreur"),
                    MB_OK | MB_ICONERROR);
            }
            finally
            {
                Dispose();
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            System.Diagnostics.Debug.WriteLine("Win32App: Disposing resources...");
            
            _overlayWindow?.Dispose();
            _rawInputThreadManager?.Dispose();
            _inputStateManager?.Dispose();
            
            // Force exit to ensure clean termination
            Environment.Exit(0);
        }
    }
}
