using System;
using System.Diagnostics;
using System.Drawing; // For Graphics
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamingKeypressOverlay.Input;
using GamingKeypressOverlay.Localization;
using GamingKeypressOverlay.Overlay;
using GamingKeypressOverlay.Settings;

namespace GamingKeypressOverlay.Win32
{
    /// <summary>
    /// Win32 window with Direct2D rendering for ultra-low latency (<1ms)
    /// Replaces XAML/WPF for competitive gaming scenarios
    /// </summary>
    public unsafe class Win32OverlayWindow : IDisposable
    {
        private IntPtr _hwnd;
        private bool _disposed = false;
        private Thread _renderThread;
        private volatile bool _threadRunning = false;
        
        // Renderer
        private Direct2DRenderer _renderer;
        
        // Input state (shared with RawInputThreadManager)
        private unsafe InputState* _inputState;
        
        // Win32 menu
        private IntPtr _contextMenu = IntPtr.Zero;
        
        // Window properties
        private int _width = 1400;
        private int _height = 600;
        private string _title = "Mouse and keyboard Overlay 1.0";
        
        // Store window position before maximizing (to restore to correct monitor)
        private int _savedX = -1;
        private int _savedY = -1;
        private int _savedWidth = -1;
        private int _savedHeight = -1;
        
        // Transparency state for OBS/TikTok Studio capture
        private bool _useLayeredWindow = false;
        private volatile bool _needsRedraw = false;
        
        // Rendering
        private const int TARGET_FPS = 120; // 120fps for ultra-low latency
        private const double FRAME_TIME_MS = 1000.0 / TARGET_FPS;

        // Win32 z-order constants for temporary TopMost toggle
        private const int HWND_TOPMOST = -1;
        private const int HWND_NOTOPMOST = -2;

        // System tray and taskbar icons (Win32 implementation)
        private IntPtr _trayMenuHandle;
        private Icon _keyboardIcon;
        private Icon _mouseIcon;
        private Icon _gamepadIcon;
        private bool _isOverlayVisible = true; // Track overlay visibility state
        private bool _isTrayIconAdded = false;

        public IntPtr Handle => _hwnd;
        public bool IsDisposed => _disposed;
        public Direct2DRenderer Renderer => _renderer;
        
        public unsafe Win32OverlayWindow(InputState* inputState, int width = 1400, int height = 600)
        {
            _inputState = inputState;
            _width = width;
            _height = height;
            
            // Create window on dedicated thread (STA required for Win32)
            _renderThread = new Thread(CreateWindowThread)
            {
                Name = "Win32OverlayThread",
                IsBackground = false,
                Priority = ThreadPriority.Highest
            };
            _renderThread.SetApartmentState(ApartmentState.STA);
            _threadRunning = true;
            _renderThread.Start();
            
            // Wait for window creation
            int waitCount = 0;
            while (_hwnd == IntPtr.Zero && waitCount < 200 && _threadRunning)
            {
                Thread.Sleep(10);
                waitCount++;
            }
            
            if (_hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create Win32 window");
            }
        }
        
        private void CreateWindowThread()
        {
            try
            {
                // Register window class
                WNDCLASS wc = new WNDCLASS
                {
                    lpfnWndProc = WndProc,
                    hInstance = GetModuleHandle(null),
                    lpszClassName = "Win32OverlayClass",
                    style = 0,
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    hIcon = IntPtr.Zero,
                    hCursor = LoadCursor(IntPtr.Zero, IDC_ARROW),
                    hbrBackground = IntPtr.Zero,
                    lpszMenuName = null
                };
                
                ushort atom = RegisterClass(ref wc);
                if (atom == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 0x00000582) // ERROR_CLASS_ALREADY_EXISTS
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to register window class: {error}");
                        return;
                    }
                }
                
                // Create window without WS_EX_LAYERED so right-click works on fresh install.
                // Layered is added only when user enables Transparent (SetWindowTransparent).
                // On some PCs, layered-without-LWA breaks mouse input until LWA is set then removed.
                _hwnd = CreateWindowEx(
                    0,
                    "Win32OverlayClass",
                    _title,
                    WS_POPUP | WS_THICKFRAME, // No title bar, but resizable
                    CW_USEDEFAULT, CW_USEDEFAULT, _width, _height,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    GetModuleHandle(null),
                    IntPtr.Zero
                );
                
                // Enable double buffering to prevent flickering
                SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(GetWindowLong(_hwnd, GWL_EXSTYLE) | WS_EX_COMPOSITED));
                
                if (_hwnd == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to create window");
                    return;
                }
                
                // Show window in normal size (not maximized)
                // Use SW_SHOWNORMAL to ensure window is not maximized
                ShowWindow(_hwnd, SW_SHOWNORMAL);
                UpdateWindow(_hwnd);

                // Temporary TopMost toggle to force focus (Windows blocks focus otherwise)
                // Set TopMost briefly, then remove it after focus is acquired
                SetWindowPos(_hwnd, new IntPtr(HWND_TOPMOST), 0, 0, 0, 0, 0x0001 | 0x0002); // SWP_NOSIZE | SWP_NOMOVE
                SetForegroundWindow(_hwnd);

