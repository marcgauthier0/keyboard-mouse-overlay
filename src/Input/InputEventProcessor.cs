using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace GamingKeypressOverlay.Input
{
    /// <summary>
    /// Optimized input event processor that cleans and prioritizes events
    /// to prevent UI blocking during intense gameplay
    /// </summary>
    public class InputEventProcessor
    {
        // Priority events (keyboard, mouse buttons) - processed first
        private readonly List<RawInputEvent> _priorityEvents = new List<RawInputEvent>();
        
        // Non-priority events (mouse movement) - processed after, cleaned to avoid redundancy
        private RawInputEvent _lastMouseMove = null;
        
        // Support for high polling rate mice (8000-12000 Hz)
        // At 8000 Hz = 133 events/tick (60fps), at 12000 Hz = 200 events/tick
        // Use high limit to ensure all priority events are processed
        private const int MAX_DEQUEUE_SAFETY = 500; // Safety limit when dequeueing (prevents infinite loops)
        
        /// <summary>
        /// Processes events from the raw input queue, prioritizing keyboard and button events
        /// and cleaning redundant mouse movement events.
        /// Optimized for high polling rate mice (8000-12000 Hz) by cleaning MouseMove events.
        /// </summary>
        public List<RawInputEvent> ProcessQueue(ConcurrentQueue<RawInputEvent> rawQueue)
        {
            _priorityEvents.Clear();
            _lastMouseMove = null;
            
            int dequeued = 0;
            
            // Dequeue all events and separate priority from non-priority
            // For high polling rate mice (8000-12000 Hz), we get 133-200 MouseMove events per tick
            // We only keep the LAST MouseMove (latest position), discarding all intermediate ones
            // This dramatically reduces processing time while maintaining accuracy
            while (dequeued < MAX_DEQUEUE_SAFETY && rawQueue.TryDequeue(out RawInputEvent rawEvent))
            {
                dequeued++;
                
                switch (rawEvent.Type)
                {
                    case RawInputEvent.EventType.KeyDown:
                    case RawInputEvent.EventType.KeyUp:
                    case RawInputEvent.EventType.MouseButton:
                    case RawInputEvent.EventType.MouseWheel:
                        // Priority events - must be processed immediately
                        // These are critical for gameplay responsiveness
                        _priorityEvents.Add(rawEvent);
                        break;
                        
                    case RawInputEvent.EventType.MouseMove:
                        // Discard all intermediate MouseMove events, keep only the last one
                        // This is safe because we only need the final position, not every intermediate step
                        // For 8000-12000 Hz mice, this reduces 133-200 events to just 1 event per tick
                        _lastMouseMove = rawEvent;
                        break;
                }
            }
            
            // Build final list: priority events first, then last mouse position (if any)
            var processedEvents = new List<RawInputEvent>(_priorityEvents);
            if (_lastMouseMove != null)
            {
                processedEvents.Add(_lastMouseMove);
            }
            
            return processedEvents;
        }
    }
}
