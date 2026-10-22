using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VOIDOverlay.Metron;

public sealed class CpuSampler
{
    private ulong? previousIdle;
    private ulong? previousTotal;

    public MetricReading<double> Sample()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetSystemTimes failed.");
        }

        var currentIdle = ToUInt64(idle);
        var currentTotal = ToUInt64(kernel) + ToUInt64(user);
        if (previousIdle is null || previousTotal is null)
        {
            previousIdle = currentIdle;
            previousTotal = currentTotal;
            return MetricReading<double>.Initializing();
        }

        if (currentIdle < previousIdle.Value || currentTotal < previousTotal.Value)
        {
            previousIdle = currentIdle;
            previousTotal = currentTotal;
            return MetricReading<double>.Initializing("System time counters reset.");
        }

        var idleDelta = currentIdle - previousIdle.Value;
        var totalDelta = currentTotal - previousTotal.Value;
        previousIdle = currentIdle;
        previousTotal = currentTotal;

        if (totalDelta == 0 || idleDelta > totalDelta)
        {
            return MetricReading<double>.Unavailable("Invalid system time delta.");
        }

        var percent = (totalDelta - idleDelta) * 100d / totalDelta;
        return double.IsFinite(percent) && percent is >= 0 and <= 100
            ? MetricReading<double>.Available(percent)
            : MetricReading<double>.Unavailable("Invalid CPU utilization sample.");
    }

    public static MetricReading<double> NormalizeDelta(ulong idleDelta, ulong totalDelta)
    {
        if (totalDelta == 0 || idleDelta > totalDelta)
        {
            return MetricReading<double>.Unavailable("Invalid system time delta.");
        }

        return MetricReading<double>.Available((totalDelta - idleDelta) * 100d / totalDelta);
    }

    private static ulong ToUInt64(SystemFileTime time) => ((ulong)time.High << 32) | time.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemFileTime
    {
        public uint Low;
        public uint High;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out SystemFileTime idleTime, out SystemFileTime kernelTime, out SystemFileTime userTime);
}

public sealed class RamSampler
{
    public MemoryReading Sample()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GlobalMemoryStatusEx failed.");
        }

        return MemoryReading.FromBytes(status.TotalPhysical, status.AvailablePhysical);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}

