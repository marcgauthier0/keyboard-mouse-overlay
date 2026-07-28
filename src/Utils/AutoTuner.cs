using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GamingKeypressOverlay.Settings;

namespace GamingKeypressOverlay.Utils
{
    /// <summary>
    /// Auto-tuning based on system capabilities
    /// Detects laptop vs desktop, CPU cores, RAM, and adjusts settings accordingly
    /// </summary>
    public static class AutoTuner
    {
        [DllImport("kernel32.dll")]
        private static extern int GetSystemPowerStatus(ref SYSTEM_POWER_STATUS powerStatus);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }
        
        /// <summary>
        /// Detect if system is a laptop
        /// </summary>
        public static bool IsLaptop()
        {
            try
            {
                var powerStatus = new SYSTEM_POWER_STATUS();
                if (GetSystemPowerStatus(ref powerStatus) != 0)
                {
                    // If battery is present, likely a laptop
                    return powerStatus.BatteryFlag != 255; // 255 = no battery
                }
            }
            catch
            {
                // Fallback: WMI detection removed to avoid dependency
                // If power status detection fails, assume desktop
            }
            
            return false;
        }
        
        /// <summary>
        /// Get total system RAM in GB
        /// </summary>
        public static long GetTotalRAM()
        {
            // Avoid PerformanceCounter dependency (not in net8.0-windows by default).
            // Use GlobalMemoryStatusEx via P/Invoke for accurate RAM if needed.
            return 0; // Unknown - caller can use 0 as fallback
        }
        
        /// <summary>
        /// Check if anti-cheat software is active
        /// </summary>
        public static bool IsAntiCheatActive()
        {
            try
            {
                var antiCheatProcesses = new[]
                {
                    "EasyAntiCheat",
                    "BattlEye",
                    "BEService",
                    "EasyAntiCheat_EOS",
                    "EasyAntiCheat_launcher"
                };
                
                foreach (var processName in antiCheatProcesses)
                {
                    var processes = Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return false;
        }
        
        /// <summary>
        /// Tune settings based on system capabilities
        /// </summary>
        public static AdvancedSettings TuneBasedOnSystem(AdvancedSettings currentSettings)
        {
            var tunedSettings = new AdvancedSettings
            {
                FlashDurationMs = currentSettings.FlashDurationMs,
                LatchDurationMs = currentSettings.LatchDurationMs,
                EventBufferSize = currentSettings.EventBufferSize,
                EnablePolling = currentSettings.EnablePolling,
                PollingIntervalMs = currentSettings.PollingIntervalMs,
                InputThreadPriority = currentSettings.InputThreadPriority
            };
            
            int cores = Environment.ProcessorCount;
            long ram = GetTotalRAM();
            bool isLaptop = IsLaptop();
            
            // Adjust based on CPU cores
            if (cores < 4)
            {
                // Low-end system: disable polling, reduce buffer size
                tunedSettings.EnablePolling = false;
                tunedSettings.EventBufferSize = Math.Min(tunedSettings.EventBufferSize, 16);
                tunedSettings.PollingIntervalMs = 10;
                System.Diagnostics.Debug.WriteLine("[AutoTuner] Low CPU core count detected, reducing performance settings");
            }
            else if (cores >= 8)
            {
                // High-end system: enable all optimizations
                tunedSettings.EnablePolling = true;
                tunedSettings.EventBufferSize = Math.Max(tunedSettings.EventBufferSize, 32);
                tunedSettings.PollingIntervalMs = 1;
                System.Diagnostics.Debug.WriteLine("[AutoTuner] High CPU core count detected, enabling all optimizations");
            }
            
            // Adjust based on RAM
            if (ram > 0 && ram < 8)
            {
                // Low RAM: reduce buffer size
                tunedSettings.EventBufferSize = Math.Min(tunedSettings.EventBufferSize, 16);
                System.Diagnostics.Debug.WriteLine("[AutoTuner] Low RAM detected, reducing buffer size");
            }
            
            // Adjust based on laptop/desktop
            if (isLaptop)
            {
                // Laptop: reduce priority to save battery
                tunedSettings.InputThreadPriority = System.Threading.ThreadPriority.AboveNormal;
                tunedSettings.EnablePolling = false; // Disable polling on laptop to save battery
                System.Diagnostics.Debug.WriteLine("[AutoTuner] Laptop detected, reducing priority and disabling polling");
            }
            
            // Warn if anti-cheat is active
            if (IsAntiCheatActive())
            {
                System.Diagnostics.Debug.WriteLine("[AutoTuner] WARNING: Anti-cheat software detected. Overlay may be blocked.");
            }
            
            return tunedSettings;
        }
        
        /// <summary>
        /// Get system information summary
        /// </summary>
        public static string GetSystemInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"CPU Cores: {Environment.ProcessorCount}");
            info.AppendLine($"RAM: {GetTotalRAM()} GB");
            info.AppendLine($"Is Laptop: {IsLaptop()}");
            info.AppendLine($"Anti-Cheat Active: {IsAntiCheatActive()}");
            info.AppendLine($"OS: {Environment.OSVersion}");
            info.AppendLine($"CLR: {Environment.Version}");
            
            return info.ToString();
        }
    }
}
