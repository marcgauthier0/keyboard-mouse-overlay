using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using GamingKeypressOverlay.Diagnostics;

namespace GamingKeypressOverlay.Input
{
    /// <summary>
    /// Raw Input manager running on a dedicated thread with AboveNormal priority.
    /// This thread is completely separate from WPF Dispatcher, preventing starvation.
    /// 
    /// IMPORTANT DESIGN NOTES:
    /// - AboveNormal priority is often BETTER than Highest (reduces priority inversion)
    /// - CPU affinity is DISABLED by default (modern CPUs with P/E cores benefit from scheduler flexibility)
    /// - Polling is CONDITIONAL (only when WM_INPUT is delayed, detected via timeout)
    /// - Uses MsgWaitForMultipleObjectsEx to avoid busy-polling and reduce contention
    /// 
    /// When COD (or other games) saturate CPU:
    /// - WM_INPUT is captured at interrupt time, but delivery to user mode can be delayed
    /// - Polling helps ONLY when message delivery is late (timeout = late delivery)
    /// - Removing affinity often reduces latency by letting scheduler move thread away from hot cores
    /// </summary>
    public unsafe class RawInputThreadManager : IDisposable
    {
        // Configuration flags (can be changed on the fly via settings)
        // IMPORTANT: Polling is a FALLBACK, not redundancy
        // Only enable when Raw Input fails or messages are delayed
        private bool _enableContinuousPolling = false; // Default: disabled - only use as fallback
        
        // IMPORTANT: AboveNormal is often BETTER than Highest for input threads
        // Highest can cause priority inversion and increase contention under load
        // Windows input threads typically run at AboveNormal, not Highest
        // NOTE: Changing this requires thread restart
        private bool _useHighestPriority = false; // Default: AboveNormal (safer and often faster)
        
        // RECOMMENDATION: Enable safety checks for unsafe code (development/debug)
        // Can be changed on the fly without restart
        private volatile bool _enableSafetyChecks = true; // Set to false for maximum performance
        
        private Thread _inputThread;
        private volatile bool _disposed = false;
        private volatile bool _threadRunning = false;
        private volatile bool _rawInputInitialized = false;
        private volatile bool _fallbackMode = false;
        private readonly object _disposeLock = new object();
        private IntPtr _windowClassAtom = IntPtr.Zero;
        private GCHandle? _windowGCHandle = null; // Track GCHandle to ensure single free
        
        // Shared input state (lock-free, thread-safe)
        private unsafe InputState* _inputState;
        private IntPtr _inputStatePtr;
        
        // Window handle for Raw Input (created on input thread)
        private IntPtr _rawInputWindowHandle = IntPtr.Zero;
        
        // Track previous button states for continuous polling (like keyboard)
        private bool[] _previousButtonStates = new bool[5];
        
        // Track previous keyboard key states for continuous polling (ultra-fast capture)
        // Size: 256 keys (VK codes 0-255)
        private bool[] _previousKeyStates = new bool[256];
        
        // Keys to poll continuously (dynamically updated based on keyboard mode)
        private HashSet<byte> _keysToPoll = new HashSet<byte>();
        
        // Counter for position polling (optimize: position doesn't need 8000 Hz)
        private int _positionPollCounter = 0;
        
        // POOL: Reusable buffers to avoid allocations (clean memory management)
        private IntPtr _rawInputBuffer = IntPtr.Zero; // Reusable buffer for Raw Input (max 4KB)
        private const int MAX_RAW_INPUT_SIZE = 4096; // Maximum expected Raw Input size
        private POINT _reusablePoint = new POINT(); // Reusable POINT struct (zero-allocation)
        
        public unsafe InputState* InputState => _inputState;
        public bool RawInputInitialized => _rawInputInitialized;
        public bool FallbackMode => _fallbackMode;
        
        // Properties to change settings on the fly
        public bool EnableContinuousPolling
        {
            get => _enableContinuousPolling;
            set
            {
                _enableContinuousPolling = value;
                if (_enableSafetyChecks)
                    System.Diagnostics.Debug.WriteLine($"[RawInputThreadManager] EnableContinuousPolling changed to: {value}");
            }
        }
        
        public bool UseHighestPriority
        {
            get => _useHighestPriority;
            set
            {
                // NOTE: Changing thread priority requires thread restart
                // This will be applied on next initialization
                _useHighestPriority = value;
                if (_enableSafetyChecks)
                    System.Diagnostics.Debug.WriteLine($"[RawInputThreadManager] UseHighestPriority changed to: {value} (requires restart)");
            }
        }
        
        public bool EnableSafetyChecks
        {
            get => _enableSafetyChecks;
            set
            {
                _enableSafetyChecks = value;
                System.Diagnostics.Debug.WriteLine($"[RawInputThreadManager] EnableSafetyChecks changed to: {value}");
            }
        }
        
        private bool _enableCpuAffinity = false; // OFF by default - risky on modern CPUs
        
        public bool EnableCpuAffinity
        {
            get => _enableCpuAffinity;
            set => _enableCpuAffinity = value;
        }
        
        public RawInputThreadManager(bool enableContinuousPolling = false, bool useHighestPriority = true, bool enableSafetyChecks = true, bool enableCpuAffinity = false, InputState* externalInputState = null)
        {
            // Store configuration
            _enableContinuousPolling = enableContinuousPolling;
            _useHighestPriority = useHighestPriority;
            _enableSafetyChecks = enableSafetyChecks;
            _enableCpuAffinity = enableCpuAffinity;
            
            // SAFETY CHECK: Validate memory allocation
            if (_enableSafetyChecks)
            {
                System.Diagnostics.Debug.WriteLine("[RawInputThreadManager] Initializing with safety checks enabled");
            }
            
            // Use external InputState if provided, otherwise allocate our own
            if (externalInputState != null)
            {
                _inputState = externalInputState;
                _inputStatePtr = IntPtr.Zero; // Don't free external memory
            }
            else
            {
                // Allocate unmanaged memory for InputState (zero-allocation, cache-friendly)
                _inputStatePtr = Marshal.AllocHGlobal(Marshal.SizeOf<InputState>());
                
                // SAFETY CHECK: Validate allocation
                if (_inputStatePtr == IntPtr.Zero)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for InputState");
                }
                
                _inputState = (InputState*)_inputStatePtr.ToPointer();
                
                // SAFETY CHECK: Validate pointer
                if (_inputState == null)
                {
                    throw new InvalidOperationException("Failed to get pointer to InputState");
                }
                
                // Initialize state
                *_inputState = new InputState();
            }
            
