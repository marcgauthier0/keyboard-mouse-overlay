using System.Collections.Generic;

namespace GamingKeypressOverlay.Win32.Direct2D
{
    /// <summary>
    /// Interface for Direct2D keyboard layouts
    /// Each layout defines the key positions and labels for a specific keyboard type
    /// </summary>
    public interface IDirect2DKeyboardLayout
    {
        /// <summary>
        /// Get the keyboard layout as a 2D array of key labels
        /// Each row is an array of strings, empty strings represent spacing
        /// </summary>
        string[][] GetLayout();

        /// <summary>
        /// Get the width multiplier for special keys
        /// </summary>
        int GetKeyWidth(string keyLabel);

        /// <summary>
        /// Convert a key label to its virtual key code
        /// </summary>
        byte KeyLabelToVkCode(string label);
    }
}
