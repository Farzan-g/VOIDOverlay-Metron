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

public sealed class AppSettings
{
    public OverlayPosition Position { get; set; } = OverlayPosition.TopCenter;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public string MonitorDeviceName { get; set; } = string.Empty;
    public int RefreshIntervalMs { get; set; } = 500;
    public double FontSize { get; set; } = 13;
    public double Opacity { get; set; } = 0.92;
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowFps { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool StartOverlayVisible { get; set; } = true;
    public bool KeepOnTop { get; set; } = true;
    public bool IsLocked { get; set; }

    public static AppSettings Defaults() => new();

    public void Validate()
    {
        if (!Enum.IsDefined(Position))
        {
            Position = OverlayPosition.TopCenter;
        }

        if (RefreshIntervalMs is not (250 or 500 or 1000 or 2000))
        {
            RefreshIntervalMs = 500;
        }

        if (!double.IsFinite(FontSize) || FontSize is < 9 or > 32)
        {
            FontSize = 13;
        }

        if (!double.IsFinite(Opacity) || Opacity is < 0.3 or > 1)
        {
            Opacity = 0.92;
        }

        OffsetX = Math.Clamp(OffsetX, -10000, 10000);
        OffsetY = Math.Clamp(OffsetY, -10000, 10000);
        MonitorDeviceName ??= string.Empty;
    }
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
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? AppSettings.Defaults();
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