            // POOL: Pre-allocate reusable buffer for Raw Input (clean memory management)
            // This avoids allocating/freeing on every WM_INPUT message (8000-12000 Hz)
            _rawInputBuffer = Marshal.AllocHGlobal(MAX_RAW_INPUT_SIZE);
            
            // SAFETY CHECK: Validate buffer allocation
            if (_rawInputBuffer == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Failed to allocate memory for Raw Input buffer");
            }
            
            if (_enableSafetyChecks)
            {
                System.Diagnostics.Debug.WriteLine($"[RawInputThreadManager] Memory allocated: InputState={_inputStatePtr.ToInt64():X}, Buffer={_rawInputBuffer.ToInt64():X}");
            }
            
            // Start dedicated input thread
            // CRITICAL: SetApartmentState(STA) is REQUIRED for Win32 message loop
            // Without STA, WM_INPUT messages may not be processed correctly
            _inputThread = new Thread(InputThreadProc)
            {
                Name = "RawInputThread",
                // IMPORTANT: AboveNormal is often BETTER than Highest
                // Highest can cause priority inversion and increase contention under load
                // Windows input threads typically run at AboveNormal, not Highest
                // Only use Highest if you've verified it helps on your specific system
                Priority = _useHighestPriority ? ThreadPriority.Highest : ThreadPriority.AboveNormal,
                IsBackground = false // Keep thread alive
            };
            
            if (_enableSafetyChecks)
            {
                System.Diagnostics.Debug.WriteLine($"[RawInputThreadManager] Thread priority: {_inputThread.Priority}, Polling: {_enableContinuousPolling}");
            }
            
            // CRITICAL: STA (Single Threaded Apartment) is required for Win32 message loop
            // This ensures WM_INPUT messages are properly dispatched on this thread
            _inputThread.SetApartmentState(ApartmentState.STA);
            
            _threadRunning = true;
            _inputThread.Start();
            
