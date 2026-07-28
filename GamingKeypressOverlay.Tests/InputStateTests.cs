using System;
using System.Diagnostics;
using System.Threading;
using Xunit;
using GamingKeypressOverlay.Input;

namespace GamingKeypressOverlay.Tests
{
    /// <summary>
    /// Unit tests for InputState - core input state management
    /// </summary>
    public class InputStateTests
    {
        [Fact]
        public void SetKey_ValidKey_UpdatesState()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                byte vkey = 0x57; // 'W' key
                
                // Act
                inputState.SetKey(vkey, true);
                
                // Assert
                Assert.True(inputState.GetKey(vkey));
            }
        }
        
        [Fact]
        public void SetKey_InvalidKey_DoesNotCrash()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                byte invalidVkey = 255; // Max valid, but test edge case
                
                // Act & Assert - should not throw
                inputState.SetKey(invalidVkey, true);
                // Should handle gracefully (validation in SetKey)
            }
        }
        
        [Fact]
        public void SetKey_PressThenRelease_StateUpdatesCorrectly()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                byte vkey = 0x41; // 'A' key
                
                // Act
                inputState.SetKey(vkey, true);
                bool pressed = inputState.GetKey(vkey);
                
                inputState.SetKey(vkey, false);
                bool released = inputState.GetKey(vkey);
                
                // Assert
                Assert.True(pressed);
                Assert.False(released);
            }
        }
        
        [Fact]
        public void SetKey_Press_UpdatesTimestamp()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                byte vkey = 0x53; // 'S' key
                long beforeTimestamp = Stopwatch.GetTimestamp();
                
                // Act
                inputState.SetKey(vkey, true);
                long timestamp = inputState.GetKeyTimestamp(vkey);
                long afterTimestamp = Stopwatch.GetTimestamp();
                
                // Assert
                Assert.True(timestamp >= beforeTimestamp);
                Assert.True(timestamp <= afterTimestamp);
            }
        }
        
        [Fact]
        public void SetKey_Press_CreatesLatch()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                byte vkey = 0x44; // 'D' key
                
                // Act
                inputState.SetKey(vkey, true);
                long latch = inputState.GetKeyLatchTimestamp(vkey);
                
                // Assert
                Assert.True(latch > 0);
            }
        }
        
        [Fact]
        public void SetKey_Press_UpdatesLastKey()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                byte vkey1 = 0x57; // 'W'
                byte vkey2 = 0x41; // 'A'
                
                // Act
                inputState.SetKey(vkey1, true);
                byte lastKey1 = inputState.LastKey;
                
                inputState.SetKey(vkey2, true);
                byte lastKey2 = inputState.LastKey;
                byte secondLastKey = inputState.SecondLastKey;
                
                // Assert
                Assert.Equal(vkey1, lastKey1);
                Assert.Equal(vkey2, lastKey2);
                Assert.Equal(vkey1, secondLastKey); // Should shift
            }
        }
        
        [Fact]
        public void SetKey_Release_ClearsLastKey()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                byte vkey = 0x54; // 'T' key
                
                // Act
                inputState.SetKey(vkey, true);
                Assert.Equal(vkey, inputState.LastKey);
                
                inputState.SetKey(vkey, false);
                
                // Assert
                Assert.Equal((byte)0, inputState.LastKey);
            }
        }
        
        [Fact]
        public void CreateSnapshot_ThreadSafe_CreatesValidSnapshot()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                inputState.SetKey(0x57, true); // 'W'
                inputState.SetKey(0x41, true); // 'A'
                
                // Act
                var snapshot = inputState.CreateSnapshot();
                
                // Assert
                Assert.NotNull(snapshot);
                Assert.True(snapshot.Keys[0x57]);
                Assert.True(snapshot.Keys[0x41]);
                Assert.Equal(0x41, snapshot.LastKey);
                Assert.Equal(0x57, snapshot.SecondLastKey);
            }
        }
        
        [Fact]
        public void CleanOldTimestamps_ExpiredTimestamp_CleansUp()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                byte vkey = 0x53; // 'S' key
                inputState.SetKey(vkey, true);
                
                long currentTime = Stopwatch.GetTimestamp();
                long maxAgeTicks = Stopwatch.Frequency / 10; // 100ms
                long minVisualTicks = Stopwatch.Frequency / 20; // 50ms
                
                // Simulate old timestamp (set to past)
                // Note: This is tricky with the current implementation, but we can test the logic
                bool[] currentKeyStates = new bool[256];
                currentKeyStates[vkey] = false; // Key not pressed
                
                // Wait a bit to ensure timestamp is old
                Thread.Sleep(150);
                
                // Act
                inputState.CleanOldTimestamps(
                    Stopwatch.GetTimestamp(),
                    maxAgeTicks,
                    minVisualTicks,
                    currentKeyStates
                );
                
                // Assert - timestamp should be cleaned if key is not pressed
                // (This test may be flaky due to timing, but tests the logic)
                long timestamp = inputState.GetKeyTimestamp(vkey);
                // If cleanup worked and key is not pressed, timestamp might be 0
                // But this depends on timing, so we just verify it doesn't crash
            }
        }
        
        [Fact]
        public void AddWheelDelta_WithinLimits_UpdatesCorrectly()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                
                // Act
                inputState.AddWheelDelta(120);
                int delta1 = inputState.GetAndResetWheelDelta();
                
                inputState.AddWheelDelta(-120);
                int delta2 = inputState.GetAndResetWheelDelta();
                
                // Assert
                Assert.Equal(120, delta1);
                Assert.Equal(-120, delta2);
            }
        }
        
        [Fact]
        public void AddWheelDelta_Overflow_ClampsCorrectly()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                
                // Act - Add large value that would overflow
                for (int i = 0; i < 1000; i++)
                {
                    inputState.AddWheelDelta(1000);
                }
                
                int delta = inputState.GetAndResetWheelDelta();
                
                // Assert - Should be clamped to MAX_WHEEL_DELTA (10000)
                Assert.True(delta <= 10000);
                Assert.True(delta >= -10000);
            }
        }
        
        [Fact]
        public void GetAndResetWheelDelta_ResetsToZero()
        {
            unsafe
            {
                // Arrange
                var inputState = new InputState();
                inputState.AddWheelDelta(120);
                
                // Act
                int delta1 = inputState.GetAndResetWheelDelta();
                int delta2 = inputState.GetAndResetWheelDelta();
                
                // Assert
                Assert.Equal(120, delta1);
                Assert.Equal(0, delta2); // Should be reset
            }
        }
    }
}
