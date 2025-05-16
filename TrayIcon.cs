using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace VolumeOSD
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem settingsMenuItem;
        private ToolStripMenuItem exitMenuItem;

        public event EventHandler<int> VolumeLevelChanged;
        public event EventHandler SettingsRequested;
        public event EventHandler MainWindowToggleRequested;
        public event EventHandler ExitRequested;

        public TrayIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = CreateVolumeIcon(0);
            notifyIcon.Visible = false;
            notifyIcon.Text = "VolumeOSD - Click to open settings";

            contextMenu = new ContextMenuStrip();
            
            settingsMenuItem = new ToolStripMenuItem("Settings");
            settingsMenuItem.Click += (s, e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(settingsMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(exitMenuItem);

            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        public void Show()
        {
            notifyIcon.Visible = true;
        }

        private void NotifyIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Toggle main window visibility instead of always showing settings
                    MainWindowToggleRequested?.Invoke(this, EventArgs.Empty);
                });
            }
        }

        public void UpdateVolumeIcon(int volume) // 0-10
        {
            try
            {
                if (notifyIcon.Icon != null)
                {
                    using (var oldIcon = notifyIcon.Icon)
                    {
                        notifyIcon.Icon = CreateVolumeIcon(volume);
                    }
                }
                else
                {
                    notifyIcon.Icon = CreateVolumeIcon(volume);
                }
                
                notifyIcon.Text = $"VolumeOSD - Volume: {volume * 10}%";
                VolumeLevelChanged?.Invoke(this, volume);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating tray icon: {ex.Message}");
            }
        }

        private Icon CreateVolumeIcon(int volumeLevel)
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Black);
                
                if (volumeLevel > 0)
                {
                    int barWidth = (int)(volumeLevel / 10.0 * bmp.Width);
                    using (var brush = new SolidBrush(System.Drawing.Color.LimeGreen))
                    {
                        g.FillRectangle(brush, 0, 0, barWidth, bmp.Height);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon); // Will be disposed when new icon is set
            }
        }

        public void Dispose()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Icon?.Dispose();
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
                notifyIcon = null;
            }

            if (contextMenu != null)
            {
                contextMenu.Dispose();
                contextMenu = null;
            }
        }
    }
}
