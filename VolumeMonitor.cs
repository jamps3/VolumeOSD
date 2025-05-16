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

            // Add polling timer for redundancy
            pollTimer = new Timer(100) { AutoReset = true }; // Poll every 100ms
            pollTimer.Elapsed += PollTimer_Elapsed;

            try
            {
                InitializeDevice();
                pollTimer.Start();
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

                // Initial volume check
                CheckAndNotifyVolumeChange();
                reconnectTimer.Stop();
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
                if (device == null) return;

                var currentMute = device.AudioEndpointVolume.Mute;
                var currentVolume = device.AudioEndpointVolume.MasterVolumeLevelScalar;

                // Check if volume or mute state has changed
                if (Math.Abs(currentVolume - lastVolume) > 0.001 || currentMute != lastMuteState)
                {
                    lastVolume = currentVolume;
                    lastMuteState = currentMute;

                    if (currentMute)
                    {
                        VolumeChanged?.Invoke(0);
                        return;
                    }

                    // Convert 0.0-1.0 to 0-100 range for better precision
                    int volumeLevel = (int)Math.Round(currentVolume * 100);
                    volumeLevel = Math.Max(0, Math.Min(100, volumeLevel)); // Ensure within bounds
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
