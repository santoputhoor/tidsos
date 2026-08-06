using System.Diagnostics;

namespace TidsOS.Agent;

// Best-effort, dependency-free system vitals. This sums TotalProcessorTime
// across all visible processes between two samples to approximate
// system-wide CPU usage — it's an estimate, not a precise OS counter, and
// deliberately avoids platform-specific APIs (PerformanceCounter, /proc)
// to keep the agent trivially cross-platform for the MVP.
public sealed class SystemVitals
{
    private DateTimeOffset _lastSampledAt;
    private TimeSpan _lastTotalCpuTime;

    public double SampleCpuUsagePercent()
    {
        var now = DateTimeOffset.UtcNow;
        var totalCpuTime = TimeSpan.Zero;

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                totalCpuTime += process.TotalProcessorTime;
            }
            catch
            {
                // Process exited or is inaccessible between enumeration and read — skip it.
            }
            finally
            {
                process.Dispose();
            }
        }

        if (_lastSampledAt == default)
        {
            _lastSampledAt = now;
            _lastTotalCpuTime = totalCpuTime;
            return 0;
        }

        var wallElapsedMs = (now - _lastSampledAt).TotalMilliseconds;
        var cpuElapsedMs = (totalCpuTime - _lastTotalCpuTime).TotalMilliseconds;

        _lastSampledAt = now;
        _lastTotalCpuTime = totalCpuTime;

        if (wallElapsedMs <= 0)
        {
            return 0;
        }

        return Math.Clamp(cpuElapsedMs / (wallElapsedMs * Environment.ProcessorCount) * 100, 0, 100);
    }

    public static long AvailableMemoryMb() => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
}
