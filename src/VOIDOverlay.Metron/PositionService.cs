using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows;
using Forms = System.Windows.Forms;

namespace VOIDOverlay.Metron;

public static class PositionService
{
    private const int MonitorDpiType = 0;
    private const double EdgeMargin = 10;

    public static string FormatCoordinate(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);

    public static void Apply(Window window, AppSettings settings)
    {
        var screen = FindScreen(settings.MonitorDeviceName);
        if (screen is null)
        {
            return;
        }

        var (scaleX, scaleY) = GetScale(screen);
        var monitorBounds = ToDipRect(screen.Bounds, scaleX, scaleY);
        var usableBounds = ToDipRect(
            settings.EdgeReference == OverlayEdgeReference.FullScreen ? screen.Bounds : screen.WorkingArea,
            scaleX,
            scaleY);
        var windowWidth = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var windowHeight = window.ActualHeight > 0 ? window.ActualHeight : window.Height;

        if (settings.PositionCoordinatesVersion == 0)
        {
            var oldPosition = ComputeLegacyPlacement(
                usableBounds,
                windowWidth,
                windowHeight,
                settings.Position,
                settings.OffsetX,
                settings.OffsetY);
            settings.OffsetX = oldPosition.X + windowWidth / 2 - monitorBounds.Left - monitorBounds.Width / 2;
            settings.OffsetY = oldPosition.Y + windowHeight / 2 - monitorBounds.Top - monitorBounds.Height / 2;
            settings.PositionCoordinatesVersion = 1;
        }

        var range = GetOffsetRange(monitorBounds, usableBounds, windowWidth, windowHeight);
        settings.OffsetX = Math.Clamp(settings.OffsetX, range.MinimumX, range.MaximumX);
        settings.OffsetY = Math.Clamp(settings.OffsetY, range.MinimumY, range.MaximumY);
        window.Left = monitorBounds.Left + monitorBounds.Width / 2 + settings.OffsetX - windowWidth / 2;
        window.Top = monitorBounds.Top + monitorBounds.Height / 2 + settings.OffsetY - windowHeight / 2;
    }

    public static (double MinimumX, double MaximumX, double MinimumY, double MaximumY) GetOffsetRange(
        Rect monitorBounds,
        Rect usableBounds,
        double windowWidth,
        double windowHeight)
    {
        var monitorCenterX = monitorBounds.Left + monitorBounds.Width / 2;
        var monitorCenterY = monitorBounds.Top + monitorBounds.Height / 2;
        var horizontalInset = Math.Min(windowWidth, usableBounds.Width) / 2;
        var verticalInset = Math.Min(windowHeight, usableBounds.Height) / 2;
        return (
            usableBounds.Left + horizontalInset - monitorCenterX,
            usableBounds.Right - horizontalInset - monitorCenterX,
            usableBounds.Top + verticalInset - monitorCenterY,
            usableBounds.Bottom - verticalInset - monitorCenterY);
    }

    public static (double X, double Y) GetPresetCoordinates(
        Rect monitorBounds,
        Rect usableBounds,
        double windowWidth,
        double windowHeight,
        OverlayPosition preset)
    {
        var range = GetOffsetRange(monitorBounds, usableBounds, windowWidth, windowHeight);
        var x = preset switch
        {
            OverlayPosition.TopLeft or OverlayPosition.CenterLeft or OverlayPosition.BottomLeft => range.MinimumX + EdgeMargin,
            OverlayPosition.TopCenter or OverlayPosition.BottomCenter or OverlayPosition.Center => 0,
            _ => range.MaximumX - EdgeMargin
        };
        var y = preset switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight => range.MinimumY + EdgeMargin,
            OverlayPosition.CenterLeft or OverlayPosition.Center or OverlayPosition.CenterRight => 0,
            _ => range.MaximumY - EdgeMargin
        };
        return (Math.Clamp(x, range.MinimumX, range.MaximumX), Math.Clamp(y, range.MinimumY, range.MaximumY));
    }

    public static Rect GetMonitorBoundsDip(Forms.Screen screen)
    {
        var (scaleX, scaleY) = GetScale(screen);
        return ToDipRect(screen.Bounds, scaleX, scaleY);
    }

    public static Rect GetUsableBoundsDip(Forms.Screen screen, OverlayEdgeReference edgeReference)
    {
        var (scaleX, scaleY) = GetScale(screen);
        return ToDipRect(edgeReference == OverlayEdgeReference.FullScreen ? screen.Bounds : screen.WorkingArea, scaleX, scaleY);
    }

    public static Forms.Screen? FindScreen(string deviceName) =>
        Forms.Screen.AllScreens.FirstOrDefault(screen =>
            screen.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
        ?? Forms.Screen.PrimaryScreen
        ?? Forms.Screen.AllScreens.FirstOrDefault();

    private static Rect ToDipRect(System.Drawing.Rectangle bounds, double scaleX, double scaleY) =>
        new(bounds.Left / scaleX, bounds.Top / scaleY, bounds.Width / scaleX, bounds.Height / scaleY);

    private static System.Windows.Point ComputeLegacyPlacement(
        Rect workArea,
        double windowWidth,
        double windowHeight,
        OverlayPosition position,
        double offsetX,
        double offsetY)
    {
        var x = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft or OverlayPosition.CenterLeft => workArea.Left + EdgeMargin,
            OverlayPosition.TopCenter or OverlayPosition.BottomCenter or OverlayPosition.Center => workArea.Left + (workArea.Width - windowWidth) / 2,
            _ => workArea.Right - windowWidth - EdgeMargin
        } + offsetX;
        var y = position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.TopCenter or OverlayPosition.TopRight => workArea.Top + EdgeMargin,
            OverlayPosition.CenterLeft or OverlayPosition.Center or OverlayPosition.CenterRight => workArea.Top + (workArea.Height - windowHeight) / 2,
            _ => workArea.Bottom - windowHeight - EdgeMargin
        } + offsetY;
        return new System.Windows.Point(
            Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - windowWidth)),
            Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - windowHeight)));
    }

    private static (double X, double Y) GetScale(Forms.Screen screen)
    {
        try
        {
            var monitor = MonitorFromPoint(new NativePoint(screen.WorkingArea.Left + 1, screen.WorkingArea.Top + 1), 2);
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MonitorDpiType, out var dpiX, out var dpiY) == 0)
            {
                return (dpiX / 96d, dpiY / 96d);
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        return (1, 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
}