            // WARNING: CPU Affinity is OPTIONAL and DISABLED by default
            // Modern CPUs (P/E cores, Windows 11) may have worse latency if pinned incorrectly
            // Only enable if you understand your CPU topology
            if (_enableCpuAffinity)
            {
                try
                {
                    // Wait a bit for thread to start and get its OS thread ID
                    Thread.Sleep(50);
                    ProcessThread inputProcessThread = null;
                    foreach (ProcessThread pt in Process.GetCurrentProcess().Threads)
                    {
                        if (pt.Id == _inputThread.ManagedThreadId)
                        {
                            inputProcessThread = pt;
                            break;
                        }
                    }
                    if (inputProcessThread != null)
                    {
                        // WARNING: This assumes last core is available - may be E-core on modern CPUs
                        // This logic is from ~2018-2020 era and may not be optimal on modern systems
                        int processorCount = Environment.ProcessorCount;
                        if (processorCount >= 8)
                        {
                            // 8+ cores: Use last core (RISKY - may be E-core on Alder Lake+)
                            long lastCoreMask = 1L << (processorCount - 1);
                            inputProcessThread.ProcessorAffinity = new IntPtr(lastCoreMask);
                            System.Diagnostics.Debug.WriteLine($"[CPU AFFINITY] WARNING: Raw Input thread pinned to core {processorCount - 1} (may be E-core on modern CPUs)");
                        }
                        else if (processorCount >= 4)
                        {
                            // 4-7 cores: Use last core
                            long lastCoreMask = 1L << (processorCount - 1);
                            inputProcessThread.ProcessorAffinity = new IntPtr(lastCoreMask);
                            System.Diagnostics.Debug.WriteLine($"[CPU AFFINITY] WARNING: Raw Input thread pinned to core {processorCount - 1}");
                        }
                        else
                        {
                            // <4 cores: Use all cores (can't avoid conflict)
                            inputProcessThread.ProcessorAffinity = new IntPtr(0xFF);
                            System.Diagnostics.Debug.WriteLine($"[CPU AFFINITY] Raw Input thread using all cores (system has {processorCount} cores)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CPU AFFINITY] Failed to set CPU affinity: {ex.Message}");
                    // Continue anyway - affinity is optional
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[CPU AFFINITY] CPU affinity disabled (default - safer on modern CPUs)");
            }
            
            // Wait for thread to initialize (max 2 seconds)
            int waitCount = 0;
            while (_rawInputWindowHandle == IntPtr.Zero && waitCount < 200 && _threadRunning)
            {
                Thread.Sleep(10);
                waitCount++;
            }
            
            if (_rawInputWindowHandle == IntPtr.Zero)
            {
                // FIX: Enable fallback mode instead of throwing
                _fallbackMode = true;
                _rawInputInitialized = false;
                System.Diagnostics.Debug.WriteLine("[RawInputThreadManager] WARNING: Failed to initialize Raw Input thread, enabling fallback mode");
                // Don't throw - allow fallback mode to work
                // throw new InvalidOperationException("Failed to initialize Raw Input thread");
            }
            else
            {
                _rawInputInitialized = true;
            }
        }
        
        private void InputThreadProc()
        {
            try
            {
                // Create a hidden window for Raw Input (Win32, not WPF)
                _rawInputWindowHandle = CreateRawInputWindow();
                
                if (_rawInputWindowHandle == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to create Raw Input window");
                    _fallbackMode = true;
                    _rawInputInitialized = false;
                    return;
                }
                
                // Register Raw Input devices
                bool registered = RegisterRawInputDevices();
                if (!registered)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to register Raw Input devices");
                    _fallbackMode = true;
                    _rawInputInitialized = false;
                    // Continue anyway - fallback mode will use polling
                }
                else
                {
                    _rawInputInitialized = true;
                    _fallbackMode = false;
                }
                
                // Message loop using MsgWaitForMultipleObjectsEx for better scheduling
                // IMPORTANT: This approach allows us to detect when WM_INPUT is delayed
                // and only poll during timeout windows (when messages are late)
                // This avoids busy-polling and reduces contention with foreground processes
                MSG msg;
                const uint POLL_TIMEOUT_MS = 1; // OPTIMIZED: 1ms timeout for faster mouse polling (was 5ms)
                // CRITICAL: Mouse buttons MUST be polled frequently, not just on timeout
                // COD can monopolize CPU and delay WM_INPUT, so we need aggressive polling
                long lastMousePollTime = Stopwatch.GetTimestamp();
                long MOUSE_POLL_INTERVAL_TICKS = Stopwatch.Frequency / 1000; // 1ms = 1000Hz polling (calculated at runtime)
                
                while (_threadRunning && !_disposed)
                {
                    // CRITICAL: Poll mouse buttons aggressively (every 1ms) regardless of messages
                    // This ensures mouse clicks are detected even when COD monopolizes CPU
                    long currentTime = Stopwatch.GetTimestamp();
                    if (currentTime - lastMousePollTime >= MOUSE_POLL_INTERVAL_TICKS)
                    {
                        PollMouseButtonsContinuous();
                        lastMousePollTime = currentTime;
                    }
                    
                    // Use MsgWaitForMultipleObjectsEx to wait for messages OR timeout
                    // This wakes up on:
                    // 1. New messages arrive (WM_INPUT, etc.) - normal case
                    // 2. Timeout expires - indicates WM_INPUT might be delayed
                    uint waitResult = MsgWaitForMultipleObjectsEx(
                        0,                          // No handles to wait on
                        null,                       // No handles array
                        POLL_TIMEOUT_MS,            // Timeout: 1ms (optimized for mouse polling)
                        QS_ALLINPUT,                // Wait for all input message types
                        MWMO_INPUTAVAILABLE);       // Return even if messages already in queue
                    
                    if (waitResult == WAIT_TIMEOUT)
                    {
                        // Timeout occurred - WM_INPUT might be delayed
                        // This is when polling is useful: message delivery is late
                        if (_enableContinuousPolling && _rawInputInitialized)
                        {
                            // Poll keyboard only when messages are delayed (timeout = late delivery)
                            // This avoids busy-polling when messages arrive on time
                            PollKeyboardContinuous();
                        }
                        
                        // Mouse buttons already polled above (aggressive polling)
                        // Poll position periodically (lightweight)
                        if (++_positionPollCounter % 10 == 0)
                        {
                            PollMousePositionContinuous();
                        }
                        
                        // Check for messages that might have arrived during timeout
                        // Use PeekMessage to check without blocking
                        if (PeekMessage(out msg, IntPtr.Zero, 0, 0, 0x0001)) // PM_REMOVE = 0x0001
                        {
                            // Message available - process it
                            if (msg.message == WM_QUIT)
                            {
                                _threadRunning = false;
                                break;
                            }
                            
                            TranslateMessage(ref msg);
                            DispatchMessage(ref msg);
                        }
                        
                        continue; // Continue loop to wait again
                    }
                    else if (waitResult == WAIT_OBJECT_0)
                    {
                        // Messages are available - process them normally
                        // This is the fast path when WM_INPUT arrives on time
                        int result = GetMessage(out msg, IntPtr.Zero, 0, 0);
                        
                        if (result == -1)
                        {
                            // Error - should not happen, but exit gracefully
                            System.Diagnostics.Debug.WriteLine("GetMessage error in Raw Input thread");
                            break;
                        }
                        else if (result == 0)
                        {
                            // WM_QUIT received
                            _threadRunning = false;
                            break;
                        }
                        
                        // Process message (WM_INPUT will be handled in WndProc)
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                        
                        // Mouse buttons already polled above (aggressive polling)
                        // Poll position periodically (lightweight)
                        if (++_positionPollCounter % 10 == 0)
                        {
                            PollMousePositionContinuous();
                        }
                    }
                    else
                    {
                        // Unexpected return value - continue loop
                        System.Diagnostics.Debug.WriteLine($"Unexpected wait result: {waitResult}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Raw Input thread error: {ex.Message}");
            }
            finally
            {
                // Cleanup
                if (_rawInputWindowHandle != IntPtr.Zero)
                {
                    UnregisterRawInputDevices();
                    DestroyWindow(_rawInputWindowHandle);
                    _rawInputWindowHandle = IntPtr.Zero;
                }
            }
        }
        
        private IntPtr CreateRawInputWindow()
        {
            // Create delegate for WndProc (must be kept alive)
            _wndProcDelegate = StaticWndProc;
            
            // Register window class
            WNDCLASS wc = new WNDCLASS
            {
                lpfnWndProc = _wndProcDelegate,
                hInstance = GetModuleHandle(null),
                lpszClassName = "RawInputWindowClass"
            };
            
            ushort atom = RegisterClass(ref wc);
            if (atom == 0)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0x00000582) // ERROR_CLASS_ALREADY_EXISTS
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to register window class: {error}");
                    return IntPtr.Zero;
                }
            }
            _windowClassAtom = new IntPtr(atom);
            
            // Create hidden window for Raw Input
            // Must use valid window style to receive WM_INPUT messages
            // WS_EX_TOOLWINDOW: hidden window that doesn't appear in taskbar
            // WS_EX_NOACTIVATE: window never receives focus (prevents activation)
            // WS_POPUP: popup window (no title bar, minimal)
            // WS_DISABLED: window is disabled (prevents user interaction, but still receives messages)
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_NOACTIVATE = 0x08000000;
            const uint WS_POPUP = 0x80000000;
            const uint WS_DISABLED = 0x08000000;
            
            IntPtr hwnd = CreateWindowEx(
                (int)(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE),  // Extended style: hidden, no activation
                "RawInputWindowClass",
                "RawInputWindow",
                WS_POPUP | WS_DISABLED,  // Window style: popup + disabled (still receives WM_INPUT)
                0, 0, 1, 1,        // Minimal size (1x1), position doesn't matter
                IntPtr.Zero,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero
            );
            
            // CRITICAL: Window must exist and be valid to receive WM_INPUT
            // For RIDEV_INPUTSINK, window doesn't need focus, but must be shown
            if (hwnd != IntPtr.Zero)
            {
                // Show window (SW_SHOWNOACTIVATE = show without activating)
                // This is required for Raw Input to work properly
                const int SW_SHOWNOACTIVATE = 4;
                ShowWindow(hwnd, SW_SHOWNOACTIVATE);
                
                // Move window off-screen so it's not visible to user
                // But keep it "shown" so Windows sends WM_INPUT messages
                SetWindowPos(hwnd, IntPtr.Zero, -32000, -32000, 0, 0, 0x0001); // SWP_NOSIZE | SWP_NOZORDER
                
                System.Diagnostics.Debug.WriteLine($"Raw Input window created: hwnd=0x{hwnd.ToInt64():X}");
            }
            
            // Store instance pointer in window user data using GCHandle
            // WARNING: GCHandle must be freed exactly once in Dispose()
            // Any double-free or window recreation bug will cause leak or crash
            // This is acceptable but brittle - be careful during refactors
            if (hwnd != IntPtr.Zero)
            {
                GCHandle handle = GCHandle.Alloc(this, GCHandleType.Normal);
                _windowGCHandle = handle; // Track for safe disposal
                SetWindowLongPtr(hwnd, GWLP_USERDATA, GCHandle.ToIntPtr(handle));
            }
            
            return hwnd;
        }
        
