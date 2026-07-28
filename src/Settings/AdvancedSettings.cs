using System;

namespace GamingKeypressOverlay.Settings
{
    /// <summary>
    /// Advanced configuration settings with validation
    /// </summary>
    public class AdvancedSettings
    {
        private int _flashDurationMs = 30;
        private int _latchDurationMs = 50;
        private int _eventBufferSize = 32;
        private bool _enablePolling = true;
        private int _pollingIntervalMs = 1;
        private System.Threading.ThreadPriority _inputThreadPriority = System.Threading.ThreadPriority.Highest;
        private bool _enableCpuAffinity = false; // OFF by default - risky on modern CPUs
        private bool _enableProcessAffinity = false; // OFF by default - risky on modern CPUs
        
        public int FlashDurationMs
        {
            get => _flashDurationMs;
            set
            {
                if (value < 0 || value > 1000)
                    throw new ArgumentOutOfRangeException(nameof(FlashDurationMs), "Must be between 0 and 1000ms");
                _flashDurationMs = value;
            }
        }
        
        public int LatchDurationMs
        {
            get => _latchDurationMs;
            set
            {
                if (value < 0 || value > 1000)
                    throw new ArgumentOutOfRangeException(nameof(LatchDurationMs), "Must be between 0 and 1000ms");
                _latchDurationMs = value;
            }
        }
        
        public int EventBufferSize
        {
            get => _eventBufferSize;
            set
            {
                if (value < 8 || value > 256)
                    throw new ArgumentOutOfRangeException(nameof(EventBufferSize), "Must be between 8 and 256");
                _eventBufferSize = value;
            }
        }
        
        public bool EnablePolling
        {
            get => _enablePolling;
            set => _enablePolling = value;
        }
        
        public int PollingIntervalMs
        {
            get => _pollingIntervalMs;
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentOutOfRangeException(nameof(PollingIntervalMs), "Must be between 0 and 100ms");
                _pollingIntervalMs = value;
            }
        }
        
        public System.Threading.ThreadPriority InputThreadPriority
        {
            get => _inputThreadPriority;
            set
            {
                if (value != System.Threading.ThreadPriority.Lowest &&
                    value != System.Threading.ThreadPriority.BelowNormal &&
                    value != System.Threading.ThreadPriority.Normal &&
                    value != System.Threading.ThreadPriority.AboveNormal &&
                    value != System.Threading.ThreadPriority.Highest)
                {
                    throw new ArgumentException("Invalid thread priority", nameof(InputThreadPriority));
                }
                _inputThreadPriority = value;
            }
        }
        
        /// <summary>
        /// Enable CPU affinity for input thread (RISKY on modern CPUs with P/E cores)
        /// WARNING: May cause worse latency if pinned to E-core or parked core
        /// Default: false (disabled for safety)
        /// </summary>
        public bool EnableCpuAffinity
        {
            get => _enableCpuAffinity;
            set => _enableCpuAffinity = value;
        }
        
        /// <summary>
        /// Enable process-level CPU affinity (RISKY on modern CPUs)
        /// WARNING: May cause worse performance on heterogeneous core systems
        /// Default: false (disabled for safety)
        /// </summary>
        public bool EnableProcessAffinity
        {
            get => _enableProcessAffinity;
            set => _enableProcessAffinity = value;
        }
        
        /// <summary>
        /// Validate all settings and return error message if invalid
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;
            
            if (_flashDurationMs < 0 || _flashDurationMs > 1000)
            {
                error = $"FlashDurationMs must be between 0 and 1000ms (current: {_flashDurationMs})";
                return false;
            }
            
            if (_latchDurationMs < 0 || _latchDurationMs > 1000)
            {
                error = $"LatchDurationMs must be between 0 and 1000ms (current: {_latchDurationMs})";
                return false;
            }
            
            if (_eventBufferSize < 8 || _eventBufferSize > 256)
            {
                error = $"EventBufferSize must be between 8 and 256 (current: {_eventBufferSize})";
                return false;
            }
            
            if (_pollingIntervalMs < 0 || _pollingIntervalMs > 100)
            {
                error = $"PollingIntervalMs must be between 0 and 100ms (current: {_pollingIntervalMs})";
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Create default settings optimized for gaming
        /// </summary>
        public static AdvancedSettings CreateGamingDefaults()
        {
            return new AdvancedSettings
            {
                FlashDurationMs = 30,
                LatchDurationMs = 50,
                EventBufferSize = 32,
                EnablePolling = false, // Disabled by default - only use as fallback when Raw Input fails
                PollingIntervalMs = 1,
                InputThreadPriority = System.Threading.ThreadPriority.Highest,
                EnableCpuAffinity = false, // Disabled by default - risky on modern CPUs
                EnableProcessAffinity = false // Disabled by default - risky on modern CPUs
            };
        }
        
        /// <summary>
        /// Create default settings optimized for desktop use (lower CPU)
        /// </summary>
        public static AdvancedSettings CreateDesktopDefaults()
        {
            return new AdvancedSettings
            {
                FlashDurationMs = 50,
                LatchDurationMs = 70,
                EventBufferSize = 16,
                EnablePolling = false,
                PollingIntervalMs = 10,
                InputThreadPriority = System.Threading.ThreadPriority.AboveNormal,
                EnableCpuAffinity = false,
                EnableProcessAffinity = false
            };
        }
    }
}
