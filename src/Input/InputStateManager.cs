using System;
using System.Runtime.InteropServices;

namespace GamingKeypressOverlay.Input
{
    /// <summary>
    /// Safe wrapper for InputState* unsafe pointer with IDisposable pattern.
    /// Ensures InputState memory is properly freed when disposed.
    /// </summary>
    public unsafe class InputStateManager : IDisposable
    {
        private InputState* _state;
        private IntPtr _statePtr;
        private bool _disposed = false;
        
        /// <summary>
        /// Get the InputState pointer (throws if disposed)
        /// </summary>
        public InputState* State
        {
            get
            {
                ThrowIfDisposed();
                return _state;
            }
        }
        
        /// <summary>
        /// Create and initialize InputState in unmanaged memory
        /// </summary>
        public InputStateManager()
        {
            try
            {
                // Allocate unmanaged memory for InputState
                _statePtr = Marshal.AllocHGlobal(Marshal.SizeOf<InputState>());
                
                if (_statePtr == IntPtr.Zero)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for InputState");
                }
                
                _state = (InputState*)_statePtr.ToPointer();
                
                if (_state == null)
                {
                    Marshal.FreeHGlobal(_statePtr);
                    _statePtr = IntPtr.Zero;
                    throw new InvalidOperationException("Failed to get pointer to InputState");
                }
                
                // Initialize state
                *_state = new InputState();
            }
            catch
            {
                // Cleanup on failure
                Dispose();
                throw;
            }
        }
        
        /// <summary>
        /// Dispose and free unmanaged memory
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            
            if (_statePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_statePtr);
                _statePtr = IntPtr.Zero;
                _state = null;
            }
            
            _disposed = true;
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Finalizer (safety net if user forgets Dispose)
        /// </summary>
        ~InputStateManager()
        {
            Dispose();
        }
        
        /// <summary>
        /// Throw if already disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InputStateManager));
        }
    }
}
