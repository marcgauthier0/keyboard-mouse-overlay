using System;
using System.Drawing;

namespace GamingKeypressOverlay.Input
{
    public class RawInputEvent
    {
        public enum EventType { KeyDown, KeyUp, MouseMove, MouseButton, MouseWheel }
        public EventType Type { get; set; }
        public byte VKeyCode { get; set; } // Virtual key code (0-255)
        public PointF MousePosition { get; set; }
        public int MouseButton { get; set; } // 0=Left, 1=Right, 2=Middle
        public bool ButtonPressed { get; set; }
        public int WheelDelta { get; set; } // >0 = up, <0 = down
        public long Timestamp { get; set; }
    }
}
