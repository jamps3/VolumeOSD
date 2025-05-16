using NAudio.CoreAudioApi;
using System;
using System.Timers;

namespace VolumeOSD
{
    public class VolumeMonitor : IDisposable
    {
        private readonly MMDeviceEnumerator deviceEnumerator;
        private MMDevice device;
        private readonly Timer reconnectTimer;
        private readonly Timer pollTimer;  // Added for polling
        private bool isDisposed;
        private float lastVolume = -1;  // Track last volume
        private bool lastMuteState = false;  // Track last mute state

        public event Action<int> VolumeChanged;  // 0-100 for better precision

        public VolumeMonitor()
        {
            deviceEnumerator = new MMDeviceEnumerator();
            reconnectTimer = new Timer(5000) { AutoReset = true };
            reconnectTimer.Elapsed += ReconnectTimer_Elapsed;

            // Add polling timer for redundancy (poll every 100ms)
            pollTimer = new Timer(100) { AutoReset = true }; 
            pollTimer.Elapsed += PollTimer_Elapsed;
            System.Diagnostics.Debug.WriteLine($"Volume monitor initialized - Poll interval: {pollTimer.Interval}ms");

            try
            {
                InitializeDevice();
                
                // Force an immediate volume check
                System.Diagnostics.Debug.WriteLine("Performing initial volume check...");
                ForceVolumeNotification();
                
                // Start the polling timer
                pollTimer.Start();
                System.Diagnostics.Debug.WriteLine("Volume polling timer started");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize audio device: {ex.Message}");
                reconnectTimer.Start();
            }
        }

        private void InitializeDevice()
        {
            if (isDisposed) return;

            try
            {
                if (device != null)
                {
                    device.AudioEndpointVolume.OnVolumeNotification -= AudioEndpointVolume_OnVolumeNotification;
                    device.Dispose();
                }

                device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                device.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
                System.Diagnostics.Debug.WriteLine("Connected to audio device volume notifications");

                // Initial volume check
                System.Diagnostics.Debug.WriteLine("Performing initial device volume check...");
                CheckAndNotifyVolumeChange();
                
                // Force a notification even if volume hasn't changed
                ForceVolumeNotification();
                
                reconnectTimer.Stop();
                System.Diagnostics.Debug.WriteLine("Device initialization complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing audio device: {ex.Message}");
                reconnectTimer.Start();
            }
        }

        private void PollTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CheckAndNotifyVolumeChange();
        }
        
        /// <summary>
        /// Forces a volume notification with the current volume level
        /// </summary>
        public void ForceVolumeNotification()
        {
            try
            {
                if (device == null)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot force volume notification - device is null");
                    return;
                }
                
                var currentVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                var currentMute = device.AudioEndpointVolume.Mute;
                
                // Always notify regardless of last value
                int volumeLevel = (int)Math.Round(currentVolume * 100);
                volumeLevel = Math.Max(0, Math.Min(100, volumeLevel));
                
                System.Diagnostics.Debug.WriteLine($"Forcing volume notification: {volumeLevel}%, Muted: {currentMute}");
                
                // Update the last values
                lastVolume = currentVolume;
                lastMuteState = currentMute;
                
                // Trigger notification
                if (currentMute)
                {
                    VolumeChanged?.Invoke(0);
                }
                else
                {
                    VolumeChanged?.Invoke(volumeLevel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error forcing volume notification: {ex.Message}");
            }
        }

        private void ReconnectTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                InitializeDevice();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reconnect to audio device: {ex.Message}");
            }
        }

        private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            CheckAndNotifyVolumeChange();
        }

        private void CheckAndNotifyVolumeChange()
        {
            try
            {
                if (device == null)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot check volume - device is null");
                    return;
                }

                var currentMute = device.AudioEndpointVolume.Mute;
                var currentVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                
                // Debug current volume state
                System.Diagnostics.Debug.WriteLine($"Checking volume - Current: {currentVolume:F2} ({(int)(currentVolume * 100)}%), " +
                                                  $"Last: {lastVolume:F2} ({(int)(lastVolume * 100)}%), " +
                                                  $"Muted: {currentMute}, Last Muted: {lastMuteState}");

                // Check if volume or mute state has changed
                if (Math.Abs(currentVolume - lastVolume) > 0.001 || currentMute != lastMuteState)
                {
                    System.Diagnostics.Debug.WriteLine($"Volume change detected - Old: {lastVolume:F2}, New: {currentVolume:F2}");
                    
                    lastVolume = currentVolume;
                    lastMuteState = currentMute;

                    if (currentMute)
                    {
                        System.Diagnostics.Debug.WriteLine("Reporting muted state (0%)");
                        VolumeChanged?.Invoke(0);
                        return;
                    }

                    // Convert 0.0-1.0 to 0-100 range for better precision
                    int volumeLevel = (int)Math.Round(currentVolume * 100);
                    volumeLevel = Math.Max(0, Math.Min(100, volumeLevel)); // Ensure within bounds
                    
                    System.Diagnostics.Debug.WriteLine($"Reporting volume change: {volumeLevel}%");
                    VolumeChanged?.Invoke(volumeLevel);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking volume: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            pollTimer.Stop();
            pollTimer.Dispose();

            reconnectTimer.Stop();
            reconnectTimer.Dispose();

            if (device != null)
            {
                try
                {
                    device.AudioEndpointVolume.OnVolumeNotification -= AudioEndpointVolume_OnVolumeNotification;
                    device.Dispose();
                }
                catch { }
                device = null;
            }

            deviceEnumerator?.Dispose();
        }
    }
}
