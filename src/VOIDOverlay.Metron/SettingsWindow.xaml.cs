using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace VOIDOverlay.Metron;

public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;
    private readonly SettingsStore store;
    private readonly StartupManager startupManager;
    private readonly DispatcherTimer saveTimer;
    private bool initialized;
    private bool synchronizingOffsets;
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
        saveTimer.Stop();
        PersistSettings();
        Close();
    }

    public void RefreshLockState()
    {
        LockedCheck.IsChecked = settings.IsLocked;
        PositionControls.IsEnabled = !settings.IsLocked;
    }

    private void LoadSettings()
    {
        initialized = false;
        StartupCheck.IsChecked = startupManager.IsEnabled();
        TopmostCheck.IsChecked = settings.KeepOnTop;
        LockedCheck.IsChecked = settings.IsLocked;
        ClickThroughCheck.IsChecked = settings.ClickThrough;
        StartVisibleCheck.IsChecked = settings.StartOverlayVisible;
        BackgroundEnabledCheck.IsChecked = settings.BackgroundEnabled;
        BackgroundColorBox.Text = settings.BackgroundColor;
        BackgroundOpacitySlider.Value = settings.BackgroundOpacity;
        OverlayOpacitySlider.Value = settings.Opacity;
        TextColorBox.Text = settings.TextColor;
        AccentColorBox.Text = settings.AccentColor;
        SecondaryAccentColorBox.Text = settings.SecondaryAccentColor;
        FontSizeSlider.Value = settings.FontSize;
        IntervalCombo.SelectedValue = settings.RefreshIntervalMs.ToString(CultureInfo.InvariantCulture);
        PositionCombo.SelectedItem = settings.Position;
        MonitorCombo.SelectedItem = MonitorCombo.Items
            .Cast<MonitorOption>()
            .FirstOrDefault(item => item.DeviceName.Equals(settings.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? MonitorCombo.Items.Cast<MonitorOption>().FirstOrDefault(item => item.DeviceName == (Forms.Screen.PrimaryScreen?.DeviceName ?? string.Empty));
        EdgeReferenceCombo.SelectedIndex =
            settings.EdgeReference == OverlayEdgeReference.WorkArea ? 0 : 1;
        OffsetXSlider.Value = settings.OffsetX;
        OffsetYSlider.Value = settings.OffsetY;
        OffsetXBox.Text = settings.OffsetX.ToString(CultureInfo.InvariantCulture);
        OffsetYBox.Text = settings.OffsetY.ToString(CultureInfo.InvariantCulture);
        PositionControls.IsEnabled = !settings.IsLocked;
        initialized = true;
    }

    private void WireChanges()
    {
        StartupCheck.Checked += StartupChanged;
        StartupCheck.Unchecked += StartupChanged;
        TopmostCheck.Checked += SettingsChangedHandler;
        TopmostCheck.Unchecked += SettingsChangedHandler;
        LockedCheck.Checked += SettingsChangedHandler;
        LockedCheck.Unchecked += SettingsChangedHandler;
        ClickThroughCheck.Checked += SettingsChangedHandler;
        ClickThroughCheck.Unchecked += SettingsChangedHandler;
        StartVisibleCheck.Checked += SettingsChangedHandler;
        StartVisibleCheck.Unchecked += SettingsChangedHandler;
        BackgroundEnabledCheck.Checked += SettingsChangedHandler;
        BackgroundEnabledCheck.Unchecked += SettingsChangedHandler;
        BackgroundColorBox.LostFocus += SettingsChangedHandler;
        BackgroundOpacitySlider.ValueChanged += SettingsChangedHandler;
        OverlayOpacitySlider.ValueChanged += SettingsChangedHandler;
        TextColorBox.LostFocus += SettingsChangedHandler;
        AccentColorBox.LostFocus += SettingsChangedHandler;
        SecondaryAccentColorBox.LostFocus += SettingsChangedHandler;
        FontSizeSlider.ValueChanged += SettingsChangedHandler;
        PositionCombo.SelectionChanged += SettingsChangedHandler;
        MonitorCombo.SelectionChanged += SettingsChangedHandler;
        EdgeReferenceCombo.SelectionChanged += SettingsChangedHandler;
        IntervalCombo.SelectionChanged += SettingsChangedHandler;
        CpuCheck.Checked += SettingsChangedHandler;
        CpuCheck.Unchecked += SettingsChangedHandler;
        GpuCheck.Checked += SettingsChangedHandler;
        GpuCheck.Unchecked += SettingsChangedHandler;
        RamCheck.Checked += SettingsChangedHandler;
        RamCheck.Unchecked += SettingsChangedHandler;
        FpsCheck.Checked += SettingsChangedHandler;
        FpsCheck.Unchecked += SettingsChangedHandler;
        OffsetXSlider.ValueChanged += OffsetSliderChanged;
        OffsetYSlider.ValueChanged += OffsetSliderChanged;
        OffsetXBox.LostFocus += OffsetTextChanged;
        OffsetYBox.LostFocus += OffsetTextChanged;
    }

    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized)
        {
            return;
        }

        try
        {
            startupManager.SetEnabled(StartupCheck.IsChecked == true);
            ApplySettings();
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            AppLog.Error($"Unable to update current-user startup registration. {exception.Message}");
            StartupCheck.IsChecked = startupManager.IsEnabled();
        }
    }

    private void SettingsChangedHandler(object sender, RoutedEventArgs e) => ApplySettings();

    private void OffsetSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!initialized || synchronizingOffsets)
        {
            return;
        }

        synchronizingOffsets = true;
        try
        {
            if (ReferenceEquals(sender, OffsetXSlider))
            {
                OffsetXBox.Text = ((int)OffsetXSlider.Value).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                OffsetYBox.Text = ((int)OffsetYSlider.Value).ToString(CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            synchronizingOffsets = false;
        }

        ApplySettings();
    }

    private void OffsetTextChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized || synchronizingOffsets)
        {
            return;
        }

        var textBox = (System.Windows.Controls.TextBox)sender;
        if (!TryParseOffset(textBox.Text, out var offset))
        {
            textBox.ToolTip = "Enter a whole number from -200 to 1000 DIPs.";
            return;
        }

        textBox.ToolTip = null;
        synchronizingOffsets = true;
        try
        {
            if (ReferenceEquals(textBox, OffsetXBox))
            {
                OffsetXSlider.Value = offset;
            }
            else
            {
                OffsetYSlider.Value = offset;
            }
        }
        finally
        {
            synchronizingOffsets = false;
        }

        ApplySettings();
    }

    private void ApplySettings()
    {
        if (!initialized ||
            !TryReadColor(BackgroundColorBox, out var backgroundColor) ||
            !TryReadColor(TextColorBox, out var textColor) ||
            !TryReadColor(AccentColorBox, out var accentColor) ||
            !TryReadColor(SecondaryAccentColorBox, out var secondaryColor) ||
            !TryParseOffset(OffsetXBox.Text, out var offsetX) ||
            !TryParseOffset(OffsetYBox.Text, out var offsetY))
        {
            return;
        }

        settings.StartWithWindows = startupManager.IsEnabled();
        settings.KeepOnTop = TopmostCheck.IsChecked == true;
        settings.IsLocked = LockedCheck.IsChecked == true;
        settings.ClickThrough = ClickThroughCheck.IsChecked == true;
        settings.StartOverlayVisible = StartVisibleCheck.IsChecked == true;
        settings.BackgroundEnabled = BackgroundEnabledCheck.IsChecked == true;
        settings.BackgroundColor = backgroundColor;
        settings.BackgroundOpacity = BackgroundOpacitySlider.Value;
        settings.Opacity = OverlayOpacitySlider.Value;
        settings.TextColor = textColor;
        settings.AccentColor = accentColor;
        settings.SecondaryAccentColor = secondaryColor;
        settings.FontSize = FontSizeSlider.Value;
        settings.ShowCpu = CpuCheck.IsChecked == true;
        settings.ShowGpu = GpuCheck.IsChecked == true;
        settings.ShowRam = RamCheck.IsChecked == true;
        settings.ShowFps = FpsCheck.IsChecked == true;
        settings.OffsetX = offsetX;
        settings.OffsetY = offsetY;
        if (PositionCombo.SelectedItem is OverlayPosition position)
        {
            settings.Position = position;
        }

        if (MonitorCombo.SelectedItem is MonitorOption monitor)
        {
            settings.MonitorDeviceName = monitor.DeviceName;
        }

        settings.EdgeReference = EdgeReferenceCombo.SelectedIndex == 0
            ? OverlayEdgeReference.WorkArea
            : OverlayEdgeReference.FullScreen;
        if (IntervalCombo.SelectedItem is ComboBoxItem interval &&
            int.TryParse(interval.Tag?.ToString(), out var milliseconds))
        {
            settings.RefreshIntervalMs = milliseconds;
        }

        settings.Validate();
        PositionControls.IsEnabled = !settings.IsLocked;
        SettingsChanged?.Invoke();
        saveTimer.Stop();
        saveTimer.Start();
    }

    private static bool TryReadColor(System.Windows.Controls.TextBox textBox, out string value)
    {
        value = textBox.Text.Trim();
        if (value.Length == 7 && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit))
        {
            value = value.ToUpperInvariant();
            textBox.ToolTip = null;
            return true;
        }

        textBox.ToolTip = "Enter a six-digit hex color such as #4DE3D2.";
        textBox.Focus();
        return false;
    }

    private static bool TryParseOffset(string text, out int value) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
        value is >= -200 and <= 1000;

    private void PersistSettings()
    {
        try
        {
            store.Save(settings);
        }
        catch (IOException exception)
        {
            AppLog.Error($"Unable to save settings. {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            AppLog.Error($"Unable to save settings. {exception.Message}");
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var defaults = AppSettings.Defaults();
        defaults.StartWithWindows = startupManager.IsEnabled();
        defaults.MonitorDeviceName = Forms.Screen.PrimaryScreen?.DeviceName ?? string.Empty;
        CopySettings(defaults, settings);
        LoadSettings();
        ApplySettings();
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

    private static void CopySettings(AppSettings source, AppSettings destination)
    {
        destination.Position = source.Position;
        destination.EdgeReference = source.EdgeReference;
        destination.OffsetX = source.OffsetX;
        destination.OffsetY = source.OffsetY;
        destination.MonitorDeviceName = source.MonitorDeviceName;
        destination.RefreshIntervalMs = source.RefreshIntervalMs;
        destination.FontSize = source.FontSize;
        destination.Opacity = source.Opacity;
        destination.BackgroundEnabled = source.BackgroundEnabled;
        destination.BackgroundColor = source.BackgroundColor;
        destination.BackgroundOpacity = source.BackgroundOpacity;
        destination.TextColor = source.TextColor;
        destination.AccentColor = source.AccentColor;
        destination.SecondaryAccentColor = source.SecondaryAccentColor;
        destination.ClickThrough = source.ClickThrough;
        destination.ShowCpu = source.ShowCpu;
        destination.ShowGpu = source.ShowGpu;
        destination.ShowRam = source.ShowRam;
        destination.ShowFps = source.ShowFps;
        destination.StartWithWindows = source.StartWithWindows;
        destination.StartOverlayVisible = source.StartOverlayVisible;
        destination.KeepOnTop = source.KeepOnTop;
        destination.IsLocked = source.IsLocked;
    }

    private sealed record MonitorOption(string DeviceName, string Name);
}