        private WndProcDelegate _wndProcDelegate;
        private const int GWLP_USERDATA = -21;
        
        private static IntPtr StaticWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            // Get instance from window user data
            IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
            if (userData != IntPtr.Zero)
            {
                GCHandle handle = GCHandle.FromIntPtr(userData);
                if (handle.IsAllocated)
                {
                    var instance = handle.Target as RawInputThreadManager;
                    if (instance != null)
                    {
                        return instance.WndProc(hwnd, msg, wParam, lParam);
                    }
                }
            }
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }
        
        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_INPUT && !_disposed && _inputState != null)
            {
                // OPTIMIZED: Ultra-fast path - no try-catch, no logging
                // Processing must be minimal to avoid lag in games like COD
                ProcessRawInput(lParam);
                // CRITICAL: For WM_INPUT, we must return DefWindowProc result, not 0
                // Returning 0 can prevent Windows from processing the message correctly
                return DefWindowProc(hwnd, msg, wParam, lParam);
            }
            
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }
        
        private unsafe void ProcessRawInput(IntPtr lParam)
        {
            // OPTIMIZED: Minimal validation - fast path for hot loop (8000-12000 Hz)
            if (lParam == IntPtr.Zero || _rawInputBuffer == IntPtr.Zero || _inputState == null)
                return;
            
            // Get size of raw input data
            uint dwSize = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
            
            // Quick validation
            if (dwSize == 0 || dwSize > MAX_RAW_INPUT_SIZE)
                return;
            
            // Get raw input data into reusable buffer
            uint actualSize = dwSize;
            if (GetRawInputData(lParam, RID_INPUT, _rawInputBuffer, ref actualSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == UIntPtr.Zero)
                return;
            
            // CRITICAL: Use direct pointer access instead of Marshal.PtrToStructure
            // This avoids marshalling issues with unions (RAWMOUSE ulButtons/usButtonFlags/usButtonData)
            byte* pRawInput = (byte*)_rawInputBuffer.ToPointer();
            if (pRawInput == null)
                return;
            
            // Read header directly (RAWINPUTHEADER is 24 bytes)
            // Offset 0: dwType (uint)
            uint dwType = *(uint*)(pRawInput + 0);
            
            // Process keyboard input
            if (dwType == RIM_TYPEKEYBOARD)
            {
                // Read keyboard data directly from buffer (after 24-byte header)
                RAWKEYBOARD keyboard;
                byte* pKeyboard = pRawInput + 24; // Skip RAWINPUTHEADER (24 bytes)
                keyboard.MakeCode = *(ushort*)(pKeyboard + 0);
                keyboard.Flags = *(ushort*)(pKeyboard + 2);
                keyboard.Reserved = *(ushort*)(pKeyboard + 4);
                keyboard.VKey = *(ushort*)(pKeyboard + 6);
                keyboard.Message = *(uint*)(pKeyboard + 8);
                keyboard.ExtraInformation = *(uint*)(pKeyboard + 12);
                
                ProcessKeyboardInput(keyboard);
            }
            // Process mouse input (wheel events only - buttons are polled)
            else if (dwType == RIM_TYPEMOUSE)
            {
                // CRITICAL: Read mouse data directly via pointer (UNSAFE)
                // This ensures correct reading of union fields (usButtonFlags/usButtonData)
                // Mouse data starts after 24-byte header
                RAWMOUSE mouse;
                byte* pMouse = pRawInput + 24; // Skip RAWINPUTHEADER (24 bytes)
                
                // Read RAWMOUSE structure directly (avoiding marshalling union issues)
                // CRITICAL: There's 2 bytes of padding after usFlags to align the union on 4 bytes
                // Structure layout:
                // usFlags: offset 0 (2 bytes)
                // padding: offset 2 (2 bytes) - for alignment
                // ulButtons/usButtonFlags: offset 4 (4 bytes union)
                // usButtonData: offset 6 (2 bytes, part of union)
                // ulRawButtons: offset 8 (4 bytes)
                // lLastX: offset 12 (4 bytes)
                // lLastY: offset 16 (4 bytes)
                // ulExtraInformation: offset 20 (4 bytes)
                mouse.usFlags = *(ushort*)(pMouse + 0);
                mouse.usButtonFlags = *(ushort*)(pMouse + 4);  // UNION: at offset 4 (after padding)
                mouse.usButtonData = *(ushort*)(pMouse + 6);   // UNION: at offset 6 (after usButtonFlags)
                mouse.ulRawButtons = *(uint*)(pMouse + 8);
                mouse.lLastX = *(int*)(pMouse + 12);
                mouse.lLastY = *(int*)(pMouse + 16);
                mouse.ulExtraInformation = *(uint*)(pMouse + 20);
                
                ProcessMouseInput(mouse);
            }
            // No finally needed - buffer is reused, not freed
        }
        
        /// <summary>
        /// OPTIMIZED: Ultra-fast keyboard input processing for hot path (8000-12000 Hz)
        /// Minimal processing - just update state. No logging, no try-catch in hot path.
        /// </summary>
        private unsafe void ProcessKeyboardInput(RAWKEYBOARD keyboard)
        {
            int vkCode = keyboard.VKey;
            ushort makeCode = keyboard.MakeCode;
            
            // Quick validation - skip invalid keys
            if (vkCode == 0 || vkCode > 255) return;
            
            // Skip system keys (Alt, Windows keys) - they can cause issues
            if (vkCode == 0x12 || vkCode == 0x5B || vkCode == 0x5C) // Alt, LWin, RWin
                return;
            
            // OPTIMIZED: Check if key is pressed or released using RI_KEY_BREAK flag
            // RI_KEY_BREAK (0x01) = key released, RI_KEY_MAKE (0x00) = key pressed
            bool isKeyDown = (keyboard.Flags & RI_KEY_BREAK) == 0;
            
            // CRITICAL: Handle Shift keys specially
            // VK_SHIFT (0x10) is a "generic" Shift key - we need to distinguish Left/Right using scan code
            // Scan codes: 0x2A = Left Shift, 0x36 = Right Shift
            if (vkCode == 0x10) // VK_SHIFT
            {
                if (makeCode == 0x2A)
                {
                    // Left Shift - use VK_LSHIFT (0xA0)
                    _inputState->SetKey(0xA0, isKeyDown);
                }
                else if (makeCode == 0x36)
                {
                    // Right Shift - use VK_RSHIFT (0xA1)
                    _inputState->SetKey(0xA1, isKeyDown);
                }
                else
                {
                    // Fallback: use Left Shift as default (most common)
                    _inputState->SetKey(0xA0, isKeyDown);
                }
            }
            // CRITICAL: Handle Ctrl keys specially (same issue as Shift)
            // VK_CONTROL (0x11) is generic - distinguish Left/Right using extended flag
            // Extended keys have RI_KEY_E0 (0x02) flag set
            else if (vkCode == 0x11) // VK_CONTROL
            {
                // Check if extended key flag is set (RI_KEY_E0 = 0x02)
                // Extended = Right Ctrl, Non-extended = Left Ctrl
                if ((keyboard.Flags & RI_KEY_E0) != 0)
                {
                    // Right Ctrl - use VK_RCONTROL (0xA3)
                    _inputState->SetKey(0xA3, isKeyDown);
                }
                else
                {
                    // Left Ctrl - use VK_LCONTROL (0xA2)
                    _inputState->SetKey(0xA2, isKeyDown);
                }
            }
            else
            {
                // Normal key - update state directly (ultra-fast path)
                _inputState->SetKey((byte)vkCode, isKeyDown);
            }
        }
        
        /// <summary>
        /// OPTIMIZED: Ultra-fast mouse input processing (wheel events only)
        /// Mouse buttons and position are polled continuously, not handled here.
        /// </summary>
        private unsafe void ProcessMouseInput(RAWMOUSE mouse)
        {
            // NOTE: Mouse buttons and position are now polled continuously in the main loop
            // (like keyboard). This function only processes wheel events from Raw Input
            
            ushort buttonFlags = mouse.usButtonFlags;
            ushort usButtonData = mouse.usButtonData;
            
            // OPTIMIZED: Just check if RI_MOUSE_WHEEL flag is set (0x0400)
            // Even if usButtonData is 0 (common in AAA games with exclusive input),
            // we can still animate the wheel to show that a scroll occurred
            if ((buttonFlags & RI_MOUSE_WHEEL) != 0)
            {
                // Wheel scroll detected! Animate regardless of delta value
                short wheelDelta = unchecked((short)usButtonData);
                
                // If usButtonData is 0 or unreliable (common in AAA games), use a default direction
                // This ensures the wheel always animates when RI_MOUSE_WHEEL is detected
                if (wheelDelta == 0)
                {
                    wheelDelta = 120; // Default to UP scroll for animation
                }
                
                _inputState->AddWheelDelta(wheelDelta);
            }
            
            // Process horizontal wheel (rare, but supported)
            if ((buttonFlags & RI_MOUSE_HWHEEL) != 0)
            {
                short wheelDelta = unchecked((short)usButtonData);
                // Could add horizontal wheel support if needed
            }
        }
        
        /// <summary>
        /// Set the list of keys to poll continuously based on keyboard mode
        /// Only poll keys that are visible in the current mode (Gaming or Full)
        /// </summary>
        public void SetKeysToPoll(HashSet<byte> vkCodes)
        {
            lock (_keysToPoll)
            {
                _keysToPoll = new HashSet<byte>(vkCodes);
            }
        }
        
        /// <summary>
        /// Continuous polling of keyboard keys (for COD gaming - ultra-fast capture)
        /// This is OUR OWN polling, independent of WM_INPUT messages
        /// Works even when COD monopolizes CPU - ensures even very rapid key presses (<10ms) are captured
        /// CRITICAL: Essential for actions like Ctrl+C (slide+jump) that happen in <20ms
        /// Only polls keys that are visible in the current keyboard mode
        /// </summary>
        private unsafe void PollKeyboardContinuous()
        {
            // Poll only keys that are visible in the current keyboard mode
            // This is more efficient and ensures we only poll what we display
            HashSet<byte> keysToPoll;
            lock (_keysToPoll)
            {
                keysToPoll = new HashSet<byte>(_keysToPoll);
            }
            
            // If no keys specified, don't poll (fallback to WM_INPUT only)
            if (keysToPoll.Count == 0) return;
            
            // Poll each key in the list
            foreach (byte vkCode in keysToPoll)
            {
                PollKey(vkCode, _previousKeyStates);
            }
        }
        
        /// <summary>
        /// Poll a single key state - ONLY detects MISSED states (fallback mode)
        /// IMPORTANT: Raw Input is the source of truth. Polling only fills gaps.
        /// 
        /// REALITY CHECK:
        /// - WM_INPUT is queued at interrupt time
        /// - If Windows can't deliver WM_INPUT, polling won't save you either
        /// - Missed input is usually due to exclusive mode + anti-cheat, not scheduling
        /// - Polling does not guarantee capture of <10ms presses better than Raw Input
        /// 
        /// Only updates if:
        /// - Key is pressed according to GetAsyncKeyState
        /// - BUT InputState says it's not pressed (missed by Raw Input)
        /// This prevents conflicts and ensures Raw Input owns normal transitions
        /// </summary>
        private unsafe void PollKey(byte vkCode, bool[] previousStates)
        {
            const ushort KEY_PRESSED_MASK = 0x8000;
            bool pressedAccordingToPolling = (GetAsyncKeyState(vkCode) & KEY_PRESSED_MASK) != 0;
            bool pressedAccordingToRawInput = _inputState->GetKey(vkCode);
            
            // Only update if polling detects a pressed state that Raw Input missed
            // This is a LIMITED FALLBACK - Raw Input should handle normal transitions
            // Note: If WM_INPUT is truly lost (exclusive mode, anti-cheat), polling may also fail
            if (pressedAccordingToPolling && !pressedAccordingToRawInput)
            {
                // Raw Input missed this press - update it (fallback mode)
                // This helps with edge cases but cannot fix fundamental WM_INPUT loss
                _inputState->SetKey(vkCode, true);
                previousStates[vkCode] = true;
                if (_enableSafetyChecks)
                    System.Diagnostics.Debug.WriteLine($"[POLLING FALLBACK] Key {vkCode} detected as pressed (missed by Raw Input)");
            }
            else if (!pressedAccordingToPolling && pressedAccordingToRawInput)
            {
                // Key was released according to polling but Raw Input still thinks it's pressed
                // Only update if the Raw Input state is stale (>100ms old)
                long keyTimestamp = _inputState->GetKeyTimestamp(vkCode);
                long currentTime = Stopwatch.GetTimestamp();
                long elapsedMs = (currentTime - keyTimestamp) * 1000 / Stopwatch.Frequency;
                
                if (elapsedMs > 100) // Raw Input state is stale (>100ms)
                {
                    _inputState->SetKey(vkCode, false);
                    previousStates[vkCode] = false;
                    if (_enableSafetyChecks)
                        System.Diagnostics.Debug.WriteLine($"[POLLING FALLBACK] Key {vkCode} released (Raw Input state was stale: {elapsedMs}ms)");
                }
            }
            
            // Update previous state for change detection
            previousStates[vkCode] = pressedAccordingToPolling;
        }
        
        /// <summary>
        /// Poll mouse buttons continuously
        /// IMPORTANT: ProcessMouseInput only handles wheel events, NOT button clicks
        /// Therefore, mouse buttons MUST be polled - this is the PRIMARY method, not a fallback
        /// </summary>
        private unsafe void PollMouseButtonsContinuous()
        {
            // Virtual key codes for mouse buttons (compile-time constants)
            const int VK_LBUTTON = 0x01;
            const int VK_RBUTTON = 0x02;
            const int VK_MBUTTON = 0x04;
            const int VK_XBUTTON1 = 0x05;
            const int VK_XBUTTON2 = 0x06;
            const ushort KEY_PRESSED_MASK = 0x8000;
            
            // Poll each button - buttons are PRIMARY via polling (not handled by Raw Input)
            PollMouseButton(0, VK_LBUTTON, KEY_PRESSED_MASK);
            PollMouseButton(1, VK_RBUTTON, KEY_PRESSED_MASK);
            PollMouseButton(2, VK_MBUTTON, KEY_PRESSED_MASK);
            PollMouseButton(3, VK_XBUTTON1, KEY_PRESSED_MASK);
            PollMouseButton(4, VK_XBUTTON2, KEY_PRESSED_MASK);
        }
        
        /// <summary>
        /// Poll a single mouse button - PRIMARY method (not fallback)
        /// ProcessMouseInput doesn't handle buttons, so polling is the only way to detect them
        /// </summary>
        private unsafe void PollMouseButton(int buttonIndex, int vkCode, ushort keyPressedMask)
        {
            bool pressedAccordingToPolling = (GetAsyncKeyState(vkCode) & keyPressedMask) != 0;
            bool currentState = _inputState->GetMouseButton(buttonIndex);
            
            // Update state if it changed (buttons are PRIMARY via polling, not Raw Input)
            if (pressedAccordingToPolling != currentState)
            {
                _inputState->SetMouseButton(buttonIndex, pressedAccordingToPolling);
                _previousButtonStates[buttonIndex] = pressedAccordingToPolling;
            }
            else
            {
                // State unchanged - just update previous state tracker
                _previousButtonStates[buttonIndex] = pressedAccordingToPolling;
            }
        }
        
        /// <summary>
        /// Continuous polling of mouse position (for COD gaming)
        /// Updates position directly in InputState
        /// POOL: Reuses _reusablePoint to avoid allocations
        /// </summary>
        private unsafe void PollMousePositionContinuous()
        {
            // POOL: Reuse pre-allocated POINT struct (zero-allocation)
            // Avoids creating new structs at 800-1200 Hz
            GetCursorPos(out _reusablePoint);
            Point mousePos = new Point(_reusablePoint.x, _reusablePoint.y);
            
            // Update position directly in InputState (like keyboard)
            _inputState->UpdateMousePosition(mousePos);
        }
        
        // POOL: Reusable device arrays (allocated once, reused)
        private RAWINPUTDEVICE[] _rawInputDevices = new RAWINPUTDEVICE[2];
        
        private bool RegisterRawInputDevices()
        {
            // POOL: Reuse pre-allocated array (no allocation per call)
            // Keyboard
            _rawInputDevices[0].usUsagePage = 0x01; // Generic Desktop Controls
            _rawInputDevices[0].usUsage = 0x06;     // Keyboard
            _rawInputDevices[0].dwFlags = RIDEV_INPUTSINK; // Receive input even when not in foreground
            _rawInputDevices[0].hwndTarget = _rawInputWindowHandle;
            
            // Mouse (CRITICAL: Must include wheel events)
            // usUsagePage = 0x01 (Generic Desktop Controls)
            // usUsage = 0x02 (Mouse) - this includes buttons AND wheel
            // dwFlags = RIDEV_INPUTSINK (receive even when not in foreground)
            // CRITICAL: Do NOT use RIDEV_NOLEGACY - it blocks wheel events!
            _rawInputDevices[1].usUsagePage = 0x01; // Generic Desktop Controls
            _rawInputDevices[1].usUsage = 0x02;     // Mouse (includes wheel)
            _rawInputDevices[1].dwFlags = RIDEV_INPUTSINK; // Receive input even when not in foreground
            _rawInputDevices[1].hwndTarget = _rawInputWindowHandle;
            
            System.Diagnostics.Debug.WriteLine($"[REGISTER] Registering Raw Input devices:");
            System.Diagnostics.Debug.WriteLine($"[REGISTER] Keyboard: UsagePage=0x01, Usage=0x06, Flags=0x{RIDEV_INPUTSINK:X}, HWND=0x{_rawInputWindowHandle.ToInt64():X}");
            System.Diagnostics.Debug.WriteLine($"[REGISTER] Mouse: UsagePage=0x01, Usage=0x02, Flags=0x{RIDEV_INPUTSINK:X}, HWND=0x{_rawInputWindowHandle.ToInt64():X}");
            
            if (!RegisterRawInputDevices(_rawInputDevices, (uint)_rawInputDevices.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                int error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"[REGISTER] FAILED to register Raw Input devices. Error: {error}");
                return false;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[REGISTER] SUCCESS: Raw Input devices registered. Window handle: 0x{_rawInputWindowHandle.ToInt64():X}");
                return true;
            }
        }
        
        private void UnregisterRawInputDevices()
        {
            // POOL: Reuse pre-allocated array
            _rawInputDevices[0].usUsagePage = 0x01;
            _rawInputDevices[0].usUsage = 0x06;
            _rawInputDevices[0].dwFlags = 0x00000002; // RIDEV_REMOVE
            _rawInputDevices[0].hwndTarget = IntPtr.Zero;
            
            _rawInputDevices[1].usUsagePage = 0x01;
            _rawInputDevices[1].usUsage = 0x02;
            _rawInputDevices[1].dwFlags = 0x00000002; // RIDEV_REMOVE
            _rawInputDevices[1].hwndTarget = IntPtr.Zero;
            
            RegisterRawInputDevices(_rawInputDevices, (uint)_rawInputDevices.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (_disposed) return;
                _disposed = true;
                
                if (disposing)
                {
                    // 1. Signal thread to exit gracefully
                    _threadRunning = false;
                    
                    // 2. Post WM_QUIT to thread for graceful shutdown
                    if (_rawInputWindowHandle != IntPtr.Zero)
                    {
                        // Free GCHandle stored in window user data
                        // CRITICAL: Only free once, use tracked handle to prevent double-free
                        if (_windowGCHandle.HasValue && _windowGCHandle.Value.IsAllocated)
                        {
                            _windowGCHandle.Value.Free();
                            _windowGCHandle = null; // Mark as freed
                        }
                        else
                        {
                            // Fallback: try to get from window (shouldn't happen if tracking works)
                            IntPtr userData = GetWindowLongPtr(_rawInputWindowHandle, GWLP_USERDATA);
                            if (userData != IntPtr.Zero)
                            {
                                try
                                {
                                    GCHandle handle = GCHandle.FromIntPtr(userData);
                                    if (handle.IsAllocated)
                                    {
                                        handle.Free();
                                    }
                                }
                                catch
                                {
                                    // Handle already freed or invalid - ignore
                                }
                            }
                        }
                        
                        PostMessage(_rawInputWindowHandle, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                    }
                    
                    // 3. Wait for thread to exit gracefully (max 1 second)
                    if (_inputThread != null && _inputThread.IsAlive)
                    {
                    if (!_inputThread.Join(1000))
                    {
                        CrashReporter.LogWarning("Raw Input thread did not stop gracefully within timeout. Thread will terminate when message loop exits.");
                            // Note: Thread.Abort() is obsolete in .NET Core/.NET 5+
                            // The thread will exit naturally when WM_QUIT is processed
                        }
                    }
                    
                    // 4. Unregister Raw Input devices
                    if (_rawInputWindowHandle != IntPtr.Zero)
                    {
                        UnregisterRawInputDevices();
                    }
                    
                    // 5. Destroy Win32 window
                    if (_rawInputWindowHandle != IntPtr.Zero)
                    {
                        if (IsWindow(_rawInputWindowHandle))
                        {
                            DestroyWindow(_rawInputWindowHandle);
                        }
                        _rawInputWindowHandle = IntPtr.Zero;
                    }
                    
                    // 6. Unregister window class
                    if (_windowClassAtom != IntPtr.Zero)
                    {
                        UnregisterClass("RawInputWindowClass", GetModuleHandle(null));
                        _windowClassAtom = IntPtr.Zero;
                    }
                }
                
                // 7. Free non-managed memory (ALWAYS, even if disposing=false)
                if (_rawInputBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_rawInputBuffer);
                    _rawInputBuffer = IntPtr.Zero;
                    
                    if (_enableSafetyChecks)
                        System.Diagnostics.Debug.WriteLine("[RawInputThreadManager] Raw Input buffer freed");
                }
                
                // 8. Free InputState memory (only if we allocated it, not if external)
                if (_inputStatePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_inputStatePtr);
                    _inputStatePtr = IntPtr.Zero;
                    _inputState = null;
                    
                    if (_enableSafetyChecks)
                        System.Diagnostics.Debug.WriteLine("[RawInputThreadManager] InputState memory freed");
                }
                else
                {
                    // External InputState - just clear reference, don't free
                    _inputState = null;
                }
                
                if (_enableSafetyChecks)
                    System.Diagnostics.Debug.WriteLine("[RawInputThreadManager] Dispose completed");
            }
        }
        
        ~RawInputThreadManager()
        {
            // Finalizer - only cleanup unmanaged resources
            Dispose(false);
        }
        
        #region Win32 API
        
        private const int WM_INPUT = 0x00FF;
        private const int WM_QUIT = 0x0012;
        private const int RID_INPUT = 0x10000003;
        
        // Constants for MsgWaitForMultipleObjectsEx
        private const uint QS_ALLINPUT = 0x04FF; // Wait for all input message types
        private const uint MWMO_INPUTAVAILABLE = 0x0004; // Return even if messages are already in queue
        private const uint WAIT_TIMEOUT = 0x102; // Timeout occurred
        private const uint WAIT_OBJECT_0 = 0x00000000; // Object signaled
        private const int RIM_TYPEMOUSE = 0;
        private const int RIM_TYPEKEYBOARD = 1;
        private const int RIDEV_INPUTSINK = 0x00000100;
        
        private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        private const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
        private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
        private const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
        private const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040;
        private const ushort RI_MOUSE_BUTTON_4_UP = 0x0080;
        private const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100;
        private const ushort RI_MOUSE_BUTTON_5_UP = 0x0200;
        private const ushort RI_MOUSE_WHEEL = 0x0400;      // Vertical scroll
        private const ushort RI_MOUSE_HWHEEL = 0x0800;     // Horizontal scroll
        
        // Raw Input Keyboard Flags
        private const ushort RI_KEY_MAKE = 0x0000;         // Key pressed (make)
        private const ushort RI_KEY_BREAK = 0x0001;        // Key released (break)
        private const ushort RI_KEY_E0 = 0x0002;           // Extended key (Right Ctrl, Right Alt, etc.)
        private const ushort RI_KEY_E1 = 0x0004;            // Extended key (Pause key)
        
        [StructLayout(LayoutKind.Sequential)]
        private struct WNDCLASS
        {
            public uint style;
            public WndProcDelegate lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpszClassName;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public System.Drawing.Point pt;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public int dwFlags;
            public IntPtr hwndTarget;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public int dwType;
            public int dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }
        
        // CRITICAL: RAWMOUSE uses a UNION between ulButtons and {usButtonFlags, usButtonData}
        // In C#, we must use LayoutKind.Explicit with FieldOffset to represent the union correctly
        // The union means ulButtons (4 bytes) and {usButtonFlags (2 bytes) + usButtonData (2 bytes)} share the same memory
        // IMPORTANT: There's 2 bytes of padding after usFlags to align the union on 4 bytes
        [StructLayout(LayoutKind.Explicit)]
        private struct RAWMOUSE
        {
            [FieldOffset(0)]
            public ushort usFlags;
            
            // Padding: 2 bytes at offset 2 (for alignment, not exposed)
            
            // UNION at offset 4: Either ulButtons (uint) OR {usButtonFlags (ushort) + usButtonData (ushort)}
            // We use usButtonFlags and usButtonData (the actual fields we need)
            [FieldOffset(4)]
            public ushort usButtonFlags;    // Flags for button events (RI_MOUSE_WHEEL, etc.)
            
            [FieldOffset(6)]
            public ushort usButtonData;     // Wheel delta when RI_MOUSE_WHEEL is set
            
            [FieldOffset(8)]
            public uint ulRawButtons;
            
            [FieldOffset(12)]
            public int lLastX;
            
            [FieldOffset(16)]
            public int lLastY;
            
            [FieldOffset(20)]
            public uint ulExtraInformation;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWKEYBOARD
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public uint ExtraInformation;
        }
        
        [StructLayout(LayoutKind.Explicit)]
        private struct RAWINPUTDATA
        {
            [FieldOffset(0)]
            public RAWMOUSE mouse;
            [FieldOffset(0)]
            public RAWKEYBOARD keyboard;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWINPUTDATA data;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
        
        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,  // Changed to uint to match WS_POPUP
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);
        
        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);
        
        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
        
        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
        
        [DllImport("user32.dll")]
        private static extern uint MsgWaitForMultipleObjectsEx(
            uint nCount,
            IntPtr[] pHandles,
            uint dwMilliseconds,
            uint dwWakeMask,
            uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);
        
        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);
        
        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);
        
        [DllImport("user32.dll")]
        private static extern UIntPtr GetRawInputData(IntPtr hRawInput, int uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        
        #endregion
    }
}
