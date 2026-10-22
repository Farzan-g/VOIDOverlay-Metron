namespace VOIDOverlay.Metron;

public enum MetricState
{
    Initializing,
    Available,
    Unavailable
}

public readonly record struct MetricReading<T>(MetricState State, T Value, string? Detail = null)
{
    public bool IsAvailable => State == MetricState.Available;

    public static MetricReading<T> Initializing(string? detail = null) =>
        new(MetricState.Initializing, default!, detail);

    public static MetricReading<T> Available(T value) =>
        new(MetricState.Available, value);

    public static MetricReading<T> Unavailable(string? detail = null) =>
        new(MetricState.Unavailable, default!, detail);
}

public readonly record struct MemoryReading(bool IsAvailable, double UsedGiB, double TotalGiB, double Percent)
{
    public static MemoryReading Unavailable => new(false, 0, 0, 0);

    public static MemoryReading FromBytes(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0 || availableBytes > totalBytes)
        {
            return Unavailable;
        }

        var usedBytes = totalBytes - availableBytes;
        const double bytesPerGiB = 1024d * 1024 * 1024;
        return new MemoryReading(
            true,
            usedBytes / bytesPerGiB,
            totalBytes / bytesPerGiB,
            usedBytes * 100d / totalBytes);
    }
}

public readonly record struct MetricSnapshot(
    MetricReading<double> Cpu,
    MetricReading<double> Gpu,
    MemoryReading Memory,
    MetricReading<double> Fps);
