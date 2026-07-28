using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GamingKeypressOverlay.Performance
{
    /// <summary>
    /// Performance metrics and telemetry for diagnostics
    /// Tracks input latency, UI latency, dropped frames, and key press rates
    /// </summary>
    public class PerformanceMetrics : IDisposable
    {
        private readonly object _lock = new object();
        private long _inputLatencyTicks = 0;
        private long _uiLatencyTicks = 0;
        private long _lastKeyCount = 0;
        private DateTime _lastMetricsReset = DateTime.Now;
        private int _totalKeysProcessed = 0;
        private int _totalInputEvents = 0;
        private int _totalUIUpdates = 0;
        private StreamWriter _logWriter;
        private bool _disposed = false;
        
        // Alert thresholds
        private const long MAX_INPUT_LATENCY_MS = 10;
        private const long MAX_UI_LATENCY_MS = 16; // ~60 FPS
        private const int MAX_DROPPED_FRAMES_PER_SEC = 5;
        
        public long InputLatencyMs { get; private set; }
        public long UILatencyMs { get; private set; }
        public int DroppedFrames { get; private set; }
        public int KeysPerSecond { get; private set; }
        public int TotalKeysProcessed => _totalKeysProcessed;
        public int TotalInputEvents => _totalInputEvents;
        public int TotalUIUpdates => _totalUIUpdates;
        
        /// <summary>
        /// Record input processing latency
        /// </summary>
        public void RecordInputLatency(long ticks)
        {
            lock (_lock)
            {
                _inputLatencyTicks = ticks;
                InputLatencyMs = (long)(ticks * 1000.0 / Stopwatch.Frequency);
                _totalInputEvents++;
                
                if (InputLatencyMs > MAX_INPUT_LATENCY_MS)
                {
                    LogWarning($"High input latency detected: {InputLatencyMs}ms (threshold: {MAX_INPUT_LATENCY_MS}ms)");
                }
            }
        }
        
        /// <summary>
        /// Record UI update latency
        /// </summary>
        public void RecordUILatency(long ticks)
        {
            lock (_lock)
            {
                _uiLatencyTicks = ticks;
                UILatencyMs = (long)(ticks * 1000.0 / Stopwatch.Frequency);
                _totalUIUpdates++;
                
                if (UILatencyMs > MAX_UI_LATENCY_MS)
                {
                    DroppedFrames++;
                    LogWarning($"High UI latency detected: {UILatencyMs}ms (threshold: {MAX_UI_LATENCY_MS}ms)");
                }
            }
        }
        
        /// <summary>
        /// Record a key press
        /// </summary>
        public void RecordKeyPress()
        {
            lock (_lock)
            {
                _totalKeysProcessed++;
                UpdateKeysPerSecond();
            }
        }
        
        /// <summary>
        /// Update keys per second calculation
        /// </summary>
        private void UpdateKeysPerSecond()
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastMetricsReset).TotalSeconds;
            
            if (elapsed >= 1.0)
            {
                long currentKeyCount = _totalKeysProcessed;
                long keysInPeriod = currentKeyCount - _lastKeyCount;
                KeysPerSecond = (int)(keysInPeriod / elapsed);
                
                _lastKeyCount = currentKeyCount;
                _lastMetricsReset = now;
            }
        }
        
        /// <summary>
        /// Initialize log writer (called once)
        /// </summary>
        private void EnsureLogWriter()
        {
            if (_logWriter != null) return;
            
            lock (_lock)
            {
                if (_logWriter != null) return;
                
                try
                {
                    string logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "GamingKeypressOverlay",
                        "Logs",
                        "metrics.log"
                    );
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    _logWriter = new StreamWriter(logPath, append: true) { AutoFlush = true };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PerformanceMetrics] Failed to create log writer: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Log metrics to file for diagnostics
        /// </summary>
        public void LogMetrics(string filePath = null)
        {
            if (_disposed) return;
            
            lock (_lock)
            {
                if (_disposed) return;
                
                try
                {
                    EnsureLogWriter();
                    
                    if (_logWriter != null)
                    {
                        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                                      $"InputLatency: {InputLatencyMs}ms, " +
                                      $"UILatency: {UILatencyMs}ms, " +
                                      $"DroppedFrames: {DroppedFrames}, " +
                                      $"KeysPerSecond: {KeysPerSecond}, " +
                                      $"TotalKeys: {_totalKeysProcessed}, " +
                                      $"TotalInputEvents: {_totalInputEvents}, " +
                                      $"TotalUIUpdates: {_totalUIUpdates}";
                        
                        _logWriter.WriteLine(logEntry);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PerformanceMetrics] Failed to log metrics: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Check if performance is degraded and needs attention
        /// </summary>
        public bool IsPerformanceDegraded()
        {
            lock (_lock)
            {
                return InputLatencyMs > MAX_INPUT_LATENCY_MS ||
                       UILatencyMs > MAX_UI_LATENCY_MS ||
                       DroppedFrames > MAX_DROPPED_FRAMES_PER_SEC;
            }
        }
        
        /// <summary>
        /// Reset all metrics
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _inputLatencyTicks = 0;
                _uiLatencyTicks = 0;
                DroppedFrames = 0;
                KeysPerSecond = 0;
                _lastKeyCount = 0;
                _lastMetricsReset = DateTime.Now;
                InputLatencyMs = 0;
                UILatencyMs = 0;
            }
        }
        
        private void LogWarning(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[PerformanceMetrics] WARNING: {message}");
        }
        
        /// <summary>
        /// Dispose and close log file
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            
            lock (_lock)
            {
                if (_disposed) return;
                
                try
                {
                    _logWriter?.Flush();
                    _logWriter?.Dispose();
                    _logWriter = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PerformanceMetrics] Error disposing log writer: {ex.Message}");
                }
                
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
        
        /// <summary>
        /// Finalizer (safety net if user forgets Dispose)
        /// </summary>
        ~PerformanceMetrics()
        {
            Dispose();
        }
    }
}
