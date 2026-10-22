using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO;
using Forms = System.Windows.Forms;

namespace VOIDOverlay.Metron;

public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;
    private readonly SettingsStore store;
    private readonly StartupManager startupManager;
    private readonly DispatcherTimer saveTimer;
    private bool initialized;
    private bool allowClose;

    public event Action? SettingsChanged;
    public AppSettings Settings => settings;

    public SettingsWindow(AppSettings settings, SettingsStore store, StartupManager startupManager)
    {
        InitializeComponent();
        this.settings = settings;
        this.store = store;
        this.startupManager = startupManager;
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveTimer.Tick += (_, _) =>
        {
            saveTimer.Stop();
            PersistSettings();
        };
        PositionCombo.ItemsSource = Enum.GetValues<OverlayPosition>();
        MonitorCombo.ItemsSource = Forms.Screen.AllScreens
            .Select(screen => new MonitorOption(screen.DeviceName, $"{screen.DeviceName} {(screen.Primary ? "(Primary)" : string.Empty)}"))
            .ToList();
        LoadSettings();
        WireChanges();
    }

    public void CloseForExit()
    {
        allowClose = true;
        Close();
    }

    public void RefreshLockState()
    {
        LockedCheck.IsChecked = settings.IsLocked;
        PositionControls.IsEnabled = !settings.IsLocked;
    }

    private void LoadSettings()
    {
        StartupCheck.IsChecked = startupManager.IsEnabled();
        TopmostCheck.IsChecked = settings.KeepOnTop;
        LockedCheck.IsChecked = settings.IsLocked;
        StartVisibleCheck.IsChecked = settings.StartOverlayVisible;
        CpuCheck.IsChecked = settings.ShowCpu;
        GpuCheck.IsChecked = settings.ShowGpu;
        RamCheck.IsChecked = settings.ShowRam;
        FpsCheck.IsChecked = settings.ShowFps;
        IntervalCombo.SelectedValue = settings.RefreshIntervalMs.ToString();
        PositionCombo.SelectedItem = settings.Position;
        MonitorCombo.SelectedItem = MonitorCombo.Items
            .Cast<MonitorOption>()
            .FirstOrDefault(item => item.DeviceName.Equals(settings.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? MonitorCombo.Items.Cast<MonitorOption>().FirstOrDefault(item => item.DeviceName == (Forms.Screen.PrimaryScreen?.DeviceName ?? string.Empty));
        FontSlider.Value = settings.FontSize;
        OpacitySlider.Value = settings.Opacity;
        OffsetXBox.Text = settings.OffsetX.ToString();
        OffsetYBox.Text = settings.OffsetY.ToString();
        PositionControls.IsEnabled = !settings.IsLocked;
        initialized = true;
    }

    private void WireChanges()
    {
        StartupCheck.Checked += (_, _) => Save(updateStartup: true);
        StartupCheck.Unchecked += (_, _) => Save(updateStartup: true);
        TopmostCheck.Checked += (_, _) => Save();
        TopmostCheck.Unchecked += (_, _) => Save();
        LockedCheck.Checked += (_, _) => Save();
        LockedCheck.Unchecked += (_, _) => Save();
        StartVisibleCheck.Checked += (_, _) => Save();
        StartVisibleCheck.Unchecked += (_, _) => Save();
        CpuCheck.Checked += (_, _) => Save();
        CpuCheck.Unchecked += (_, _) => Save();
        GpuCheck.Checked += (_, _) => Save();
        GpuCheck.Unchecked += (_, _) => Save();
        RamCheck.Checked += (_, _) => Save();
        RamCheck.Unchecked += (_, _) => Save();
        FpsCheck.Checked += (_, _) => Save();
        FpsCheck.Unchecked += (_, _) => Save();
        IntervalCombo.SelectionChanged += (_, _) => Save();
        PositionCombo.SelectionChanged += (_, _) => Save();
        MonitorCombo.SelectionChanged += (_, _) => Save();
        FontSlider.ValueChanged += (_, _) => Save();
        OpacitySlider.ValueChanged += (_, _) => Save();
        OffsetXBox.LostFocus += (_, _) => Save();
        OffsetYBox.LostFocus += (_, _) => Save();
    }

    private void Save(bool updateStartup = false)
    {
        if (!initialized)
        {
            return;
        }

        if (updateStartup)
        {
            try
            {
                startupManager.SetEnabled(StartupCheck.IsChecked == true);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
            {
                AppLog.Error($"Unable to update user startup registration. {ex.Message}");
                StartupCheck.IsChecked = startupManager.IsEnabled();
            }
        }

        settings.StartWithWindows = startupManager.IsEnabled();
        settings.KeepOnTop = TopmostCheck.IsChecked == true;
        settings.IsLocked = LockedCheck.IsChecked == true;
        PositionControls.IsEnabled = !settings.IsLocked;
        settings.StartOverlayVisible = StartVisibleCheck.IsChecked == true;
        settings.ShowCpu = CpuCheck.IsChecked == true;
        settings.ShowGpu = GpuCheck.IsChecked == true;
        settings.ShowRam = RamCheck.IsChecked == true;
        settings.ShowFps = FpsCheck.IsChecked == true;
        if (IntervalCombo.SelectedItem is ComboBoxItem interval && int.TryParse(interval.Tag?.ToString(), out var milliseconds))
        {
            settings.RefreshIntervalMs = milliseconds;
        }

        if (PositionCombo.SelectedItem is OverlayPosition position)
        {
            settings.Position = position;
        }

        if (MonitorCombo.SelectedItem is MonitorOption monitor)
        {
            settings.MonitorDeviceName = monitor.DeviceName;
        }

        settings.FontSize = FontSlider.Value;
        settings.Opacity = OpacitySlider.Value;
        if (int.TryParse(OffsetXBox.Text, out var offsetX))
        {
            settings.OffsetX = offsetX;
        }
        else
        {
            OffsetXBox.Text = settings.OffsetX.ToString();
        }

        if (int.TryParse(OffsetYBox.Text, out var offsetY))
        {
            settings.OffsetY = offsetY;
        }
        else
        {
            OffsetYBox.Text = settings.OffsetY.ToString();
        }

        settings.Validate();
        SettingsChanged?.Invoke();
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void PersistSettings()
    {
        try
        {
            store.Save(settings);
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

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var defaults = AppSettings.Defaults();
        settings.Position = defaults.Position;
        settings.OffsetX = defaults.OffsetX;
        settings.OffsetY = defaults.OffsetY;
        settings.MonitorDeviceName = Forms.Screen.PrimaryScreen?.DeviceName ?? string.Empty;
        settings.RefreshIntervalMs = defaults.RefreshIntervalMs;
        settings.FontSize = defaults.FontSize;
        settings.Opacity = defaults.Opacity;
        settings.ShowCpu = defaults.ShowCpu;
        settings.ShowGpu = defaults.ShowGpu;
        settings.ShowRam = defaults.ShowRam;
        settings.ShowFps = defaults.ShowFps;
        settings.KeepOnTop = defaults.KeepOnTop;
        settings.IsLocked = defaults.IsLocked;
        settings.StartOverlayVisible = defaults.StartOverlayVisible;
        StartupCheck.IsChecked = defaults.StartWithWindows;
        LoadSettings();
        Save();
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        saveTimer.Stop();
        PersistSettings();
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        saveTimer.Stop();
        PersistSettings();
        if (!allowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    private sealed record MonitorOption(string DeviceName, string Name);
}