                // Remove TopMost after a brief delay to avoid permanent TopMost (no async in unsafe context)
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    SetWindowPos(_hwnd, new IntPtr(HWND_NOTOPMOST), 0, 0, 0, 0, 0x0001 | 0x0002); // SWP_NOSIZE | SWP_NOMOVE
                });
                
                // Store instance in window user data
                GCHandle handle = GCHandle.Alloc(this, GCHandleType.Normal);
                SetWindowLongPtr(_hwnd, GWLP_USERDATA, GCHandle.ToIntPtr(handle));
                
                // Initialize Direct2D with the user's free-form color palette.
                var settings = SettingsManager.LoadSettings();
                System.Diagnostics.Debug.WriteLine($"Win32OverlayWindow.CreateWindowThread: Loaded settings - Style={settings.Style}, Layout={settings.KeyboardLayoutType}, GameConfig={settings.GameConfig}, MousePos={settings.MousePosition}");
                OverlayTheme customTheme = StyleManager.GetCustomTheme(settings);
                // Create render context for Direct2D renderer
                var renderContext = new GDIRenderContext
                {
                    CurrentStyle = OverlayStyle.Custom,
                    Theme = customTheme,
                    KeyFont = new System.Drawing.Font("Consolas", 12, System.Drawing.FontStyle.Bold),
                    TitleFont = new System.Drawing.Font("Consolas", 16, System.Drawing.FontStyle.Bold)
                };

                _renderer = new Direct2DRenderer(_hwnd, _width, _height, renderContext.Theme, renderContext);
                
                // Apply saved GameConfig immediately after renderer creation
                if (!string.IsNullOrEmpty(settings.GameConfig) && Enum.TryParse<GameConfig>(settings.GameConfig, ignoreCase: true, out var savedGameConfig))
                {
                    _renderer.SetGameConfig(savedGameConfig);
                    System.Diagnostics.Debug.WriteLine($"Win32OverlayWindow.CreateWindowThread: Applied saved GameConfig = {savedGameConfig}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Win32OverlayWindow.CreateWindowThread: Could not parse GameConfig '{settings.GameConfig}', using default = {GameConfig.General}");
                }
                
                // Apply saved KeyboardLayoutType immediately after renderer creation
                if (!string.IsNullOrEmpty(settings.KeyboardLayoutType) && Enum.TryParse<KeyboardLayoutType>(settings.KeyboardLayoutType, ignoreCase: true, out var savedLayoutType))
                {
                    _renderer.SetLayoutType(savedLayoutType);
                    System.Diagnostics.Debug.WriteLine($"Win32OverlayWindow.CreateWindowThread: Applied saved KeyboardLayoutType = {savedLayoutType}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Win32OverlayWindow.CreateWindowThread: Could not parse KeyboardLayoutType '{settings.KeyboardLayoutType}', using default = {KeyboardLayoutType.QWERTY}");
                }
                
                // Apply saved mouse position immediately after renderer creation
                if (settings.MousePosition == "None")
                {
                    if (_renderer is Direct2DRenderer direct2DRenderer)
                    {
                        direct2DRenderer.SetMouseVisible(false);
                    }
                    System.Diagnostics.Debug.WriteLine($"Win32OverlayWindow.CreateWindowThread: Applied saved MousePosition = None (Hidden)");
                }
                else
                {
                    bool mouseOnRight = settings.MousePosition == "Right";
                    _renderer.SetMousePosition(mouseOnRight);
                    System.Diagnostics.Debug.WriteLine($"Win32OverlayWindow.CreateWindowThread: Applied saved MousePosition = {(mouseOnRight ? "Right" : "Left")}");
                }
                
                // Apply saved mouse style
                if (!string.IsNullOrEmpty(settings.MouseStyle))
                {
                    _renderer.SetMouseStyle(settings.MouseStyle);
                    System.Diagnostics.Debug.WriteLine($"Win32OverlayWindow.CreateWindowThread: Applied saved MouseStyle = {settings.MouseStyle}");
                }

                // Apply saved BackgroundMode (Transparent/Opaque) so it's never lost on style change
                ApplyBackgroundModeFromSettings();

                // Resize window to fit content after applying all settings
                // This ensures both keyboard and mouse are visible
                ResizeWindowToFitContent();
                
                // Create Win32 context menu
                CreateContextMenu();

                // Initialize system tray and taskbar icons
                InitializeTrayIcon();

                // Start render loop
                RenderLoop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Win32 window thread error: {ex.Message}");
            }
        }
        
        
        private void RenderLoop()
        {
            Stopwatch sw = Stopwatch.StartNew();
            long lastFrameTime = 0;
            
            while (_threadRunning && !_disposed)
            {
                long currentTime = sw.ElapsedMilliseconds;
                long elapsed = currentTime - lastFrameTime;
                
                // Render immediately if redraw is needed, otherwise respect FPS limit
                bool shouldRender = _needsRedraw || (elapsed >= (long)FRAME_TIME_MS);
                
                if (shouldRender)
                {
                    if (_useLayeredWindow)
                    {
                        // For layered windows, render directly (no WM_PAINT)
                        if (_hwnd != IntPtr.Zero && _inputState != null && _renderer != null)
                        {
                            var snapshot = _inputState->CreateSnapshot();
                            RECT clientRect;
                            GetClientRect(_hwnd, out clientRect);
                            int width = clientRect.right - clientRect.left;
                            int height = clientRect.bottom - clientRect.top;
                            if (width > 0 && height > 0)
                            {
                                RenderFrameLayered(snapshot, width, height);
                                _needsRedraw = false; // Reset flag after rendering
                                lastFrameTime = currentTime; // Update time after successful render
                            }
                        }
                    }
                    else
                    {
                        // For Direct2D renderer, render directly (no WM_PAINT needed)
                        if (_renderer is Direct2DRenderer)
                        {
                            RenderFrame();
                            _needsRedraw = false; // Reset flag
                            lastFrameTime = currentTime;
                        }
                        else
                        {
                            // For standard windows (GDI), invalidate to trigger WM_PAINT
                            InvalidateRect(_hwnd, IntPtr.Zero, false);
                            _needsRedraw = false; // Reset flag
                            lastFrameTime = currentTime;
                        }
                    }
                }
                
                // Process messages (WM_PAINT will be handled in WndProc for standard mode)
                MSG msg;
                if (PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                {
                    if (msg.message == WM_QUIT)
                    {
                        _threadRunning = false;
                        break;
                    }
                    
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
                else
                {
                    // No messages - sleep to avoid busy-waiting
                    // If redraw is needed, don't sleep (render immediately next iteration)
                    if (!_needsRedraw)
                    {
                        Thread.Sleep(1);
                    }
                }
            }
        }
        
        private unsafe void RenderFrame()
        {
            if (_hwnd == IntPtr.Zero || _inputState == null || _renderer == null) return;
            
            // Get snapshot of input state
            var snapshot = _inputState->CreateSnapshot();
            
            RECT clientRect;
            GetClientRect(_hwnd, out clientRect);
            int width = clientRect.right - clientRect.left;
            int height = clientRect.bottom - clientRect.top;
            
            if (width <= 0 || height <= 0) return;
            
            // Direct2D renderer handles its own rendering with HwndRenderTarget
            // It has built-in double buffering, so we just call Render() directly
            if (_renderer is Direct2DRenderer)
            {
                // Direct2D handles everything - just render
                _renderer.Render(snapshot);
                return;
            }
            
            // Use UpdateLayeredWindow for per-pixel alpha transparency (OBS/TikTok Studio compatible)
            if (_useLayeredWindow)
            {
                RenderFrameLayered(snapshot, width, height);
            }
            else
            {
                // Standard rendering for opaque mode (GDI only)
                PAINTSTRUCT ps;
                IntPtr hdc = BeginPaint(_hwnd, out ps);
                if (hdc != IntPtr.Zero)
                {
                    // Create memory DC for double buffering
                    IntPtr memDC = CreateCompatibleDC(hdc);
                    IntPtr memBitmap = CreateCompatibleBitmap(hdc, width, height);
                    IntPtr oldBitmap = SelectObject(memDC, memBitmap);
                    
                    try
                    {
                        using (Graphics g = Graphics.FromHdc(memDC))
                        {
                    // Clear background - use black for transparent mode
                    // Black will be transparent via SetLayeredWindowAttributes color key
                    g.Clear(Color.Black);
                            
                            // Render main overlay
                            _renderer.Render(snapshot);
                        }
                        
                        // Copy from memory DC to screen
                        BitBlt(hdc, 0, 0, width, height, memDC, 0, 0, SRCCOPY);
                    }
                    finally
                    {
                        SelectObject(memDC, oldBitmap);
                        DeleteObject(memBitmap);
                        DeleteDC(memDC);
                        EndPaint(_hwnd, ref ps);
                    }
                }
            }
        }
        
        /// <summary>
        /// Render frame using UpdateLayeredWindow for per-pixel alpha transparency
        /// This ensures proper transparency capture in OBS and TikTok Studio
        /// </summary>
        private unsafe void RenderFrameLayered(InputStateSnapshot snapshot, int width, int height)
        {
            // Create 32-bit BGRA bitmap with alpha channel
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);
            
            // Create 32-bit bitmap (BGRA format with alpha)
            BITMAPINFO bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                    biWidth = width,
                    biHeight = -height, // Negative height = top-down DIB
                    biPlanes = 1,
                    biBitCount = 32, // 32-bit BGRA
                    biCompression = 0 // BI_RGB
                }
            };
            
            IntPtr bitsPtr;
            IntPtr hBitmap = CreateDIBSection(memDC, ref bmi, 0, out bitsPtr, IntPtr.Zero, 0);
            
            if (hBitmap == IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDC);
                DeleteDC(memDC);
                return;
            }
            
            IntPtr oldBitmap = SelectObject(memDC, hBitmap);
            
            try
            {
                // Render to bitmap with transparent background
                using (Graphics g = Graphics.FromHdc(memDC))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    
                    // Clear with transparent background (alpha = 0)
                    g.Clear(Color.Transparent);
                    
                    // Render main overlay
                    _renderer.Render(snapshot);
                }
                
                // Get window position
                RECT windowRect;
                GetWindowRect(_hwnd, out windowRect);
                POINT windowPos = new POINT { x = windowRect.left, y = windowRect.top };
                POINT sourcePos = new POINT { x = 0, y = 0 };
                SIZE size = new SIZE { cx = width, cy = height };
                
                // Setup blend function for per-pixel alpha
                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255, // Full opacity (per-pixel alpha is in bitmap)
                    AlphaFormat = AC_SRC_ALPHA // Use per-pixel alpha from bitmap
                };
                
                // Update layered window with per-pixel alpha
                UpdateLayeredWindow(_hwnd, screenDC, ref windowPos, ref size, memDC, ref sourcePos, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                SelectObject(memDC, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, screenDC);
            }
        }
        
        private static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_PAINT)
            {
                // Get instance from window user data
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null)
                        {
                            // Only render via WM_PAINT if not in layered window mode
                            // Layered windows use UpdateLayeredWindow and don't use WM_PAINT
                            if (!instance._useLayeredWindow)
                            {
                                instance.RenderFrame();
                            }
                            else
                            {
                                // For layered windows, just validate the rect
                                ValidateRect(hwnd, IntPtr.Zero);
                            }
                            return IntPtr.Zero;
                        }
                    }
                }
            }
            else if (msg == WM_CLOSE)
            {
                // User clicked close button - save settings before closing
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null)
                        {
                            instance.SaveCurrentSettings();
                            instance._disposed = true;
                            instance._threadRunning = false;
                        }
                    }
                }
                // Ensure cursor is visible before closing
                ShowCursor(true);
                DestroyWindow(hwnd);
                return IntPtr.Zero;
            }
            else if (msg == WM_DESTROY)
            {
                // Window is being destroyed - quit message loop
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null)
                        {
                            instance._disposed = true;
                            instance._threadRunning = false;
                        }
                    }
                }
                // Ensure cursor is visible before quitting
                ShowCursor(true);
                PostQuitMessage(0);
                return IntPtr.Zero;
            }
            else if (msg == WM_SIZE)
            {
                // Window resized - update renderer and maintain aspect ratio
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null && instance._renderer != null)
                        {
                            // Get new client size
                            RECT clientRect;
                            GetClientRect(hwnd, out clientRect);
                            int newWidth = clientRect.right - clientRect.left;
                            int newHeight = clientRect.bottom - clientRect.top;
                            
                            if (newWidth > 0 && newHeight > 0)
                            {
                                // Calculate optimal size based on content
                                var (optimalWidth, optimalHeight) = instance._renderer.CalculateRequiredSize();
                                
                                // Calculate aspect ratio
                                double optimalRatio = (double)optimalWidth / optimalHeight;
                                double currentRatio = (double)newWidth / newHeight;
                                
                                // If user is resizing, maintain the optimal ratio
                                // Adjust size to maintain ratio
                                int adjustedWidth = newWidth;
                                int adjustedHeight = newHeight;
                                
                                if (Math.Abs(currentRatio - optimalRatio) > 0.01) // If ratio differs significantly
                                {
                                    // Maintain optimal ratio
                                    if (currentRatio > optimalRatio)
                                    {
                                        // Window is too wide, adjust width
                                        adjustedWidth = (int)(newHeight * optimalRatio);
                                    }
                                    else
                                    {
                                        // Window is too tall, adjust height
                                        adjustedHeight = (int)(newWidth / optimalRatio);
                                    }
                                    
                                    // Resize window to maintain ratio (only if significant difference)
                                    if (Math.Abs(adjustedWidth - newWidth) > 5 || Math.Abs(adjustedHeight - newHeight) > 5)
                                    {
                                        RECT windowRect;
                                        GetWindowRect(hwnd, out windowRect);
                                        int borderWidth = (windowRect.right - windowRect.left) - newWidth;
                                        int borderHeight = (windowRect.bottom - windowRect.top) - newHeight;
                                        
                                        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 
                                            adjustedWidth + borderWidth, 
                                            adjustedHeight + borderHeight,
                                            0x0001 | 0x0004); // SWP_NOMOVE | SWP_NOZORDER
                                        
                                        newWidth = adjustedWidth;
                                        newHeight = adjustedHeight;
                                    }
                                }
                                
                                // Resize Direct2D renderer
                                if (instance._renderer is Direct2DRenderer direct2DRenderer)
                                {
                                    direct2DRenderer.Resize(newWidth, newHeight);
                                }
                                
                                // Invalidate to redraw
                                InvalidateRect(hwnd, IntPtr.Zero, true);
                                
                                // Save window size to settings
                                instance.SaveCurrentSettings();
                            }
                        }
                    }
                }
            }
            else if (msg == WM_CONTEXTMENU)
            {
                // Right-click context menu
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null && instance._contextMenu != IntPtr.Zero)
                        {
                            POINT pt;
                            
                            // If lParam is -1, menu was triggered by keyboard (Shift+F10)
                            // Use current cursor position instead
                            if (lParam.ToInt32() == -1)
                            {
                                GetCursorPos(out pt);
                            }
                            else
                            {
                                // lParam contains screen coordinates directly for WM_CONTEXTMENU
                                pt.x = (short)(lParam.ToInt32() & 0xFFFF);
                                pt.y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
                            }
                            
                            // Set foreground window to ensure menu messages are received
                            SetForegroundWindow(hwnd);
                            
                            // Show context menu at cursor position
                            TrackPopupMenu(instance._contextMenu, TPM_LEFTALIGN | TPM_TOPALIGN | TPM_RIGHTBUTTON,
                                pt.x, pt.y, 0, hwnd, IntPtr.Zero);
                        }
                    }
                }
                return IntPtr.Zero;
            }
            else if (msg == WM_TRAY_ICON)
            {
                // Handle tray icon messages
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null)
                        {
                            int mouseMsg = (int)lParam;
                            if (mouseMsg == WM_LBUTTONDBLCLK)
                            {
                                // Double-click tray: only SHOW overlay (never hide). Prevents "click → app closes" confusion.
                                // User hides via tray menu "Hide Overlay".
                                instance.ShowOverlayIfHidden();
                            }
                            else if (mouseMsg == WM_RBUTTONUP)
                            {
                                instance.ShowTrayMenu();
                            }
                        }
                    }
                }
                return IntPtr.Zero;
            }
            else if (msg == WM_COMMAND)
            {
                // Handle menu item selection
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null && instance._renderer != null)
                        {
                            // Extract menu ID from LOWORD of wParam
                            int menuId = (int)(wParam.ToInt32() & 0xFFFF);
                            System.Diagnostics.Debug.WriteLine($"WM_COMMAND received: menuId={menuId}, wParam=0x{wParam.ToInt32():X8}");
                            instance.HandleMenuCommand(menuId);
                        }
                    }
                }
                return IntPtr.Zero;
            }
            else if (msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP || msg == WM_NCRBUTTONDOWN || msg == WM_NCRBUTTONUP)
            {
                // Handle right-click to show context menu
                // WM_NCRBUTTON* is sent when clicking on non-client area (which includes HTCAPTION)
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null && instance._contextMenu != IntPtr.Zero && 
                            (msg == WM_RBUTTONUP || msg == WM_NCRBUTTONUP))
                        {
                            POINT pt;
                            GetCursorPos(out pt);
                            
                            // Set foreground window to ensure menu messages are received
                            SetForegroundWindow(hwnd);
                            
                            // Show context menu at cursor position
                            TrackPopupMenu(instance._contextMenu, TPM_LEFTALIGN | TPM_TOPALIGN | TPM_RIGHTBUTTON,
                                pt.x, pt.y, 0, hwnd, IntPtr.Zero);
                            return IntPtr.Zero;
                        }
                    }
                }
            }
            else if (msg == WM_NCHITTEST)
            {
                // Get instance to check if transparent mode is enabled
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                bool isTransparent = false;
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null && instance._renderer != null)
                        {
                            // Check if transparent background is enabled
                            isTransparent = instance._renderer.IsTransparentBackgroundEnabled();
                        }
                    }
                }
                
                // Allow window to be moved by clicking and holding anywhere (since there's no title bar)
                // But preserve resize handles on borders
                IntPtr result = DefWindowProc(hwnd, msg, wParam, lParam);
                int hitTest = result.ToInt32();
                
                // If transparent mode is enabled, check if clicked pixel is black (transparent)
                // This allows click-through for transparent areas while keeping visible elements interactive
                if (isTransparent && hitTest == HTCLIENT)
                {
                    // Get click position
                    int x = (short)(lParam.ToInt32() & 0xFFFF);
                    int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
                    
                    // Convert screen coordinates to client coordinates
                    POINT pt = new POINT { x = x, y = y };
                    ScreenToClient(hwnd, ref pt);
                    
                    // Get pixel color at click position
                    IntPtr hdc = GetDC(hwnd);
                    if (hdc != IntPtr.Zero)
                    {
                        uint pixel = GetPixel(hdc, pt.x, pt.y);
                        ReleaseDC(hwnd, hdc);
                        
                        // Extract RGB values (GetPixel returns COLORREF: 0x00BBGGRR)
                        byte r = (byte)(pixel & 0xFF);
                        byte g = (byte)((pixel >> 8) & 0xFF);
                        byte b = (byte)((pixel >> 16) & 0xFF);
                        
                        // If pixel is black (transparent), allow click-through
                        if (r == 0 && g == 0 && b == 0)
                        {
                            return new IntPtr(HTTRANSPARENT);
                        }
                    }
                }
                
                // If it's the client area (not a border), treat it as caption for dragging
                // This allows you to always drag the window, even in transparent mode
                if (hitTest == HTCLIENT)
                {
                    return new IntPtr(HTCAPTION); // Treat client area as caption for dragging
                }
                
                // Otherwise, return the original result (borders for resizing, etc.)
                return result;
            }
            else if (msg == WM_NCLBUTTONDBLCLK)
            {
                // Double-click on caption area (which is now the whole client area) - toggle maximize/restore
                IntPtr userData = GetWindowLongPtr(hwnd, GWLP_USERDATA);
                if (userData != IntPtr.Zero)
                {
                    GCHandle handle = GCHandle.FromIntPtr(userData);
                    if (handle.IsAllocated)
                    {
                        var instance = handle.Target as Win32OverlayWindow;
                        if (instance != null)
                        {
                            instance.ToggleMaximize();
                            return IntPtr.Zero;
                        }
                    }
                }
            }
            
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }
        
        public void Show()
        {
            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SW_SHOW);
            }
        }
        
        public void Hide()
        {
            if (_hwnd != IntPtr.Zero)
            {
                ShowWindow(_hwnd, SW_HIDE);
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _threadRunning = false;
            
            if (_hwnd != IntPtr.Zero)
            {
                PostMessage(_hwnd, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }
            
            if (_renderThread != null && _renderThread.IsAlive)
            {
                if (!_renderThread.Join(1000))
                {
                    System.Diagnostics.Debug.WriteLine("Win32 render thread did not stop gracefully");
                }
            }
            
            // Cleanup renderer and menu
            _renderer?.Dispose();
            if (_contextMenu != IntPtr.Zero)
            {
                DestroyMenu(_contextMenu);
                _contextMenu = IntPtr.Zero;
            }

            // Cleanup tray icon and menu (Win32)
            RemoveTrayIcon();

            if (_trayMenuHandle != IntPtr.Zero)
            {
                DestroyMenu(_trayMenuHandle);
                _trayMenuHandle = IntPtr.Zero;
            }

            // Cleanup icons
            _keyboardIcon?.Dispose();
            _mouseIcon?.Dispose();
            _gamepadIcon?.Dispose();
        }
        
        /// <summary>
        /// Appends overlay menu content (personalization, layout, mouse, about, exit) to parent.
        /// Shared by overlay context menu and tray menu.
        /// </summary>
        private void AppendOverlayMenuContent(IntPtr parent)
        {
            var settings = SettingsManager.LoadSettings();

            IntPtr personalizationMenu = CreatePopupMenu();
            AppendMenu(personalizationMenu, MF_STRING, 8505, UiText.Get("Colors and presets (HEX)...", "Couleurs et palettes (HEX)..."));
            AppendMenu(personalizationMenu, MF_SEPARATOR, 0, null);
            AppendMenu(personalizationMenu, MF_STRING, 8502,
                settings.UseAnimatedBackground
                    ? UiText.Get("Animated Background (ON)", "Arrière-plan animé (ACTIF)")
                    : UiText.Get("Animated Background (OFF)", "Arrière-plan animé (INACTIF)"));
            AppendMenu(personalizationMenu, MF_STRING, 8503,
                string.IsNullOrWhiteSpace(settings.CustomLogoPath)
                    ? UiText.Get("Add Logo...", "Ajouter un logo...")
                    : UiText.Get("Change Logo...", "Changer le logo..."));
            if (!string.IsNullOrWhiteSpace(settings.CustomLogoPath))
                AppendMenu(personalizationMenu, MF_STRING, 8504, UiText.Get("Remove Logo", "Retirer le logo"));
            AppendMenu(parent, MF_POPUP, (IntPtr)personalizationMenu, UiText.Get("Personalization", "Personnalisation"));

            IntPtr layoutMenu = CreatePopupMenu();
            AppendMenu(layoutMenu, MF_STRING, 3001, "QWERTY");
            AppendMenu(layoutMenu, MF_STRING, 3002, "AZERTY");
            AppendMenu(layoutMenu, MF_STRING, 3003, "QWERTZ");
            AppendMenu(parent, MF_POPUP, (IntPtr)layoutMenu, UiText.Get("Layout Type", "Disposition des touches"));

            IntPtr gameConfigMenu = CreatePopupMenu();
            AppendMenu(gameConfigMenu, MF_STRING, 2001, "FPS");
            AppendMenu(gameConfigMenu, MF_STRING, 2002, "MMO");
            AppendMenu(gameConfigMenu, MF_STRING, 2003, "MOBA");
            AppendMenu(gameConfigMenu, MF_STRING, 2004, UiText.Get("Racing", "Course"));
            AppendMenu(gameConfigMenu, MF_STRING, 2005, UiText.Get("Survival", "Survie"));
            AppendMenu(gameConfigMenu, MF_STRING, 2006, UiText.Get("General", "Général"));
            AppendMenu(parent, MF_POPUP, (IntPtr)gameConfigMenu, UiText.Get("Keyboard Preset", "Préréglage clavier"));

            IntPtr mousePosMenu = CreatePopupMenu();
            AppendMenu(mousePosMenu, MF_STRING, 7001, UiText.Get("Left", "Gauche"));
            AppendMenu(mousePosMenu, MF_STRING, 7002, UiText.Get("Right", "Droite"));
            AppendMenu(mousePosMenu, MF_STRING, 7003, UiText.Get("Hidden", "Masquée"));
            AppendMenu(parent, MF_POPUP, (IntPtr)mousePosMenu, UiText.Get("Mouse Position", "Position de la souris"));

            IntPtr mouseStyleMenu = CreatePopupMenu();
            AppendMenu(mouseStyleMenu, MF_STRING, 7101, "Gaming");
            AppendMenu(mouseStyleMenu, MF_STRING, 7104, UiText.Get("Minimal", "Minimaliste"));
            AppendMenu(mouseStyleMenu, MF_STRING, 7103, UiText.Get("None", "Aucune"));
            AppendMenu(parent, MF_POPUP, (IntPtr)mouseStyleMenu, UiText.Get("Mouse Style", "Style de souris"));

            AppendMenu(parent, MF_SEPARATOR, IntPtr.Zero, null);
            AppendMenu(parent, MF_STRING, 8001, UiText.Get("About", "À propos"));
            AppendMenu(parent, MF_SEPARATOR, IntPtr.Zero, null);
            AppendMenu(parent, MF_STRING, 6001, UiText.Get("Exit", "Quitter"));
        }

        private void CreateContextMenu()
        {
            _contextMenu = CreatePopupMenu();
            AppendOverlayMenuContent(_contextMenu);
        }

        private void InitializeTrayIcon()
        {
            try
            {
                // Create icons using our helper
                _keyboardIcon = TrayIconHelper.CreateKeyboardIcon();
                _mouseIcon = TrayIconHelper.CreateMouseIcon();
                _gamepadIcon = TrayIconHelper.CreateGamepadIcon();

                // Tray menu: Show/Hide, separator, full overlay content (Style, Layout, etc.), separator, Toggle Transparent only
                _trayMenuHandle = CreatePopupMenu();
                AppendMenu(_trayMenuHandle, MF_STRING, ID_TRAY_SHOW_HIDE, _isOverlayVisible
                    ? UiText.Get("Hide Overlay", "Masquer l’overlay")
                    : UiText.Get("Show Overlay", "Afficher l’overlay"));
                AppendMenu(_trayMenuHandle, MF_SEPARATOR, 0, null);
                AppendOverlayMenuContent(_trayMenuHandle);
                AppendMenu(_trayMenuHandle, MF_SEPARATOR, 0, null);
                AppendMenu(_trayMenuHandle, MF_STRING, ID_TRAY_TRANSPARENT,
                    UiText.Get("Toggle Transparency", "Basculer la transparence"));

                // Add tray icon
                var notifyIconData = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwnd,
                    uID = 1,
                    uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                    uCallbackMessage = WM_TRAY_ICON,
                    hIcon = _keyboardIcon.Handle,
                    szTip = "Mouse and keyboard Overlay 1.0"
                };

                if (Shell_NotifyIcon(NIM_ADD, ref notifyIconData))
                {
                    _isTrayIconAdded = true;
                    // Set initial taskbar visibility (hidden by default for gaming)
                    UpdateTaskbarVisibility(false);
                    System.Diagnostics.Debug.WriteLine("Tray icon initialized successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to add tray icon");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize tray icon: {ex.Message}");
            }
        }

        /// <summary>
        /// Double-click tray: show overlay only if hidden. Never hide on double-click.
        /// </summary>
        private void ShowOverlayIfHidden()
        {
            if (_isOverlayVisible)
            {
                // Already visible – bring to front
                SetForegroundWindow(_hwnd);
                return;
            }
            _isOverlayVisible = true;
            ShowWindow(_hwnd, SW_SHOW);
            UpdateTaskbarVisibility(true);
            UpdateTrayIconTip("Mouse and keyboard Overlay 1.0 (Visible)");
            ModifyMenu(_trayMenuHandle, (uint)ID_TRAY_SHOW_HIDE, MF_BYCOMMAND | MF_STRING,
                new IntPtr(ID_TRAY_SHOW_HIDE), UiText.Get("Hide Overlay", "Masquer l’overlay"));
            SetForegroundWindow(_hwnd);
        }

        private void ToggleOverlayVisibility()
        {
            _isOverlayVisible = !_isOverlayVisible;

            if (_isOverlayVisible)
            {
                // Show overlay window
                ShowWindow(_hwnd, SW_SHOW);
                UpdateTaskbarVisibility(true); // Show in taskbar when visible
                UpdateTrayIconTip("Mouse and keyboard Overlay 1.0 (Visible)");
            }
            else
            {
                // Hide overlay window
                ShowWindow(_hwnd, SW_HIDE);
                UpdateTaskbarVisibility(false); // Hide from taskbar when hidden
                UpdateTrayIconTip("Mouse and keyboard Overlay 1.0 (Hidden)");
            }

            // Update tray menu
            ModifyMenu(_trayMenuHandle, (uint)ID_TRAY_SHOW_HIDE, MF_BYCOMMAND | MF_STRING,
                new IntPtr(ID_TRAY_SHOW_HIDE), _isOverlayVisible
                    ? UiText.Get("Hide Overlay", "Masquer l’overlay")
                    : UiText.Get("Show Overlay", "Afficher l’overlay"));
        }

        private void UpdateTrayIconTip(string tip)
        {
            if (_isTrayIconAdded && _hwnd != IntPtr.Zero)
            {
                var notifyIconData = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwnd,
                    uID = 1,
                    uFlags = NIF_TIP,
                    szTip = tip
                };
                Shell_NotifyIcon(NIM_MODIFY, ref notifyIconData);
            }
        }

        private void UpdateTaskbarVisibility(bool visible)
        {
            if (_hwnd != IntPtr.Zero)
            {
                int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
                uint uStyle = (uint)exStyle;
                if (visible)
                    uStyle &= ~WS_EX_TOOLWINDOW;
                else
                    uStyle |= WS_EX_TOOLWINDOW;
                SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr((int)uStyle));
            }
        }

        private void ToggleTransparentMode()
        {
            if (_renderer != null)
            {
                SetWindowTransparent(!_renderer.IsTransparentBackgroundEnabled());
                var s = SettingsManager.LoadSettings();
                s.BackgroundMode = _renderer.IsTransparentBackgroundEnabled() ? "Transparent" : "Opaque";
                SettingsManager.SaveSettings(s);
                _needsRedraw = true;
            }
        }

        private void ShowSettings()
        {
            // TODO: Implement settings dialog
            System.Diagnostics.Debug.WriteLine("Settings dialog not yet implemented");
        }

        private void ShowAbout()
        {
            // TODO: Implement about dialog
            System.Diagnostics.Debug.WriteLine("About dialog not yet implemented");
        }

        private void ExitApplication()
        {
            // Signal application to exit
            _threadRunning = false;
            RemoveTrayIcon();
        }

        private void RemoveTrayIcon()
        {
            if (_isTrayIconAdded && _hwnd != IntPtr.Zero)
            {
                var notifyIconData = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwnd,
                    uID = 1
                };
                Shell_NotifyIcon(NIM_DELETE, ref notifyIconData);
                _isTrayIconAdded = false;
            }
        }

        private void ShowTrayMenu()
        {
            if (_trayMenuHandle != IntPtr.Zero && _hwnd != IntPtr.Zero)
            {
                GetCursorPos(out var cursorPos);
                TrackPopupMenu(_trayMenuHandle, 0x0000 | 0x0010, cursorPos.x, cursorPos.y, 0, _hwnd, IntPtr.Zero);
            }
        }
        
        private void HandleMenuCommand(int menuId)
        {
            System.Diagnostics.Debug.WriteLine($"HandleMenuCommand: menuId={menuId}");

            // Tray-only commands (5001, 5002)
            switch (menuId)
            {
                case ID_TRAY_SHOW_HIDE:
                    ToggleOverlayVisibility();
                    return;
                case ID_TRAY_TRANSPARENT:
                    ToggleTransparentMode();
                    return;
            }

            if (_renderer == null)
            {
                System.Diagnostics.Debug.WriteLine($"HandleMenuCommand: _renderer is null");
                return;
            }
            
            // Game Config / Keyboard Layout (2001-2006)
            if (menuId == 2001)
            {
                _renderer.SetGameConfig(GameConfig.FPS);
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            else if (menuId == 2002)
            {
                _renderer.SetGameConfig(GameConfig.MMO);
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            else if (menuId == 2003)
            {
                _renderer.SetGameConfig(GameConfig.MOBA);
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            else if (menuId == 2004)
            {
                _renderer.SetGameConfig(GameConfig.Racing);
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            else if (menuId == 2005)
            {
                _renderer.SetGameConfig(GameConfig.Survival);
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            else if (menuId == 2006)
            {
                _renderer.SetGameConfig(GameConfig.General);
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            // Layout Type (3001-3003)
            else if (menuId == 3001)
            {
                _renderer.SetLayoutType(KeyboardLayoutType.QWERTY);
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            else if (menuId == 3002)
            {
                _renderer.SetLayoutType(KeyboardLayoutType.AZERTY);
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            else if (menuId == 3003)
            {
                _renderer.SetLayoutType(KeyboardLayoutType.QWERTZ);
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            // Mouse Position (7001-7003)
            else if (menuId == 7001)
            {
                System.Diagnostics.Debug.WriteLine("Setting Mouse Position to LEFT");
                _renderer.SetMousePosition(false); // false = left
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            else if (menuId == 7002)
            {
                System.Diagnostics.Debug.WriteLine("Setting Mouse Position to RIGHT");
                _renderer.SetMousePosition(true); // true = right
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                if (_renderer is Direct2DRenderer)
                {
                    InvalidateRect(_hwnd, IntPtr.Zero, false);
                }
            }
            else if (menuId == 7003)
            {
                System.Diagnostics.Debug.WriteLine("Setting Mouse Position to NONE (Hidden)");
                if (_renderer is Direct2DRenderer direct2DRenderer)
                {
                    direct2DRenderer.SetMouseVisible(false);
                }
                ResizeWindowToFitContent();
                _needsRedraw = true;
                SaveCurrentSettings();
                // Force immediate redraw for Direct2D
                InvalidateRect(_hwnd, IntPtr.Zero, false);
            }
            // Mouse Style (7101-7103)
            else if (menuId == 7101)
            {
                _renderer.SetMouseStyle("Gaming");
                var settings = SettingsManager.LoadSettings();
                settings.MouseStyle = "Gaming";
                SettingsManager.SaveSettings(settings);
                ResizeWindowToFitContent();
                _needsRedraw = true;
            }
            else if (menuId == 7104)
            {
                _renderer.SetMouseStyle("Minimal");
                var settings = SettingsManager.LoadSettings();
                settings.MouseStyle = "Minimal";
                SettingsManager.SaveSettings(settings);
                ResizeWindowToFitContent();
                _needsRedraw = true;
            }
            else if (menuId == 7103)
            {
                _renderer.SetMouseStyle("None");
                var settings = SettingsManager.LoadSettings();
                settings.MouseStyle = "None";
                SettingsManager.SaveSettings(settings);
                ResizeWindowToFitContent();
                _needsRedraw = true;
            }
            // About (8001)
            else if (menuId == 8001)
            {
                ShowAboutDialog();
            }
            // Personalization features are free and available to everyone.
            else if (menuId == 8502)
            {
                var settings = SettingsManager.LoadSettings();
                settings.UseAnimatedBackground = !settings.UseAnimatedBackground;
                SettingsManager.SaveSettings(settings);
                _renderer?.SetAnimatedBackground(settings.UseAnimatedBackground);
                
                if (_contextMenu != IntPtr.Zero)
                {
                    DestroyMenu(_contextMenu);
                    _contextMenu = IntPtr.Zero;
                }
                CreateContextMenu();
                _needsRedraw = true;
            }
            else if (menuId == 8503)
            {
                var settings = SettingsManager.LoadSettings();
                string filePath = ShowOpenFileDialog("Select Custom Logo", "Image Files (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0All Files (*.*)\0*.*\0");
                
                if (!string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath))
                {
                    settings.CustomLogoPath = filePath;
                    SettingsManager.SaveSettings(settings);
                    _renderer?.SetCustomLogo(filePath);
                    if (_contextMenu != IntPtr.Zero)
                    {
                        DestroyMenu(_contextMenu);
                        _contextMenu = IntPtr.Zero;
                    }
                    CreateContextMenu();
                    _needsRedraw = true;
                }
            }
            else if (menuId == 8504)
            {
                var settings = SettingsManager.LoadSettings();
                settings.CustomLogoPath = "";
                SettingsManager.SaveSettings(settings);
                _renderer?.SetCustomLogo("");
                if (_contextMenu != IntPtr.Zero)
                {
                    DestroyMenu(_contextMenu);
                    _contextMenu = IntPtr.Zero;
                }
                CreateContextMenu();
                _needsRedraw = true;
            }
            else if (menuId == 8505)
            {
                ShowCustomColorDialog();
            }
            // Exit (6001)
            else if (menuId == 6001)
            {
                PostMessage(_hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            }
        }
        
        private void ShowAboutDialog()
        {
            string aboutText = UiText.Get(
                "Keyboard & Mouse Overlay 1.0\n\n" +
                "Free and open-source software by Marc Gauthier.\n\n" +
                "Visualize keyboard and mouse input in real time for\n" +
                "streaming, tutorials, presentations, training, accessibility,\n" +
                "demonstrations, and games.\n\n" +
                "Includes automatic English/French display, matching color\n" +
                "presets, full HEX customization, and low-latency rendering.\n\n" +
                "github.com/marcgauthier0/keyboard-mouse-overlay\n" +
                "marc@mgnetworks.ca",
                "Keyboard & Mouse Overlay 1.0\n\n" +
                "Logiciel libre et open source par Marc Gauthier.\n\n" +
                "Visualisez le clavier et la souris en temps réel pour la diffusion,\n" +
                "les tutoriels, présentations, formations, démonstrations,\n" +
                "l’accessibilité et les jeux.\n\n" +
                "Comprend l’affichage automatique français/anglais, des palettes\n" +
                "assorties, la personnalisation HEX et un rendu à faible latence.\n\n" +
                "github.com/marcgauthier0/keyboard-mouse-overlay\n" +
                "marc@mgnetworks.ca");
            
            MessageBox(_hwnd, aboutText, "Keyboard & Mouse Overlay",
                MB_OK | MB_ICONINFORMATION);
        }
        
        private void ShowCustomColorDialog()
        {
            var settings = SettingsManager.LoadSettings();
            using var dialog = new ColorCustomizationDialog(settings);
            if (dialog.ShowDialog(new NativeWindowOwner(_hwnd)) == System.Windows.Forms.DialogResult.OK)
            {
                SettingsManager.SaveSettings(settings);
                _renderer.SetTheme(StyleManager.GetCustomTheme(settings));
                _needsRedraw = true;
                InvalidateRect(_hwnd, IntPtr.Zero, false);
            }
        }
        
        /// <summary>
        /// Re-apply BackgroundMode from settings (Transparent/Opaque).
        /// Call when changing style so background preference is never lost.
        /// </summary>
        private void ApplyBackgroundModeFromSettings()
        {
            var s = SettingsManager.LoadSettings();
            bool transparent = string.Equals(s.BackgroundMode, "Transparent", StringComparison.OrdinalIgnoreCase);
            SetWindowTransparent(transparent);
        }

        private void SetWindowTransparent(bool transparent)
        {
            if (_hwnd == IntPtr.Zero) return;
            
            // Get current extended window style
            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            
            if (transparent)
            {
                // Enable layered window with color key transparency
                // Use black (RGB 0,0,0) as the transparent color
                exStyle |= (int)WS_EX_LAYERED;
                SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
                
                // Use SetLayeredWindowAttributes with color key for transparency
                // This is simpler and works better with Windows messages
                // Black pixels (RGB 0,0,0) will be transparent
                SetLayeredWindowAttributes(_hwnd, 0x000000, 255, LWA_COLORKEY | LWA_ALPHA);
                
                // Use standard rendering mode (not layered window mode)
                _useLayeredWindow = false;
                
                // Render background as black (which will be transparent)
                if (_renderer != null)
                {
                    _renderer.SetTransparentBackground(true);
                }
                
                // Force immediate redraw
                _needsRedraw = true;
            }
            else
            {
                // Disable transparent mode, return to standard rendering
                _useLayeredWindow = false;
                
                // Disable transparent background in renderer
                if (_renderer != null)
                {
                    _renderer.SetTransparentBackground(false);
                }
                
                // Remove WS_EX_LAYERED to use standard window rendering
                exStyle &= ~(int)WS_EX_LAYERED;
                SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(exStyle));
                
                // Force immediate redraw with standard rendering
                _needsRedraw = true;
            }
        }
        
        /// <summary>
        /// Resize window to fit keyboard + mouse layout
        /// Simple fit - just resize, keep position, only adjust if window goes off-screen
        /// </summary>
        public void ResizeWindowToFitContent()
        {
            if (_renderer == null || _hwnd == IntPtr.Zero) return;
            
            var (totalWidth, totalHeight) = _renderer.CalculateRequiredSize();
            
            // Get current window position
            RECT windowRect;
            GetWindowRect(_hwnd, out windowRect);
            
            // Get window border sizes (title bar, borders)
            RECT clientRect;
            GetClientRect(_hwnd, out clientRect);
            int borderWidth = (windowRect.right - windowRect.left) - (clientRect.right - clientRect.left);
            int borderHeight = (windowRect.bottom - windowRect.top) - (clientRect.bottom - clientRect.top);
            
            int newWidth = totalWidth + borderWidth;
            int newHeight = totalHeight + borderHeight;
            
            // Keep current position - don't move window to avoid changing monitors
            // Only resize, don't reposition unless absolutely necessary
            int newX = windowRect.left;
            int newY = windowRect.top;
            
            // Keep window on current monitor - don't move it unless absolutely necessary
            // Only adjust position if window would go off-screen, but try to keep it on same monitor
            // For multi-monitor setups, we'll use a simpler approach: keep current position
            // and only adjust if window extends beyond reasonable bounds
            
            // Simple approach: keep current position, only adjust if window goes way off-screen
            // This prevents moving to another monitor
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);
            
            // Only adjust if window would go significantly off the primary screen
            // This is a conservative check to avoid moving to another monitor
            bool needsAdjustment = false;
            
            if (newX + newWidth > screenWidth + 100) // Allow some overflow for multi-monitor
            {
                newX = Math.Max(0, screenWidth - newWidth);
                needsAdjustment = true;
            }
            if (newX < -100) // Allow some negative for multi-monitor
            {
                newX = 0;
                needsAdjustment = true;
            }
            
            if (newY + newHeight > screenHeight + 100)
            {
                newY = Math.Max(0, screenHeight - newHeight);
                needsAdjustment = true;
            }
            if (newY < -100)
            {
                newY = 0;
                needsAdjustment = true;
            }
            
            // If no adjustment needed, keep original position (don't move)
            if (!needsAdjustment)
            {
                newX = windowRect.left;
                newY = windowRect.top;
            }
            
            // Resize window, only move if position actually changed
            uint flags = 0x0000; // SWP_NOZORDER
            if (newX == windowRect.left && newY == windowRect.top)
            {
                flags |= 0x0002; // SWP_NOMOVE - don't move window
            }
            
            SetWindowPos(_hwnd, IntPtr.Zero, 
                newX, newY,
                newWidth, newHeight,
                flags);
        }
        
        /// <summary>
        /// Toggle maximize/restore on double-click
        /// </summary>
        private void ToggleMaximize()
        {
            if (_hwnd == IntPtr.Zero) return;
            
            // Use IsZoomed to properly detect if window is maximized
            bool isMaximized = IsZoomed(_hwnd);
            
            if (isMaximized)
            {
                // Restore to normal size - resize to fit content (keyboard + mouse layout)
                ShowWindow(_hwnd, SW_RESTORE);
                
                // Restore saved position if available (to keep window on same monitor)
                if (_savedX >= 0 && _savedY >= 0 && _savedWidth > 0 && _savedHeight > 0)
                {
                    SetWindowPos(_hwnd, IntPtr.Zero, _savedX, _savedY, _savedWidth, _savedHeight, 0x0000);
                    _savedX = -1; // Clear saved position
                    _savedY = -1;
                    _savedWidth = -1;
                    _savedHeight = -1;
                }
                else
                {
                    // Recalculate and resize to fit current layout
                    ResizeWindowToFitContent();
                }
            }
            else
            {
                // Save current position and size before maximizing
                RECT windowRect;
                GetWindowRect(_hwnd, out windowRect);
                _savedX = windowRect.left;
                _savedY = windowRect.top;
                _savedWidth = windowRect.right - windowRect.left;
                _savedHeight = windowRect.bottom - windowRect.top;
                
                // Maximize to full screen
                ShowWindow(_hwnd, SW_MAXIMIZE);
            }
        }
        
        /// <summary>
        /// Save current settings to disk
        /// </summary>
        private void SaveCurrentSettings()
        {
            if (_renderer == null)
            {
                System.Diagnostics.Debug.WriteLine("SaveCurrentSettings: _renderer is null, cannot save");
                return;
            }
            
            try
            {
                var settings = SettingsManager.LoadSettings();
                
                settings.Style = "Custom";
                
                // Save current layout type
                string layoutString = _renderer.CurrentLayoutType.ToString();
                settings.KeyboardLayoutType = layoutString;
                System.Diagnostics.Debug.WriteLine($"SaveCurrentSettings: Saving layout = {layoutString}");
                
                // Save current game config
                string gameConfigString = _renderer.CurrentGameConfig.ToString();
                settings.GameConfig = gameConfigString;
                System.Diagnostics.Debug.WriteLine($"SaveCurrentSettings: Saving game config = {gameConfigString}");
                
                // Save mouse position
                string mousePosString;
                if (_renderer is Direct2DRenderer direct2DRenderer && !direct2DRenderer.MouseVisible)
                {
                    mousePosString = "None";
                }
                else
                {
                    mousePosString = _renderer.MouseOnRight ? "Right" : "Left";
                }
                settings.MousePosition = mousePosString;
                System.Diagnostics.Debug.WriteLine($"SaveCurrentSettings: Saving mouse position = {mousePosString}");

                // Save BackgroundMode (Transparent/Opaque)
                settings.BackgroundMode = _renderer.IsTransparentBackgroundEnabled() ? "Transparent" : "Opaque";

                // Save mouse style (already saved when changed via menu)
                // Just ensure it's preserved from current settings
                var currentSettings = SettingsManager.LoadSettings();
                if (!string.IsNullOrEmpty(currentSettings.MouseStyle))
                {
                    settings.MouseStyle = currentSettings.MouseStyle;
                }
                else
                {
                    settings.MouseStyle = "Gaming"; // Default
                }
                
                // Save window size (only if not maximized - save the "normal" size that fits content)
                if (_hwnd != IntPtr.Zero && !IsZoomed(_hwnd))
                {
                    RECT windowRect;
                    GetWindowRect(_hwnd, out windowRect);
                    settings.WindowWidth = windowRect.right - windowRect.left;
                    settings.WindowHeight = windowRect.bottom - windowRect.top;
                }
                else if (_hwnd != IntPtr.Zero)
                {
                    // If maximized, calculate and save the size that fits content
                    var (totalWidth, totalHeight) = _renderer.CalculateRequiredSize();
                    RECT clientRect;
                    GetClientRect(_hwnd, out clientRect);
                    RECT windowRect;
                    GetWindowRect(_hwnd, out windowRect);
                    int borderWidth = (windowRect.right - windowRect.left) - (clientRect.right - clientRect.left);
                    int borderHeight = (windowRect.bottom - windowRect.top) - (clientRect.bottom - clientRect.top);
                    settings.WindowWidth = totalWidth + borderWidth;
                    settings.WindowHeight = totalHeight + borderHeight;
                }
                
                // Save to disk
                SettingsManager.SaveSettings(settings);
                System.Diagnostics.Debug.WriteLine($"SaveCurrentSettings: Settings saved successfully - Style={settings.Style}, Layout={settings.KeyboardLayoutType}, GameConfig={settings.GameConfig}, MousePos={settings.MousePosition}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveCurrentSettings: Failed to save settings: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        #region Win32 API
        
        // File Dialog (GetOpenFileName)
        [DllImport("comdlg32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName(ref OPENFILENAME ofn);
        
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
        }
        
        private string ShowOpenFileDialog(string title, string filter)
        {
            OPENFILENAME ofn = new OPENFILENAME();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.hwndOwner = _hwnd;
            ofn.lpstrTitle = title;
            ofn.lpstrFilter = filter;
            ofn.nFilterIndex = 1;
            ofn.lpstrFile = new string(new char[260]);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.Flags = 0x00080000 | 0x00001000; // OFN_EXPLORER | OFN_FILEMUSTEXIST
            
            if (GetOpenFileName(ref ofn))
            {
                // GetOpenFileName modifies lpstrFile in place, need to handle null-terminated string
                int nullIndex = ofn.lpstrFile.IndexOf('\0');
                if (nullIndex >= 0)
                {
                    return ofn.lpstrFile.Substring(0, nullIndex);
                }
                return ofn.lpstrFile;
            }
            return null;
        }
        
        private const uint WS_OVERLAPPED = 0x00000000;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_THICKFRAME = 0x00040000;
        private const uint WS_MINIMIZEBOX = 0x00020000;
        private const uint WS_MAXIMIZEBOX = 0x00010000;
        private const uint WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
        private const int CW_USEDEFAULT = unchecked((int)0x80000000);
        private const int SW_SHOW = 5;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const uint WM_PAINT = 0x000F;
        private const uint WM_CLOSE = 0x0010;
        private const uint WM_DESTROY = 0x0002;
        private const uint WM_QUIT = 0x0012;
        private const uint WM_SIZE = 0x0005;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_CONTEXTMENU = 0x007B;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_NCHITTEST = 0x0084;
        private const uint WM_NCLBUTTONDBLCLK = 0x00A3;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_NCRBUTTONDOWN = 0x00A4;
        private const uint WM_NCRBUTTONUP = 0x00A5;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int HTTRANSPARENT = -1;
        private const int SW_MAXIMIZE = 3;
        private const int SW_RESTORE = 9;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_COMPOSITED = 0x02000000;
        private const uint WS_EX_LAYERED = 0x00080000;
        private const uint WS_EX_TRANSPARENT = 0x00000020;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint LWA_ALPHA = 0x00000002;
        private const uint LWA_COLORKEY = 0x00000001;
        private const uint ULW_ALPHA = 0x00000002;

        // Win32 constants
        private const uint WM_USER = 0x0400;

        // Tray icon constants (use 500x to avoid clash with overlay Style 1001–1016)
        private const int WM_TRAY_ICON = (int)WM_USER + 1000;
        private const int ID_TRAY_SHOW_HIDE = 5001;
        private const int ID_TRAY_TRANSPARENT = 5002;

        // Tray icon structures
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
        }

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int NIF_STATE = 0x00000008;
        private const int NIF_INFO = 0x00000010;
        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;
        private const uint MF_BYCOMMAND = 0x00000100;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const uint MB_OK = 0x00000000;
        private const uint MB_ICONINFORMATION = 0x00000040;
        private const uint SRCCOPY = 0x00CC0020;
        private const uint PM_REMOVE = 0x0001;
        private const int IDC_ARROW = 32512;
        private const int GWLP_USERDATA = -21;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
        
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
        
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern ushort RegisterClass(ref WNDCLASS lpWndClass);
        
        [DllImport("user32.dll")]
        private static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
        
        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);
        
        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);
        
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int nExitCode);
        
        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
        
        [DllImport("user32.dll")]
        private static extern bool ValidateRect(IntPtr hWnd, IntPtr lpRect);
        
        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);
        
        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // Tray icon APIs
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
        
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hObjectSource, int nXSrc, int nYSrc, uint dwRop);
        
        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();
        
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string lpNewItem);
        
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool ModifyMenu(IntPtr hMnu, uint uPosition, uint uFlags, IntPtr uIDNewItem, string lpNewItem);
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);
        
        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);
        
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(IntPtr hWnd, string lpText, string lpCaption, uint uType);
        
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        
        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        
        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);
        
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);
        
        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);
        
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        private const int SM_CXSCREEN = 0; // Screen width
        private const int SM_CYSCREEN = 1; // Screen height
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }
        
        private const uint MF_STRING = 0x0000;
        private const uint MF_POPUP = 0x0010;
        private const uint MF_SEPARATOR = 0x0800;
        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_TOPALIGN = 0x0000;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        
        #endregion
    }
}
