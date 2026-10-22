using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Drawing;
using System.Reflection;
using Forms = System.Windows.Forms;

namespace VOIDOverlay.Metron;

public partial class App : System.Windows.Application
{
    private const int WmAppShow = 0x8001;
    private const string MutexName = "Local\\VOIDOverlay.Metron";
    private Mutex? instanceMutex;
    private SettingsStore? settingsStore;
    private MetricsService? metricsService;
    private SettingsWindow? settingsWindow;
    private MainWindow? overlay;
    private Forms.NotifyIcon? trayIcon;
    private Forms.ContextMenuStrip? trayMenu;
    private Forms.ToolStripMenuItem? lockMenuItem;
    private Forms.ToolStripMenuItem? startupMenuItem;
    private Icon? trayImage;
    private StartupManager? startupManager;
    private bool ownsInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            instanceMutex.Dispose();
            instanceMutex = null;
            var existingWindow = FindWindow(null, "VOIDOverlay Metron");
            if (existingWindow != IntPtr.Zero)
            {
                PostMessage(existingWindow, WmAppShow, IntPtr.Zero, IntPtr.Zero);
            }

            Shutdown();
            return;
        }

        ownsInstanceMutex = true;
        base.OnStartup(e);
        settingsStore = new SettingsStore();
        var settings = settingsStore.Load();
        startupManager = new StartupManager();
        settings.StartWithWindows = startupManager.IsEnabled();
        overlay = new MainWindow(settings);
        settingsWindow = new SettingsWindow(settings, settingsStore, startupManager);
        settingsWindow.SettingsChanged += OnSettingsChanged;
        metricsService = new MetricsService(settings.RefreshIntervalMs);
        metricsService.SnapshotAvailable += OnSnapshotAvailable;
        trayIcon = CreateTrayIcon(settings, startupManager);

        if (settings.StartOverlayVisible)
        {
            overlay.Show();
        }

        metricsService.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        metricsService?.Dispose();
        metricsService = null;
        settingsWindow?.CloseForExit();
        trayIcon?.Dispose();
        trayIcon = null;
        trayMenu?.Dispose();
        trayMenu = null;
        trayImage?.Dispose();
        trayImage = null;
        if (ownsInstanceMutex)
        {
            instanceMutex?.ReleaseMutex();
            ownsInstanceMutex = false;
        }

        instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private Forms.NotifyIcon CreateTrayIcon(AppSettings settings, StartupManager startupManager)
    {
        var menu = new Forms.ContextMenuStrip();
        trayMenu = menu;
        menu.Items.Add("Show/Hide Overlay", null, (_, _) => ToggleOverlay());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        lockMenuItem = new Forms.ToolStripMenuItem("Lock Position") { Checked = settings.IsLocked, CheckOnClick = true };
        lockMenuItem.CheckedChanged += (_, _) =>
        {
            settings.IsLocked = lockMenuItem.Checked;
            settingsWindow?.RefreshLockState();
            SaveSettings();
        };
        menu.Items.Add(lockMenuItem);
        startupMenuItem = new Forms.ToolStripMenuItem("Start with Windows") { Checked = startupManager.IsEnabled(), CheckOnClick = true };
        startupMenuItem.CheckedChanged += (_, _) =>
        {
            try
            {
                startupManager.SetEnabled(startupMenuItem.Checked);
                settings.StartWithWindows = startupManager.IsEnabled();
                SaveSettings();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
            {
                AppLog.Error($"Unable to update user startup registration. {ex.Message}");
                startupMenuItem.Checked = startupManager.IsEnabled();
            }
        };
        menu.Items.Add(startupMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var informationalVersion = typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Development";
        menu.Items.Add("About", null, (_, _) => System.Windows.MessageBox.Show(
            $"VOIDOverlay Metron\nVersion: {informationalVersion}\nLightweight system performance overlay.\n\nFPS is unavailable until a supported frame-statistics provider is configured.",
            "About Metron",
            MessageBoxButton.OK,
            MessageBoxImage.Information));
        menu.Items.Add("Exit", null, (_, _) => Shutdown());

        var iconResource = GetResourceStream(new Uri("pack://application:,,,/assets/Metron.ico", UriKind.Absolute))
            ?? throw new InvalidOperationException("Embedded Metron.ico application resource could not be loaded.");
        using (iconResource.Stream)
        using (var icon = new Icon(iconResource.Stream))
        {
            trayImage = (Icon)icon.Clone();
        }

        var notifyIcon = new Forms.NotifyIcon
        {
            Icon = trayImage,
            Text = "VOIDOverlay Metron",
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.DoubleClick += (_, _) => ToggleOverlay();
        return notifyIcon;
    }

    private void OnSettingsChanged()
    {
        if (overlay is not null && settingsWindow is not null && metricsService is not null)
        {
            overlay.ApplyAppearance();
            metricsService.SetRefreshInterval(settingsWindow.Settings.RefreshIntervalMs);
            lockMenuItem!.Checked = settingsWindow.Settings.IsLocked;
            startupMenuItem!.Checked = settingsWindow.Settings.StartWithWindows;
        }
    }

    private void OnSnapshotAvailable(MetricSnapshot snapshot)
    {
        Dispatcher.BeginInvoke(() => overlay?.UpdateMetrics(snapshot));
    }

    private void ToggleOverlay()
    {
        if (overlay is null)
        {
            return;
        }

        if (overlay.IsVisible)
        {
            overlay.Hide();
        }
        else
        {
            overlay.RevealFromTray();
        }
    }

    private void ShowSettings()
    {
        if (settingsWindow is null)
        {
            return;
        }

        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private void SaveSettings()
    {
        try
        {
            if (settingsStore is not null && settingsWindow is not null)
            {
                settingsStore.Save(settingsWindow.Settings);
                settingsWindow.RefreshLockState();
                lockMenuItem!.Checked = settingsWindow.Settings.IsLocked;
                startupMenuItem!.Checked = settingsWindow.Settings.StartWithWindows;
                overlay?.ApplyAppearance();
            }
        }
        catch (IOException ex)
        {
            AppLog.Error($"Unable to save settings. {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Error($"Unable to save settings. {ex.Message}");
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string windowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);
}
