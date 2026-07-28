using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using GamingKeypressOverlay.Localization;
using GamingKeypressOverlay.Performance;

namespace GamingKeypressOverlay.Diagnostics
{
    /// <summary>
    /// Crash reporting and error logging for production diagnostics
    /// </summary>
    public static class CrashReporter
    {
        private static string _logDirectory;
        private static bool _initialized = false;
        
        /// <summary>
        /// Check if CrashReporter is initialized
        /// </summary>
        public static bool IsInitialized => _initialized;
        
        // Win32 MessageBox
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);
        
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONERROR = 0x00000010;
        private const uint MB_ICONWARNING = 0x00000030;
        private const uint MB_ICONINFORMATION = 0x00000040;
        
        /// <summary>
        /// Initialize crash reporting
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GamingKeypressOverlay",
                "Logs"
            );
            
            Directory.CreateDirectory(_logDirectory);
            
            // Register global exception handlers
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            
            _initialized = true;
            LogInfo("CrashReporter initialized");
        }
        
        /// <summary>
        /// Handle unhandled exceptions from any thread
        /// </summary>
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                DumpCrashReport(exception, "UnhandledException", null, null);
                
                // Show user-friendly error message using Win32 MessageBox
                MessageBox(
                    IntPtr.Zero,
                    UiText.Get(
                        $"An unexpected error occurred. A crash report has been saved to:\n{_logDirectory}\n\nError: {exception.Message}",
                        $"Une erreur inattendue est survenue. Un rapport a été enregistré dans :\n{_logDirectory}\n\nErreur : {exception.Message}"),
                    UiText.Get("Application Error", "Erreur de l’application"),
                    MB_OK | MB_ICONERROR
                );
            }
        }
        
        /// <summary>
        /// Dump crash report to file
        /// </summary>
        public static void DumpCrashReport(
            Exception exception,
            string context,
            PerformanceMetrics metrics,
            object inputState)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"crash_{timestamp}.txt";
                var filePath = Path.Combine(_logDirectory, fileName);
                
                var report = new StringBuilder();
                report.AppendLine("=== CRASH REPORT ===");
                report.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                report.AppendLine($"Context: {context}");
                report.AppendLine();
                
                report.AppendLine("=== EXCEPTION ===");
                report.AppendLine($"Type: {exception.GetType().FullName}");
                report.AppendLine($"Message: {exception.Message}");
                report.AppendLine($"Stack Trace:\n{exception.StackTrace}");
                
                if (exception.InnerException != null)
                {
                    report.AppendLine();
                    report.AppendLine("=== INNER EXCEPTION ===");
                    report.AppendLine($"Type: {exception.InnerException.GetType().FullName}");
                    report.AppendLine($"Message: {exception.InnerException.Message}");
                    report.AppendLine($"Stack Trace:\n{exception.InnerException.StackTrace}");
                }
                
                report.AppendLine();
                report.AppendLine("=== SYSTEM INFO ===");
                report.AppendLine($"OS: {Environment.OSVersion}");
                report.AppendLine($"CLR: {Environment.Version}");
                report.AppendLine($"Processor Count: {Environment.ProcessorCount}");
                report.AppendLine($"Working Set: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");
                
                if (metrics != null)
                {
                    report.AppendLine();
                    report.AppendLine("=== PERFORMANCE METRICS ===");
                    report.AppendLine($"Input Latency: {metrics.InputLatencyMs}ms");
                    report.AppendLine($"UI Latency: {metrics.UILatencyMs}ms");
                    report.AppendLine($"Dropped Frames: {metrics.DroppedFrames}");
                    report.AppendLine($"Keys Per Second: {metrics.KeysPerSecond}");
                    report.AppendLine($"Total Keys Processed: {metrics.TotalKeysProcessed}");
                    report.AppendLine($"Total Input Events: {metrics.TotalInputEvents}");
                    report.AppendLine($"Total UI Updates: {metrics.TotalUIUpdates}");
                }
                
                report.AppendLine();
                report.AppendLine("=== END OF REPORT ===");
                
                File.WriteAllText(filePath, report.ToString());
                
                System.Diagnostics.Debug.WriteLine($"[CrashReporter] Crash report saved to: {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CrashReporter] Failed to save crash report: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Log informational message
        /// </summary>
        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }
        
        /// <summary>
        /// Log warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            Log("WARNING", message);
        }
        
        /// <summary>
        /// Log error message
        /// </summary>
        public static void LogError(string message)
        {
            Log("ERROR", message);
        }
        
        /// <summary>
        /// Log critical error message
        /// </summary>
        public static void LogCritical(string message)
        {
            Log("CRITICAL", message);
        }
        
        /// <summary>
        /// Internal logging method
        /// </summary>
        private static void Log(string level, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_logDirectory))
                {
                    _logDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "GamingKeypressOverlay",
                        "Logs"
                    );
                    Directory.CreateDirectory(_logDirectory);
                }
                
                var logFile = Path.Combine(_logDirectory, $"app_{DateTime.Now:yyyy-MM-dd}.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}\n";
                
                File.AppendAllText(logFile, logEntry);
                System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
            }
            catch
            {
                // Silently fail if logging fails (don't crash the app)
            }
        }
    }
}
