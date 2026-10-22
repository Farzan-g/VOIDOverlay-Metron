using System.Text.Json;
using System.IO;

namespace VOIDOverlay.Metron;

public enum OverlayPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public enum OverlayEdgeReference
{
    WorkArea,
    FullScreen
}

public sealed class AppSettings
{
    public OverlayPosition Position { get; set; } = OverlayPosition.TopCenter;
    public OverlayEdgeReference EdgeReference { get; set; } = OverlayEdgeReference.WorkArea;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public string MonitorDeviceName { get; set; } = string.Empty;
    public int RefreshIntervalMs { get; set; } = 500;
    public double FontSize { get; set; } = 13;
    public double Opacity { get; set; } = 1;
    public bool BackgroundEnabled { get; set; } = true;
    public string BackgroundColor { get; set; } = "#111720";
    public double BackgroundOpacity { get; set; } = 0.5;
    public string TextColor { get; set; } = "#F2F4F7";
    public string AccentColor { get; set; } = "#4DE3D2";
    public string SecondaryAccentColor { get; set; } = "#FFAE5C";
    public bool ClickThrough { get; set; } = true;
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowFps { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool StartOverlayVisible { get; set; } = true;
    public bool KeepOnTop { get; set; } = true;
    public bool IsLocked { get; set; } = true;

    public static AppSettings Defaults() => new();

    public void Validate()
    {
        if (!Enum.IsDefined(Position))
        {
            Position = OverlayPosition.TopCenter;
        }

        if (!Enum.IsDefined(EdgeReference))
        {
            EdgeReference = OverlayEdgeReference.WorkArea;
        }

        if (RefreshIntervalMs is not (250 or 500 or 750 or 1000 or 2000 or 5000))
        {
            RefreshIntervalMs = 500;
        }

        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize, 9, 24) : 13;
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0.3, 1) : 1;
        BackgroundOpacity = double.IsFinite(BackgroundOpacity) ? Math.Clamp(BackgroundOpacity, 0, 1) : 0.5;
        OffsetX = Math.Clamp(OffsetX, -200, 1000);
        OffsetY = Math.Clamp(OffsetY, -200, 1000);
        MonitorDeviceName ??= string.Empty;
        BackgroundColor = ValidColorOr(BackgroundColor, "#111720");
        TextColor = ValidColorOr(TextColor, "#F2F4F7");
        AccentColor = ValidColorOr(AccentColor, "#4DE3D2");
        SecondaryAccentColor = ValidColorOr(SecondaryAccentColor, "#FFAE5C");
    }

    private static string ValidColorOr(string? value, string fallback) =>
        value is { Length: 7 } &&
        value[0] == '#' &&
        value.Skip(1).All(Uri.IsHexDigit)
            ? value.ToUpperInvariant()
            : fallback;
}

public sealed class SettingsStore
{
    private readonly string path;

    public SettingsStore(string? settingsPath = null)
    {
        path = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VOIDOverlay",
            "Metron",
            "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(path))
        {
            return AppSettings.Defaults();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))
                ?? throw new JsonException("The settings document must contain an object.");
            settings.Validate();
            return settings;
        }
        catch (JsonException ex)
        {
            AppLog.Warning($"Settings could not be parsed; defaults were loaded. {ex.Message}");
            return AppSettings.Defaults();
        }
        catch (IOException ex)
        {
            AppLog.Error($"Settings could not be read; defaults were loaded. {ex.Message}");
            return AppSettings.Defaults();
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLog.Error($"Settings could not be read; defaults were loaded. {ex.Message}");
            return AppSettings.Defaults();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
