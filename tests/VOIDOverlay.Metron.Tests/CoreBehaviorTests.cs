using System.Windows;
using System.IO;

namespace VOIDOverlay.Metron.Tests;

public sealed class CoreBehaviorTests
{
    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(25, 100, 75)]
    [InlineData(100, 100, 0)]
    public void CpuUtilizationNormalizesIdleAndTotalDeltas(ulong idle, ulong total, double expected)
    {
        var reading = CpuSampler.NormalizeDelta(idle, total);

        Assert.True(reading.IsAvailable);
        Assert.Equal(expected, reading.Value);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(11, 10)]
    public void CpuUtilizationRejectsInvalidDeltas(ulong idle, ulong total)
    {
        var reading = CpuSampler.NormalizeDelta(idle, total);

        Assert.Equal(MetricState.Unavailable, reading.State);
    }

    [Fact]
    public void MemoryCalculatesPhysicalUsageInBinaryUnits()
    {
        const ulong gib = 1024UL * 1024 * 1024;

        var reading = MemoryReading.FromBytes(16 * gib, 4 * gib);

        Assert.True(reading.IsAvailable);
        Assert.Equal(12, reading.UsedGiB, 6);
        Assert.Equal(16, reading.TotalGiB, 6);
        Assert.Equal(75, reading.Percent, 6);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 11)]
    public void MemoryRejectsImpossibleByteCounts(ulong total, ulong available)
    {
        Assert.False(MemoryReading.FromBytes(total, available).IsAvailable);
    }

    [Fact]
    public void ConfigurationUsesDefaultsWhenMissingAndPersistsValidValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"metron-settings-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            Assert.Equal(500, store.Load().RefreshIntervalMs);
            Assert.False(store.Load().BackgroundEnabled);

            var settings = AppSettings.Defaults();
            settings.Position = OverlayPosition.BottomRight;
            settings.OffsetX = 44;
            settings.BackgroundEnabled = true;
            settings.ShowCpu = false;
            settings.ShowGpu = true;
            settings.ShowRam = false;
            settings.ShowFps = false;
            settings.Theme = SettingsTheme.Dark;
            settings.Readability = TextReadability.HighContrast;
            store.Save(settings);

            var loaded = store.Load();
            Assert.Equal(OverlayPosition.BottomRight, loaded.Position);
            Assert.Equal(44, loaded.OffsetX);
            Assert.True(loaded.BackgroundEnabled);
            Assert.False(loaded.ShowCpu);
            Assert.True(loaded.ShowGpu);
            Assert.False(loaded.ShowRam);
            Assert.False(loaded.ShowFps);
            Assert.Equal(SettingsTheme.Dark, loaded.Theme);
            Assert.Equal(TextReadability.HighContrast, loaded.Readability);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(path + ".tmp"))
            {
                File.Delete(path + ".tmp");
            }
        }
    }

    [Fact]
    public void ConfigurationRecoversFromMalformedJsonAndValidatesValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"metron-settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"RefreshIntervalMs":37,"FontSize":500,"Position":99}""");
            var loaded = new SettingsStore(path).Load();

            Assert.Equal(500, loaded.RefreshIntervalMs);
            Assert.Equal(24, loaded.FontSize);
            Assert.Equal(OverlayPosition.TopCenter, loaded.Position);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ConfigurationUsesTransparentBackgroundWhenSettingIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"metron-settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"Position":1}""");

            var loaded = new SettingsStore(path).Load();

            Assert.False(loaded.BackgroundEnabled);
            Assert.Equal(OverlayPosition.TopCenter, loaded.Position);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void AppearanceDefaultsMatchProductFamilyStandard()
    {
        var settings = AppSettings.Defaults();

        Assert.Equal(OverlayPosition.TopCenter, settings.Position);
        Assert.Equal(OverlayEdgeReference.WorkArea, settings.EdgeReference);
        Assert.Equal(13, settings.FontSize);
        Assert.Equal(1, settings.Opacity);
        Assert.False(settings.BackgroundEnabled);
        Assert.Equal("#111720", settings.BackgroundColor);
        Assert.Equal(0.5, settings.BackgroundOpacity);
        Assert.Equal("#F2F4F7", settings.TextColor);
        Assert.Equal("#4DE3D2", settings.AccentColor);
        Assert.Equal("#FFAE5C", settings.SecondaryAccentColor);
        Assert.Equal("Segoe UI", settings.FontFamily);
        Assert.Equal(SettingsTheme.System, settings.Theme);
        Assert.Equal(TextReadability.Normal, settings.Readability);
        Assert.True(settings.IsLocked);
        Assert.True(settings.ClickThrough);
    }

    [Fact]
    public void SettingsValidationAppliesSharedAppearanceAndFiniteCoordinateLimits()
    {
        var settings = new AppSettings
        {
            FontSize = 50,
            Opacity = double.NaN,
            BackgroundOpacity = 5,
            BackgroundColor = "invalid",
            OffsetX = -50000,
            OffsetY = 50000
        };

        settings.Validate();

        Assert.Equal(24, settings.FontSize);
        Assert.Equal(1, settings.Opacity);
        Assert.Equal(1, settings.BackgroundOpacity);
        Assert.Equal("#111720", settings.BackgroundColor);
        Assert.Equal(-10000, settings.OffsetX);
        Assert.Equal(10000, settings.OffsetY);
    }

    [Fact]
    public void PositionRangesAndPresetsUseMonitorCenteredCoordinates()
    {
        var monitor = new Rect(0, 0, 1920, 1080);
        var usableArea = new Rect(0, 0, 1920, 1040);
        var range = PositionService.GetOffsetRange(monitor, usableArea, 400, 40);
        var topLeft = PositionService.GetPresetCoordinates(
            monitor, usableArea, 400, 40, OverlayPosition.TopLeft);
        var center = PositionService.GetPresetCoordinates(
            monitor, usableArea, 400, 40, OverlayPosition.Center);
        var bottomRight = PositionService.GetPresetCoordinates(
            monitor, usableArea, 400, 40, OverlayPosition.BottomRight);

        Assert.Equal(-760, range.MinimumX);
        Assert.Equal(760, range.MaximumX);
        Assert.Equal(-520, range.MinimumY);
        Assert.Equal(480, range.MaximumY);
        Assert.Equal((-750, -510), topLeft);
        Assert.Equal((0, 0), center);
        Assert.Equal((750, 470), bottomRight);
    }

    [Theory]
    [InlineData(OverlayPosition.TopLeft, -750, -510)]
    [InlineData(OverlayPosition.TopCenter, 0, -510)]
    [InlineData(OverlayPosition.TopRight, 750, -510)]
    [InlineData(OverlayPosition.CenterLeft, -750, 0)]
    [InlineData(OverlayPosition.Center, 0, 0)]
    [InlineData(OverlayPosition.CenterRight, 750, 0)]
    [InlineData(OverlayPosition.BottomLeft, -750, 470)]
    [InlineData(OverlayPosition.BottomCenter, 0, 470)]
    [InlineData(OverlayPosition.BottomRight, 750, 470)]
    public void EachPositionPresetUsesDisplaySizeAndOverlaySize(
        OverlayPosition preset,
        double expectedX,
        double expectedY)
    {
        var coordinates = PositionService.GetPresetCoordinates(
            new Rect(0, 0, 1920, 1080),
            new Rect(0, 0, 1920, 1040),
            400,
            40,
            preset);

        Assert.Equal(expectedX, coordinates.X);
        Assert.Equal(expectedY, coordinates.Y);
    }

    [Theory]
    [InlineData(10.0, "10")]
    [InlineData(-10.5, "-10.5")]
    [InlineData(3.141, "3.14")]
    [InlineData(0, "0")]
    public void CoordinateFormattingIsCompactAndLimitedToTwoDecimals(double value, string expected)
    {
        Assert.Equal(expected, PositionService.FormatCoordinate(value));
    }

    [Fact]
    public void ExistingAnchorSettingsAreMarkedForSafeCoordinateMigration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"metron-settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{"Position":5,"OffsetX":40,"OffsetY":-25}""");

            var loaded = new SettingsStore(path).Load();

            Assert.Equal(OverlayPosition.BottomRight, loaded.Position);
            Assert.Equal(40, loaded.OffsetX);
            Assert.Equal(-25, loaded.OffsetY);
            Assert.Equal(0, loaded.PositionCoordinatesVersion);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(250, 250)]
    [InlineData(500, 500)]
    [InlineData(1000, 1000)]
    [InlineData(2000, 2000)]
    [InlineData(1, 500)]
    public void RefreshIntervalIsRestrictedToSupportedOptions(int value, int expected)
    {
        var settings = AppSettings.Defaults();
        settings.RefreshIntervalMs = value;
        settings.Validate();

        Assert.Equal(expected, settings.RefreshIntervalMs);
    }

    [Fact]
    public void MetricReadingsDistinguishInitializingUnavailableAndAvailable()
    {
        Assert.Equal(MetricState.Initializing, MetricReading<double>.Initializing().State);
        Assert.Equal(MetricState.Unavailable, MetricReading<double>.Unavailable().State);
        Assert.Equal(0, MetricReading<double>.Available(0).Value);
    }

    [Fact]
    public void FpsCacheTransitionsToActiveAndExpiresStaleSamples()
    {
        var cache = new FpsReadingCache(TimeSpan.FromSeconds(2));
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(MetricState.Initializing, cache.Read(now).State);
        Assert.True(cache.Publish(120, now));
        Assert.Equal(120, cache.Read(now.AddSeconds(1)).Value);
        Assert.Equal(MetricState.Unavailable, cache.Read(now.AddSeconds(3)).State);
    }

    [Fact]
    public void FpsCacheRejectsInvalidMeasurements()
    {
        var cache = new FpsReadingCache(TimeSpan.FromSeconds(2));

        Assert.False(cache.Publish(double.NaN, DateTimeOffset.UtcNow));
        Assert.Equal(MetricState.Unavailable, cache.Read(DateTimeOffset.UtcNow).State);
    }

    [Fact]
    public void MissingFpsProviderReportsUnavailableInsteadOfZero()
    {
        using var provider = new UnavailableFpsProvider();

        Assert.Equal(MetricState.Unavailable, provider.Read().State);
    }

    [Fact]
    public void FpsProviderFailureIsLoggedAndConvertedToUnavailable()
    {
        var reading = FpsProviderBoundary.Read(new ThrowingFpsProvider());

        Assert.Equal(MetricState.Unavailable, reading.State);
    }

    private sealed class ThrowingFpsProvider : IFpsProvider
    {
        public string Name => "test provider";

        public MetricReading<double> Read() => throw new InvalidOperationException("test failure");

        public void Dispose()
        {
        }
    }
}
