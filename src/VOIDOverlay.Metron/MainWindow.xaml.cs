using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace VOIDOverlay.Metron;

public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WmAppShow = 0x8001;
    private readonly AppSettings settings;

    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        this.settings = settings;
        SourceInitialized += OnSourceInitialized;
        SizeChanged += (_, _) => PositionService.Apply(this, this.settings);
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        ApplyAppearance();
        UpdateMetrics(new MetricSnapshot(
            MetricReading<double>.Initializing(),
            MetricReading<double>.Initializing(),
            MemoryReading.Unavailable,
            MetricReading<double>.Initializing()));
        new WindowInteropHelper(this).EnsureHandle();
        PositionService.Apply(this, settings);
    }

    public void UpdateMetrics(MetricSnapshot snapshot)
    {
        CpuText.Text = $"CPU  {Format(snapshot.Cpu)}";
        GpuText.Text = $"GPU  {Format(snapshot.Gpu)}";
        RamText.Text = snapshot.Memory.IsAvailable
            ? $"RAM  {snapshot.Memory.UsedGiB:F1} / {snapshot.Memory.TotalGiB:F1} GiB ({snapshot.Memory.Percent:F0}%)"
            : "RAM  —";
        FpsText.Text = $"FPS  {FormatFps(snapshot.Fps)}";
    }

    public void ApplyAppearance()
    {
        FontSize = settings.FontSize;
        Opacity = settings.Opacity;
        Topmost = settings.KeepOnTop;
        CpuText.Visibility = settings.ShowCpu ? Visibility.Visible : Visibility.Collapsed;
        GpuText.Visibility = settings.ShowGpu ? Visibility.Visible : Visibility.Collapsed;
        RamText.Visibility = settings.ShowRam ? Visibility.Visible : Visibility.Collapsed;
        FpsText.Visibility = settings.ShowFps ? Visibility.Visible : Visibility.Collapsed;
        MetricsPanel.Children.Clear();
        var visibleMetrics = new[]
        {
            (CpuText, settings.ShowCpu),
            (GpuText, settings.ShowGpu),
            (RamText, settings.ShowRam),
            (FpsText, settings.ShowFps)
        }.Where(item => item.Item2).Select(item => item.Item1).ToArray();
        for (var index = 0; index < visibleMetrics.Length; index++)
        {
            if (index > 0)
            {
                MetricsPanel.Children.Add(new TextBlock { Text = "  |  ", Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 255, 255)) });
            }

            MetricsPanel.Children.Add(visibleMetrics[index]);
        }

        PositionService.Apply(this, settings);
    }

    public void RevealFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }

        Topmost = settings.KeepOnTop;
        PositionService.Apply(this, settings);
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        base.OnClosed(e);
    }

    private static string Format(MetricReading<double> reading) =>
        reading.IsAvailable ? $"{reading.Value:F0}%" : "—";

    private static string FormatFps(MetricReading<double> reading) =>
        reading.IsAvailable ? $"{reading.Value:F0}" : "—";

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var styles = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        styles |= WsExTransparent | WsExToolWindow | WsExNoActivate;
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(styles));
        HwndSource.FromHwnd(handle)?.AddHook(WindowMessageHook);
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmAppShow)
        {
            RevealFromTray();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => PositionService.Apply(this, settings));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
