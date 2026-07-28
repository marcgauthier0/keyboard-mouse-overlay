using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;

namespace GamingKeypressOverlay.Input
{
    /// <summary>
    /// Simple Point struct to replace System.Windows.Point (no WPF dependency)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public double X { get; set; }
        public double Y { get; set; }
        
        public Point(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
    
    /// <summary>
    /// Thread-safe input state cache (STATE model instead of EVENTS).
    /// This is the core improvement: we track current state, not replay events.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct InputState
    {
        // Fixed-size array for all 256 possible virtual keys
        // Using fixed array for zero-allocation, cache-friendly access
        private fixed bool _keys[256];
        
        // CRITICAL: Timestamp for each key (when it was last pressed)
        // Used to filter ghost presses in AAA games (keys that appear pressed but are actually released)
        // If a key's timestamp is >50ms old, it's considered stale and won't be displayed
        private fixed long _keyTimestamps[256];
        
        // VISUAL LATCH: Minimum display duration for each key (when it was last pressed)
        // This ensures keys pressed <16ms are visible for at least MIN_VISUAL_MS (50-70ms)
        // Even if released immediately, the key stays lit until latch expires
        private fixed long _keyLatchTimestamps[256];
        
        // EVENT BUFFER: Circular buffer to capture ALL key presses, even <16ms
        // This ensures rapid taps (Space, C) are NEVER lost, even if pressed during <1 frame
        // Critical for combos like Shift+W+C where C is pressed very quickly
        private const int EVENT_BUFFER_SIZE = 32;
        private fixed byte _eventBuffer[EVENT_BUFFER_SIZE];
        private int _eventBufferHead; // Write position (circular buffer)
        
        // Mouse buttons state (0=Left, 1=Right, 2=Middle, 3=X1, 4=X2)
        private fixed bool _mouseButtons[5];
        
        // Last 2 keys pressed (for "Last Input" display)
        public byte LastKey;
        public byte SecondLastKey;
        
        // Mouse position
        public Point MousePosition;
        
        // Timestamp of last input (for freshness tracking)
        // Uses Stopwatch.GetTimestamp() for high-resolution timing
        public long LastInputTimestamp;
        
        // Wheel delta (accumulated, reset after read)
        public int WheelDelta;
        
        /// <summary>
        /// Get key state (thread-safe read via Volatile)
        /// </summary>
        public bool GetKey(byte vkey)
        {
            // vkey is byte, so max is 255, but check for safety
            if (vkey > 255) return false;
            fixed (bool* keys = _keys)
            {
                return Volatile.Read(ref keys[vkey]);
            }
        }
        
        /// <summary>
        /// Get key timestamp (when it was last pressed)
        /// </summary>
        public long GetKeyTimestamp(byte vkey)
        {
            if (vkey > 255) return 0;
            fixed (long* timestamps = _keyTimestamps)
            {
                return Volatile.Read(ref timestamps[vkey]);
            }
        }
        
        /// <summary>
        /// Get key latch timestamp (minimum display duration)
        /// Returns 0 if no latch is active
        /// </summary>
        public long GetKeyLatchTimestamp(byte vkey)
        {
            if (vkey > 255) return 0;
            fixed (long* latches = _keyLatchTimestamps)
            {
                return Volatile.Read(ref latches[vkey]);
            }
        }
        
        /// <summary>
        /// Set key state (thread-safe write)
        /// CRITICAL: Updates timestamp when key is pressed to filter ghost inputs
        /// FIX: Added comprehensive error handling for production
        /// </summary>
        public void SetKey(byte vkey, bool pressed)
        {
            try
            {
                // SAFETY CHECK: Validate vkey range
                if (vkey > 255)
                {
                    System.Diagnostics.Debug.WriteLine($"[InputState.SetKey] WARNING: Invalid vkey {vkey} (max 255)");
                    return;
                }
                
                long currentTimestamp = Stopwatch.GetTimestamp();
                
                fixed (bool* keys = _keys)
                fixed (long* timestamps = _keyTimestamps)
                fixed (long* latches = _keyLatchTimestamps)
                {
                    // FIX: Validate pointers before use
                    if (keys == null || timestamps == null || latches == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[InputState.SetKey] ERROR: Null pointer detected");
                        return;
                    }
                    
                    Volatile.Write(ref keys[vkey], pressed);
                
                // CRITICAL: Update timestamp when key is pressed
                // Keep timestamp even after release for flash system (touches ultra-rapides <16ms)
                // The flash system will check if timestamp is recent (<100ms) AND key is not pressed
                // Timestamp will be naturally cleaned by flash system after 100ms
                if (pressed)
                {
                    Volatile.Write(ref timestamps[vkey], currentTimestamp);
                    
                    // EVENT BUFFER: Add to circular buffer on EVERY press
                    // This ensures rapid taps are NEVER lost, even if <16ms duration
                    // Critical for combos like Shift+W+C where C is pressed very quickly
                    // FIX: Calculate nextHead BEFORE writing to prevent race condition
                    fixed (byte* eventBuffer = _eventBuffer)
                    fixed (int* eventBufferHead = &_eventBufferHead)
                    {
                        int head = Volatile.Read(ref *eventBufferHead);
                        int nextHead = (head + 1) % EVENT_BUFFER_SIZE;
                        Volatile.Write(ref eventBuffer[head], vkey);
                        Volatile.Write(ref *eventBufferHead, nextHead); // Atomic update
                    }
                    
                    // VISUAL LATCH: Set latch timestamp to guarantee minimum display duration (50ms)
                    // This ensures keys pressed <16ms are visible for at least 50ms
                    // CRITICAL: Only set latch if not already active (prevent accumulation)
                    // If latch is already active and not expired, don't reset it
                    // This prevents keys from staying lit too long with rapid taps
                    long existingLatch = Volatile.Read(ref latches[vkey]);
                    if (existingLatch == 0)
                    {
                        // No active latch - create new one
                        Volatile.Write(ref latches[vkey], currentTimestamp);
                    }
                    else
                    {
                        // Check if existing latch is expired
                        long latchAge = currentTimestamp - existingLatch;
                        long minVisualTicks = Stopwatch.Frequency / 20; // 50ms
                        if (latchAge < 0 || latchAge > minVisualTicks)
                        {
                            // Latch expired or invalid - create new one
                            Volatile.Write(ref latches[vkey], currentTimestamp);
                        }
                        // Otherwise keep existing latch (prevents accumulation from rapid taps)
                    }
                }
                // NOTE: We DON'T clear timestamp on release - this allows flash system to work
                // NOTE: We DON'T clear latch on release - latch expires naturally after MIN_VISUAL_MS
                // Flash system will check: !pressed && timestamp > 0 && timestamp recent (<100ms)
                // Latch system will check: latch > 0 && latch not expired (<MIN_VISUAL_MS)
            }
            
            if (pressed)
            {
                // Always shift: move LastKey to SecondLastKey, then update LastKey
                // This allows same key twice (e.g., "W → W")
                SecondLastKey = LastKey;
                LastKey = vkey;
                LastInputTimestamp = currentTimestamp;
            }
            else
            {
                // CRITICAL FIX: Clear LastKey/SecondLastKey if releasing them
                // This prevents LastKey from keeping a released key forever
                // Without this, if user presses TAB then releases it, TAB stays in LastKey
                // and the visual display continues to show it as pressed
                if (LastKey == vkey)
                {
                    LastKey = 0;
                }
                if (SecondLastKey == vkey)
                {
                    SecondLastKey = 0;
                }
            }
            }
            catch (Exception ex)
            {
                // FIX: Fail gracefully instead of crashing
                System.Diagnostics.Debug.WriteLine($"[InputState.SetKey] CRITICAL ERROR: {ex.Message}");
                // Don't rethrow - continue processing other keys
            }
        }
        
        /// <summary>
        /// Get mouse button state
        /// </summary>
        public bool GetMouseButton(int button)
        {
            if (button < 0 || button >= 5) return false;
            fixed (bool* buttons = _mouseButtons)
            {
                return Volatile.Read(ref buttons[button]);
            }
        }
        
        /// <summary>
        /// Set mouse button state
        /// </summary>
        public void SetMouseButton(int button, bool pressed)
        {
            if (button < 0 || button >= 5) return;
            fixed (bool* buttons = _mouseButtons)
            {
                Volatile.Write(ref buttons[button], pressed);
            }
            
            if (pressed)
            {
                LastInputTimestamp = Stopwatch.GetTimestamp();
            }
        }
        
        /// <summary>
        /// Update mouse position (thread-safe)
        /// </summary>
        public void UpdateMousePosition(Point position)
        {
            // Use Interlocked for double (split into two ints for atomicity)
            // For simplicity, we'll use a lock-free approach with timestamp
            MousePosition = position;
            LastInputTimestamp = Stopwatch.GetTimestamp();
        }
        
        /// <summary>
        /// Add wheel delta (accumulated)
        /// FIX: Prevent overflow with clamping
        /// </summary>
        public void AddWheelDelta(int delta)
        {
            // FIX: Prevent overflow from rapid scrolling
            // Clamp to reasonable range (-10000 to 10000)
            int current = Volatile.Read(ref WheelDelta);
            int newValue = current + delta;
            
            // Clamp to prevent overflow
            const int MAX_WHEEL_DELTA = 10000;
            const int MIN_WHEEL_DELTA = -10000;
            
            if (newValue > MAX_WHEEL_DELTA)
                newValue = MAX_WHEEL_DELTA;
            else if (newValue < MIN_WHEEL_DELTA)
                newValue = MIN_WHEEL_DELTA;
            
            Volatile.Write(ref WheelDelta, newValue);
            LastInputTimestamp = Stopwatch.GetTimestamp();
        }
        
        /// <summary>
        /// Get and reset wheel delta (atomic read-and-clear)
        /// </summary>
        public int GetAndResetWheelDelta()
        {
            int delta = Interlocked.Exchange(ref WheelDelta, 0);
            if (delta != 0)
            {
                System.Diagnostics.Debug.WriteLine($"[SNAPSHOT] GetAndResetWheelDelta: returning {delta}, reset to 0");
            }
            return delta;
        }
        
        /// <summary>
        /// Clean old timestamps (>maxAge) for keys that are not currently pressed
        /// Also clean expired latches (>minVisualTicks)
        /// This prevents keys from staying lit forever after release
        /// FIX: Double-check key state AFTER timestamp comparison to prevent race condition
        /// </summary>
        public unsafe void CleanOldTimestamps(long currentTime, long maxAgeTicks, long minVisualTicks, bool[] currentKeyStates)
        {
            // SAFETY CHECK: Validate parameters
            if (currentKeyStates == null || currentKeyStates.Length < 256)
            {
                System.Diagnostics.Debug.WriteLine("[InputState.CleanOldTimestamps] ERROR: currentKeyStates is null or too small");
                return;
            }
            
            if (maxAgeTicks < 0)
            {
                System.Diagnostics.Debug.WriteLine($"[InputState.CleanOldTimestamps] WARNING: maxAgeTicks is negative: {maxAgeTicks}");
                return;
            }
            
            fixed (long* timestamps = _keyTimestamps)
            fixed (long* latches = _keyLatchTimestamps)
            fixed (bool* keys = _keys)
            {
                // SAFETY CHECK: Validate pointers
                if (timestamps == null || latches == null || keys == null)
                {
                    System.Diagnostics.Debug.WriteLine("[InputState.CleanOldTimestamps] ERROR: timestamps, latches, or keys pointer is null");
                    return;
                }
                
                for (int i = 0; i < 256; i++)
                {
                    // Clean expired timestamps
                    long ts = Volatile.Read(ref timestamps[i]);
                    if (ts > 0)
                    {
                        long age = currentTime - ts;
                        // SAFETY CHECK: Validate age calculation (prevent negative/overflow)
                        if (age < 0)
                        {
                            // Timestamp in the future - should not happen, but handle gracefully
                            if (System.Diagnostics.Debugger.IsAttached)
                                System.Diagnostics.Debug.WriteLine($"[InputState.CleanOldTimestamps] WARNING: Negative age for key {i}: {age}");
                            continue;
                        }
                        
                        // FIX: Double-check key state AFTER timestamp comparison
                        // This prevents cleaning timestamp of a key that was just pressed
                        bool isKeyPressed = Volatile.Read(ref keys[i]);
                        if (age > maxAgeTicks && !isKeyPressed && !currentKeyStates[i])
                        {
                            // Double-check timestamp hasn't changed (key might have been pressed again)
                            long ts2 = Volatile.Read(ref timestamps[i]);
                            if (ts2 == ts) // Timestamp unchanged = safe to clean
                            {
                                Volatile.Write(ref timestamps[i], 0);
                            }
                        }
                    }
                    
                    // Clean expired latches
                    long latch = Volatile.Read(ref latches[i]);
                    if (latch > 0)
                    {
                        long latchAge = currentTime - latch;
                        if (latchAge >= 0 && latchAge > minVisualTicks)
                        {
                            // Double-check latch hasn't been refreshed
                            long latch2 = Volatile.Read(ref latches[i]);
                            if (latch2 == latch) // Latch unchanged = safe to clean
                            {
                                Volatile.Write(ref latches[i], 0);
                            }
                        }
                    }
                }
            }
        }
        
        // Lock for snapshot creation (prevents race conditions during cleanup)
        private static readonly object _snapshotLock = new object();
        
        /// <summary>
        /// Create a snapshot of current state (thread-safe copy)
        /// OPTIMIZED: Minimal snapshot - only reads what's needed
        /// For keys, we still need to check all 256 for UI rendering, but we do it efficiently
        /// FIX: Added lock to prevent race conditions during cleanup
        /// </summary>
        public InputStateSnapshot CreateSnapshot()
        {
            // FIX: Lock to prevent race condition if cleanup happens during snapshot
            lock (_snapshotLock)
            {
                var snapshot = new InputStateSnapshot();
            
            // OPTIMIZED: Copy all key states (256 bools = 256 bytes, very fast)
            // We need all keys for UI rendering (to know which keys to show/hide)
            // Volatile reads ensure we get the latest state without locks
            fixed (bool* keys = _keys)
            fixed (long* timestamps = _keyTimestamps)
            fixed (long* latches = _keyLatchTimestamps)
            fixed (byte* eventBuffer = _eventBuffer)
            fixed (int* eventBufferHead = &_eventBufferHead)
            {
                for (int i = 0; i < 256; i++)
                {
                    snapshot.Keys[i] = Volatile.Read(ref keys[i]);
                    snapshot.KeyTimestamps[i] = Volatile.Read(ref timestamps[i]);
                    snapshot.KeyLatchTimestamps[i] = Volatile.Read(ref latches[i]);
                }
                
                // Copy event buffer (circular buffer of recent key presses)
                snapshot.EventBuffer = new byte[EVENT_BUFFER_SIZE];
                int head = Volatile.Read(ref *eventBufferHead);
                for (int i = 0; i < EVENT_BUFFER_SIZE; i++)
                {
                    snapshot.EventBuffer[i] = Volatile.Read(ref eventBuffer[i]);
                }
                snapshot.EventBufferHead = head;
            }
            
            // Copy mouse button states (5 bools, minimal)
            fixed (bool* buttons = _mouseButtons)
            {
                for (int i = 0; i < 5; i++)
                {
                    snapshot.MouseButtons[i] = Volatile.Read(ref buttons[i]);
                }
            }
            
            // Read atomic values (already thread-safe via Volatile/Interlocked)
            snapshot.LastKey = Volatile.Read(ref LastKey);
            snapshot.SecondLastKey = Volatile.Read(ref SecondLastKey);
            snapshot.MousePosition = MousePosition; // Point is value type, copy is atomic
            snapshot.LastInputTimestamp = Volatile.Read(ref LastInputTimestamp);
            snapshot.WheelDelta = GetAndResetWheelDelta(); // Already atomic via Interlocked
            
            return snapshot;
            } // End lock
        }
    }
    
    /// <summary>
    /// Snapshot of input state (safe to read from UI thread)
    /// </summary>
    public class InputStateSnapshot
    {
        public bool[] Keys { get; } = new bool[256];
        public long[] KeyTimestamps { get; } = new long[256]; // Timestamp for each key
        public long[] KeyLatchTimestamps { get; } = new long[256]; // Latch timestamp for minimum display duration
        public byte[] EventBuffer { get; set; } = new byte[32]; // Circular buffer of recent key presses
        public int EventBufferHead { get; set; } // Write position in event buffer
        public bool[] MouseButtons { get; } = new bool[5];
        public byte LastKey { get; set; }
        public byte SecondLastKey { get; set; }
        public Point MousePosition { get; set; }
        public long LastInputTimestamp { get; set; }
        public int WheelDelta { get; set; }
    }
}