public sealed class GpuSampler : IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhValidData = 0;
    private const uint PdhNewData = 1;
    private IntPtr query;
    private IntPtr counter;

    public GpuSampler()
    {
        Check(PdhOpenQuery(null, UIntPtr.Zero, out query), "PdhOpenQuery");
        var status = PdhAddEnglishCounter(query, @"\GPU Engine(*)\Utilization Percentage", UIntPtr.Zero, out counter);
        if (status != 0)
        {
            PdhCloseQuery(query);
            query = IntPtr.Zero;
            throw new Win32Exception((int)status, "GPU Engine performance counter is unavailable.");
        }

        Check(PdhCollectQueryData(query), "PdhCollectQueryData");
    }

    public MetricReading<double> Sample()
    {
        Check(PdhCollectQueryData(query), "PdhCollectQueryData");
        uint bufferSize = 0;
        uint itemCount = 0;
        var status = PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, ref itemCount, IntPtr.Zero);
        if (status == PdhMoreData && bufferSize > 0)
        {
            var buffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                status = PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, ref itemCount, buffer);
                if (status != 0)
                {
                    return MetricReading<double>.Unavailable($"GPU counter query failed (0x{status:X8}).");
                }

                var itemSize = Marshal.SizeOf<PdhFormattedCounterItem>();
                var valueOffset = Marshal.OffsetOf<PdhFormattedCounterItem>(nameof(PdhFormattedCounterItem.Value)).ToInt32();
                double? busiestEnginePercent = null;
                for (var index = 0; index < itemCount; index++)
                {
                    var item = IntPtr.Add(buffer, index * itemSize);
                    var value = Marshal.PtrToStructure<PdhCounterValue>(IntPtr.Add(item, valueOffset));
                    if (value.Status is not (PdhValidData or PdhNewData) || !double.IsFinite(value.DoubleValue))
                    {
                        continue;
                    }

                    busiestEnginePercent = Math.Max(busiestEnginePercent ?? 0, Math.Clamp(value.DoubleValue, 0, 100));
                }

                return busiestEnginePercent is { } percent
                    ? MetricReading<double>.Available(percent)
                    : MetricReading<double>.Unavailable("GPU engine counters returned no valid data.");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return MetricReading<double>.Unavailable($"GPU counter query failed (0x{status:X8}).");
    }

    public void Dispose()
    {
        if (query != IntPtr.Zero)
        {
            PdhCloseQuery(query);
            query = IntPtr.Zero;
            counter = IntPtr.Zero;
        }
    }

    private static void Check(uint status, string operation)
    {
        if (status != 0)
        {
            throw new Win32Exception((int)status, $"{operation} failed (0x{status:X8}).");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhFormattedCounterItem
    {
        public IntPtr Name;
        public PdhCounterValue Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PdhCounterValue
    {
        public uint Status;
        public double DoubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, UIntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string counterPath, UIntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, ref uint itemCount, IntPtr itemBuffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}

public interface IFpsProvider : IDisposable
{
    string Name { get; }
    MetricReading<double> Read();
}

public sealed class UnavailableFpsProvider : IFpsProvider
{
    public string Name => "No supported frame-statistics provider configured";

    public MetricReading<double> Read() => MetricReading<double>.Unavailable("No game FPS provider is configured.");

    public void Dispose()
    {
    }
}

public static class FpsProviderBoundary
{
    public static MetricReading<double> Read(IFpsProvider provider)
    {
        try
        {
            var reading = provider.Read();
            if (reading.IsAvailable && (!double.IsFinite(reading.Value) || reading.Value <= 0 || reading.Value > 1000))
            {
                AppLog.Warning("FPS provider returned a value outside the accepted range.");
                return MetricReading<double>.Unavailable("FPS provider returned an invalid value.");
            }

            return reading;
        }
        catch (Exception ex)
        {
            AppLog.Warning($"FPS provider failed. {ex.Message}");
            return MetricReading<double>.Unavailable(ex.Message);
        }
    }
}

public sealed class FpsReadingCache(TimeSpan staleAfter)
{
    private MetricReading<double> reading = MetricReading<double>.Initializing();
    private DateTimeOffset? receivedAt;

    public bool Publish(double framesPerSecond, DateTimeOffset timestamp)
    {
        if (!double.IsFinite(framesPerSecond) || framesPerSecond <= 0 || framesPerSecond > 1000)
        {
            Invalidate("FPS sample was outside the valid range.");
            return false;
        }

        reading = MetricReading<double>.Available(framesPerSecond);
        receivedAt = timestamp;
        return true;
    }

    public void Invalidate(string? reason = null)
    {
        receivedAt = null;
        reading = MetricReading<double>.Unavailable(reason);
    }

    public MetricReading<double> Read(DateTimeOffset now)
    {
        if (receivedAt is null)
        {
            return reading;
        }

        if (now - receivedAt.Value > staleAfter)
        {
            Invalidate("FPS sample expired.");
        }

        return reading;
    }
}

public sealed class MetricsService : IDisposable
{
    private readonly CpuSampler cpu = new();
    private readonly RamSampler memory = new();
    private readonly IFpsProvider fps = new UnavailableFpsProvider();
    private readonly CancellationTokenSource cancellation = new();
    private readonly object lifecycleLock = new();
    private Task? loop;
    private GpuSampler? gpu;
    private DateTime nextGpuRetry = DateTime.MinValue;
    private MetricReading<double> cpuReading = MetricReading<double>.Initializing();
    private MetricReading<double> gpuReading = MetricReading<double>.Initializing();
    private MemoryReading memoryReading = MemoryReading.Unavailable;
    private MetricReading<double> fpsReading = MetricReading<double>.Unavailable();
    private int refreshIntervalMs;

    public MetricsService(int refreshIntervalMs)
    {
        this.refreshIntervalMs = refreshIntervalMs;
    }

    public event Action<MetricSnapshot>? SnapshotAvailable;

    public void Start()
    {
        lock (lifecycleLock)
        {
            loop ??= RunAsync(cancellation.Token);
        }
    }

    public void SetRefreshInterval(int milliseconds) => Volatile.Write(ref refreshIntervalMs, milliseconds);

    public void Dispose()
    {
        cancellation.Cancel();
        try
        {
            loop?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        gpu?.Dispose();
        fps.Dispose();
        cancellation.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Run(SampleAll, token).ConfigureAwait(false);
                SnapshotAvailable?.Invoke(new MetricSnapshot(cpuReading, gpuReading, memoryReading, fpsReading));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Unexpected metrics sampling failure. {ex.Message}");
            }

            await Task.Delay(Volatile.Read(ref refreshIntervalMs), token).ConfigureAwait(false);
        }
    }

    private void SampleAll()
    {
        try
        {
            cpuReading = cpu.Sample();
        }
        catch (Exception ex)
        {
            cpuReading = MetricReading<double>.Unavailable(ex.Message);
            AppLog.Error($"CPU sampling failed. {ex.Message}");
        }

        if (gpu is not null || DateTime.UtcNow >= nextGpuRetry)
        {
            try
            {
                gpu ??= new GpuSampler();
                gpuReading = gpu.Sample();
            }
            catch (Exception ex) when (ex is Win32Exception or DllNotFoundException or EntryPointNotFoundException)
            {
                gpu?.Dispose();
                gpu = null;
                gpuReading = MetricReading<double>.Unavailable(ex.Message);
                nextGpuRetry = DateTime.UtcNow.AddSeconds(30);
                AppLog.Warning($"GPU utilization is unavailable. {ex.Message}");
            }
        }

        try
        {
            memoryReading = memory.Sample();
        }
        catch (Exception ex)
        {
            memoryReading = MemoryReading.Unavailable;
            AppLog.Error($"RAM sampling failed. {ex.Message}");
        }

        fpsReading = FpsProviderBoundary.Read(fps);
    }
}
