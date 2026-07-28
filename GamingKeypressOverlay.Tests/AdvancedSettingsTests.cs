using System;
using Xunit;
using GamingKeypressOverlay.Settings;

namespace GamingKeypressOverlay.Tests
{
    /// <summary>
    /// Unit tests for AdvancedSettings - configuration validation
    /// </summary>
    public class AdvancedSettingsTests
    {
        [Fact]
        public void FlashDurationMs_ValidValue_SetsCorrectly()
        {
            // Arrange
            var settings = new AdvancedSettings();
            
            // Act
            settings.FlashDurationMs = 30;
            
            // Assert
            Assert.Equal(30, settings.FlashDurationMs);
        }
        
        [Fact]
        public void FlashDurationMs_InvalidValue_ThrowsException()
        {
            // Arrange
            var settings = new AdvancedSettings();
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.FlashDurationMs = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.FlashDurationMs = 1001);
        }
        
        [Fact]
        public void LatchDurationMs_ValidValue_SetsCorrectly()
        {
            // Arrange
            var settings = new AdvancedSettings();
            
            // Act
            settings.LatchDurationMs = 50;
            
            // Assert
            Assert.Equal(50, settings.LatchDurationMs);
        }
        
        [Fact]
        public void LatchDurationMs_InvalidValue_ThrowsException()
        {
            // Arrange
            var settings = new AdvancedSettings();
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.LatchDurationMs = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.LatchDurationMs = 1001);
        }
        
        [Fact]
        public void EventBufferSize_ValidValue_SetsCorrectly()
        {
            // Arrange
            var settings = new AdvancedSettings();
            
            // Act
            settings.EventBufferSize = 32;
            
            // Assert
            Assert.Equal(32, settings.EventBufferSize);
        }
        
        [Fact]
        public void EventBufferSize_InvalidValue_ThrowsException()
        {
            // Arrange
            var settings = new AdvancedSettings();
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.EventBufferSize = 7);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.EventBufferSize = 257);
        }
        
        [Fact]
        public void PollingIntervalMs_ValidValue_SetsCorrectly()
        {
            // Arrange
            var settings = new AdvancedSettings();
            
            // Act
            settings.PollingIntervalMs = 1;
            
            // Assert
            Assert.Equal(1, settings.PollingIntervalMs);
        }
        
        [Fact]
        public void PollingIntervalMs_InvalidValue_ThrowsException()
        {
            // Arrange
            var settings = new AdvancedSettings();
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.PollingIntervalMs = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.PollingIntervalMs = 101);
        }
        
        [Fact]
        public void InputThreadPriority_ValidValue_SetsCorrectly()
        {
            // Arrange
            var settings = new AdvancedSettings();
            
            // Act
            settings.InputThreadPriority = System.Threading.ThreadPriority.Highest;
            
            // Assert
            Assert.Equal(System.Threading.ThreadPriority.Highest, settings.InputThreadPriority);
        }
        
        [Fact]
        public void Validate_ValidSettings_ReturnsTrue()
        {
            // Arrange
            var settings = AdvancedSettings.CreateGamingDefaults();
            
            // Act
            bool isValid = settings.Validate(out string error);
            
            // Assert
            Assert.True(isValid);
            Assert.Null(error);
        }
        
        [Fact]
        public void CreateGamingDefaults_CreatesValidSettings()
        {
            // Act
            var settings = AdvancedSettings.CreateGamingDefaults();
            
            // Assert
            Assert.False(settings.EnablePolling); // Raw Input first; polling remains a fallback.
            Assert.Equal(1, settings.PollingIntervalMs);
            Assert.Equal(System.Threading.ThreadPriority.Highest, settings.InputThreadPriority);
        }
        
        [Fact]
        public void CreateDesktopDefaults_CreatesValidSettings()
        {
            // Act
            var settings = AdvancedSettings.CreateDesktopDefaults();
            
            // Assert
            Assert.False(settings.EnablePolling);
            Assert.Equal(10, settings.PollingIntervalMs);
            Assert.Equal(System.Threading.ThreadPriority.AboveNormal, settings.InputThreadPriority);
        }
    }
}
