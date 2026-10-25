using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace VOIDOverlay.Metron;

public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;
    private readonly SettingsStore store;
    private readonly StartupManager startupManager;
    private readonly Func<(double Width, double Height)> overlaySize;
    private readonly DispatcherTimer saveTimer;
    private bool initialized;
    private bool synchronizingOffsets;
    private bool allowClose;

    public event Action? SettingsChanged;
    public AppSettings Settings => settings;

    public SettingsWindow(
        AppSettings settings,
        SettingsStore store,
        StartupManager startupManager,
        Func<(double Width, double Height)>? overlaySize = null)
    {
        InitializeComponent();
        this.settings = settings;
        this.store = store;
        this.startupManager = startupManager;
        this.overlaySize = overlaySize ?? (() => (410, 42));
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveTimer.Tick += (_, _) =>
        {
            saveTimer.Stop();
            PersistSettings();
        };
        MonitorCombo.ItemsSource = Forms.Screen.AllScreens
            .Select(screen => new MonitorOption(screen.DeviceName, $"{screen.DeviceName} {(screen.Primary ? "(Primary)" : string.Empty)}"))
            .ToList();
        LoadSettings();
        WireChanges();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Closed += (_, _) =>
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        };
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
        PositionControlsEnabled = !settings.IsLocked;
    }

    public void RefreshPositionRange()
    {
        if (initialized)
        {
            UpdateOffsetRange();
        }
    }

    private bool PositionControlsEnabled
    {
        set
        {
            PositionCombo.IsEnabled = value;
            MonitorCombo.IsEnabled = value;
            EdgeReferenceCombo.IsEnabled = value;
            OffsetXSlider.IsEnabled = value;
            OffsetYSlider.IsEnabled = value;
            OffsetXBox.IsEnabled = value;
            OffsetYBox.IsEnabled = value;
        }
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
        FontFamilyCombo.SelectedValue = settings.FontFamily;
        FontSizeSlider.Value = settings.FontSize;
        ReadabilityCombo.SelectedValue = settings.Readability.ToString();
        ThemeCombo.SelectedValue = settings.Theme.ToString();
        IntervalCombo.SelectedValue = settings.RefreshIntervalMs.ToString(CultureInfo.InvariantCulture);
        MonitorCombo.SelectedItem = MonitorCombo.Items
            .Cast<MonitorOption>()
            .FirstOrDefault(item => item.DeviceName.Equals(settings.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? MonitorCombo.Items.Cast<MonitorOption>().FirstOrDefault(item =>
                item.DeviceName == (Forms.Screen.PrimaryScreen?.DeviceName ?? string.Empty));
        EdgeReferenceCombo.SelectedIndex = settings.EdgeReference == OverlayEdgeReference.WorkArea ? 0 : 1;
        CpuCheck.IsChecked = settings.ShowCpu;
        GpuCheck.IsChecked = settings.ShowGpu;
        RamCheck.IsChecked = settings.ShowRam;
        FpsCheck.IsChecked = settings.ShowFps;
        OffsetXBox.Text = PositionService.FormatCoordinate(settings.OffsetX);
        OffsetYBox.Text = PositionService.FormatCoordinate(settings.OffsetY);
        UpdateOffsetRange();
        UpdatePresetSelection();
        PositionControlsEnabled = !settings.IsLocked;
        ApplyTheme();
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
        FontFamilyCombo.SelectionChanged += SettingsChangedHandler;
        FontSizeSlider.ValueChanged += SettingsChangedHandler;
        ReadabilityCombo.SelectionChanged += SettingsChangedHandler;
        ThemeCombo.SelectionChanged += SettingsChangedHandler;
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
        OffsetXBox.KeyDown += OffsetKeyDown;
        OffsetYBox.KeyDown += OffsetKeyDown;
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

    private void PositionPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized ||
            PositionCombo.SelectedItem is not ComboBoxItem item ||
            !Enum.TryParse<OverlayPosition>(item.Tag?.ToString(), out var preset) ||
            MonitorCombo.SelectedItem is not MonitorOption monitor)
        {
            return;
        }

        var screen = PositionService.FindScreen(monitor.DeviceName);
        if (screen is null)
        {
            return;
        }

        var (width, height) = GetOverlaySize();
        var coordinates = PositionService.GetPresetCoordinates(
            PositionService.GetMonitorBoundsDip(screen),
            PositionService.GetUsableBoundsDip(screen, settings.EdgeReference),
            width,
            height,
            preset);
        SetOffsetValues(coordinates.X, coordinates.Y);
        ApplySettings();
    }

    private void MonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (initialized)
        {
            UpdateOffsetRange();
            ApplySettings();
        }
    }

    private void BoundsChanged(object sender, SelectionChangedEventArgs e)
    {
        if (initialized)
        {
            UpdateOffsetRange();
            ApplySettings();
        }
    }

    private void OffsetSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!initialized || synchronizingOffsets)
        {
            return;
        }

        var x = ReferenceEquals(sender, OffsetXSlider) ? OffsetXSlider.Value : settings.OffsetX;
        var y = ReferenceEquals(sender, OffsetYSlider) ? OffsetYSlider.Value : settings.OffsetY;
        SetOffsetValues(x, y);
        ApplySettings();
    }

    private void OffsetTextChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized || synchronizingOffsets)
        {
            return;
        }

        var box = (System.Windows.Controls.TextBox)sender;
        var isX = ReferenceEquals(box, OffsetXBox);
        if (!TryParseOffset(
                box.Text,
                isX ? OffsetXSlider.Minimum : OffsetYSlider.Minimum,
                isX ? OffsetXSlider.Maximum : OffsetYSlider.Maximum,
                out var offset))
        {
            box.ToolTip = "Enter a number within the current display range (up to two decimal places).";
            return;
        }

        box.ToolTip = null;
        SetOffsetValues(
            ReferenceEquals(box, OffsetXBox) ? offset : settings.OffsetX,
            ReferenceEquals(box, OffsetYBox) ? offset : settings.OffsetY);
        ApplySettings();
    }

    private void OffsetKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OffsetTextChanged(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void SetOffsetValues(double x, double y)
    {
        synchronizingOffsets = true;
        try
        {
            x = Math.Round(x, 2, MidpointRounding.AwayFromZero);
            y = Math.Round(y, 2, MidpointRounding.AwayFromZero);
            OffsetXSlider.Value = Math.Clamp(x, OffsetXSlider.Minimum, OffsetXSlider.Maximum);
            OffsetYSlider.Value = Math.Clamp(y, OffsetYSlider.Minimum, OffsetYSlider.Maximum);
            OffsetXBox.Text = PositionService.FormatCoordinate(OffsetXSlider.Value);
            OffsetYBox.Text = PositionService.FormatCoordinate(OffsetYSlider.Value);
        }
        finally
        {
            synchronizingOffsets = false;
        }
    }

    private void UpdateOffsetRange()
    {
        if (MonitorCombo.SelectedItem is not MonitorOption monitor ||
            PositionService.FindScreen(monitor.DeviceName) is not { } screen)
        {
            return;
        }

        var (width, height) = GetOverlaySize();
        var range = PositionService.GetOffsetRange(
            PositionService.GetMonitorBoundsDip(screen),
            PositionService.GetUsableBoundsDip(screen, settings.EdgeReference),
            width,
            height);
        synchronizingOffsets = true;
        try
        {
            OffsetXSlider.Minimum = range.MinimumX;
            OffsetXSlider.Maximum = range.MaximumX;
            OffsetYSlider.Minimum = range.MinimumY;
            OffsetYSlider.Maximum = range.MaximumY;
            OffsetXSlider.Value = Math.Clamp(settings.OffsetX, range.MinimumX, range.MaximumX);
            OffsetYSlider.Value = Math.Clamp(settings.OffsetY, range.MinimumY, range.MaximumY);
            settings.OffsetX = Math.Round(OffsetXSlider.Value, 2, MidpointRounding.AwayFromZero);
            settings.OffsetY = Math.Round(OffsetYSlider.Value, 2, MidpointRounding.AwayFromZero);
            OffsetXBox.Text = PositionService.FormatCoordinate(settings.OffsetX);
            OffsetYBox.Text = PositionService.FormatCoordinate(settings.OffsetY);
        }
        finally
        {
            synchronizingOffsets = false;
        }

        UpdatePresetSelection();
    }

    private (double Width, double Height) GetOverlaySize()
    {
        var size = overlaySize();
        return (size.Width > 0 ? size.Width : 410, size.Height > 0 ? size.Height : 42);
    }

    private void UpdatePresetSelection()
    {
        if (MonitorCombo.SelectedItem is not MonitorOption monitor ||
            PositionService.FindScreen(monitor.DeviceName) is not { } screen)
        {
            return;
        }

        var (width, height) = GetOverlaySize();
        var monitorBounds = PositionService.GetMonitorBoundsDip(screen);
        var usableBounds = PositionService.GetUsableBoundsDip(screen, settings.EdgeReference);
        OverlayPosition? matchingPreset = Enum.GetValues<OverlayPosition>()
            .Where(preset =>
            {
                var coordinates = PositionService.GetPresetCoordinates(monitorBounds, usableBounds, width, height, preset);
                return Math.Abs(coordinates.X - settings.OffsetX) < 0.5 &&
                       Math.Abs(coordinates.Y - settings.OffsetY) < 0.5;
            })
            .Select(preset => (OverlayPosition?)preset)
            .FirstOrDefault();
        PositionCombo.SelectedValue = matchingPreset?.ToString() ?? "Custom";
    }

    private void ApplySettings()
    {
        if (!initialized ||
            !TryReadColor(BackgroundColorBox, out var backgroundColor) ||
            !TryReadColor(TextColorBox, out var textColor) ||
            !TryReadColor(AccentColorBox, out var accentColor) ||
            !TryReadColor(SecondaryAccentColorBox, out var secondaryColor) ||
            !TryParseOffset(OffsetXBox.Text, OffsetXSlider.Minimum, OffsetXSlider.Maximum, out var offsetX) ||
            !TryParseOffset(OffsetYBox.Text, OffsetYSlider.Minimum, OffsetYSlider.Maximum, out var offsetY))
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
        settings.FontFamily = FontFamilyCombo.SelectedValue?.ToString() ?? "Segoe UI";
        settings.FontSize = FontSizeSlider.Value;
        settings.ShowCpu = CpuCheck.IsChecked == true;
        settings.ShowGpu = GpuCheck.IsChecked == true;
        settings.ShowRam = RamCheck.IsChecked == true;
        settings.ShowFps = FpsCheck.IsChecked == true;
        settings.Readability = Enum.TryParse<TextReadability>(ReadabilityCombo.SelectedValue?.ToString(), out var readability)
            ? readability
            : TextReadability.Normal;
        settings.Theme = Enum.TryParse<SettingsTheme>(ThemeCombo.SelectedValue?.ToString(), out var theme)
            ? theme
            : SettingsTheme.System;
        settings.OffsetX = offsetX;
        settings.OffsetY = offsetY;
        settings.MonitorDeviceName = (MonitorCombo.SelectedItem as MonitorOption)?.DeviceName ?? string.Empty;
        settings.EdgeReference = EdgeReferenceCombo.SelectedIndex == 0
            ? OverlayEdgeReference.WorkArea
            : OverlayEdgeReference.FullScreen;
        if (IntervalCombo.SelectedItem is ComboBoxItem interval &&
            int.TryParse(interval.Tag?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
        {
            settings.RefreshIntervalMs = milliseconds;
        }

        if (PositionCombo.SelectedValue is string positionName &&
            Enum.TryParse<OverlayPosition>(positionName, out var positionPreset))
        {
            settings.Position = positionPreset;
        }

        settings.Validate();
        PositionControlsEnabled = !settings.IsLocked;
        ApplyTheme();
        UpdatePresetSelection();
        SettingsChanged?.Invoke();
        UpdateOffsetRange();
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void ApplyTheme()
    {
        var dark = settings.Theme == SettingsTheme.Dark ||
                   (settings.Theme == SettingsTheme.System && !SystemUsesLightTheme());
        SetBrush("SettingsWindowBrush", dark ? "#FF171C22" : "#FFF3F5F8");
        SetBrush("SettingsCardBrush", dark ? "#FF202832" : "#FFFFFFFF");
        SetBrush("SettingsForegroundBrush", dark ? "#FFF1F4F7" : "#FF18212B");
        SetBrush("SettingsSecondaryBrush", dark ? "#FFB1BBC6" : "#FF5D6977");
        SetBrush("SettingsAccentBrush", dark ? "#FF57D8CB" : "#FF007F78");
        SetBrush("SettingsControlBrush", dark ? "#FF293440" : "#FFFFFFFF");
        SetBrush("SettingsBorderBrush", dark ? "#FF485665" : "#FFD2D9E1");
        SetBrush("SettingsHoverBrush", dark ? "#FF354A50" : "#FFE5F3F2");
        SetBrush("SettingsSelectedBrush", dark ? "#FF3D5D61" : "#FFD0EAE7");
        Resources[System.Windows.SystemColors.WindowBrushKey] = GetBrush(dark ? "#FF293440" : "#FFFFFFFF");
        Resources[System.Windows.SystemColors.WindowTextBrushKey] = GetBrush(dark ? "#FFF1F4F7" : "#FF18212B");
        Resources[System.Windows.SystemColors.ControlBrushKey] = GetBrush(dark ? "#FF293440" : "#FFF3F5F8");
        Resources[System.Windows.SystemColors.ControlTextBrushKey] = GetBrush(dark ? "#FFF1F4F7" : "#FF18212B");
        Resources[System.Windows.SystemColors.HighlightBrushKey] = GetBrush(dark ? "#FF3D5D61" : "#FFD0EAE7");
        Resources[System.Windows.SystemColors.HighlightTextBrushKey] = GetBrush(dark ? "#FFF1F4F7" : "#FF18212B");
    }

    private static bool SystemUsesLightTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);
            return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            AppLog.Warning($"Unable to read the Windows app theme preference. {exception.Message}");
            return true;
        }
    }

    private void SetBrush(string key, string color)
    {
        Resources[key] = GetBrush(color);
    }

    private static SolidColorBrush GetBrush(string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (settings.Theme == SettingsTheme.System)
        {
            Dispatcher.BeginInvoke(ApplyTheme);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var selectedDevice = (MonitorCombo.SelectedItem as MonitorOption)?.DeviceName;
            MonitorCombo.ItemsSource = Forms.Screen.AllScreens
                .Select(screen => new MonitorOption(screen.DeviceName, $"{screen.DeviceName} {(screen.Primary ? "(Primary)" : string.Empty)}"))
                .ToList();
            MonitorCombo.SelectedItem = MonitorCombo.Items
                .Cast<MonitorOption>()
                .FirstOrDefault(item => item.DeviceName.Equals(selectedDevice, StringComparison.OrdinalIgnoreCase))
                ?? MonitorCombo.Items.Cast<MonitorOption>().FirstOrDefault(item =>
                    item.DeviceName == (Forms.Screen.PrimaryScreen?.DeviceName ?? string.Empty));
            UpdateOffsetRange();
            ApplySettings();
        });
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

    private static bool TryParseOffset(string text, double minimum, double maximum, out double value)
    {
        value = 0;
        if (!double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value) ||
            !double.IsFinite(value) ||
            text.TrimStart('-', '+').Split('.').Skip(1).Any(fraction => fraction.Length > 2))
        {
            return false;
        }

        return value >= minimum && value <= maximum;
    }

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
        destination.PositionCoordinatesVersion = source.PositionCoordinatesVersion;
        destination.MonitorDeviceName = source.MonitorDeviceName;
        destination.RefreshIntervalMs = source.RefreshIntervalMs;
        destination.FontSize = source.FontSize;
        destination.FontFamily = source.FontFamily;
        destination.Opacity = source.Opacity;
        destination.BackgroundEnabled = source.BackgroundEnabled;
        destination.BackgroundColor = source.BackgroundColor;
        destination.BackgroundOpacity = source.BackgroundOpacity;
        destination.TextColor = source.TextColor;
        destination.AccentColor = source.AccentColor;
        destination.SecondaryAccentColor = source.SecondaryAccentColor;
        destination.Theme = source.Theme;
        destination.Readability = source.Readability;
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
