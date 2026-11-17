using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ZufeiBooster;

internal sealed record GpuCheck(bool Running, double Percent, string Verdict);

/// <summary>
/// Reads Windows' own per-process GPU counters (the same ones Task Manager uses) to show
/// whether the emulator renders on the dedicated GPU or on the onboard one.
/// </summary>
internal static class GpuUsage
{
    private static readonly Regex Luid = new(@"luid_(0x[0-9A-Fa-f]+_0x[0-9A-Fa-f]+)", RegexOptions.Compiled);

    public static GpuCheck CheckEmulator(bool hasDedicatedGpu)
    {
        try
        {
            var pids = new HashSet<int>();
            foreach (string name in BoostEngine.EmulatorNames)
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    pids.Add(p.Id);
                    p.Dispose();
                }
            }

            if (pids.Count == 0) return new GpuCheck(false, 0, "emulator is not running");

            var cat = new PerformanceCounterCategory("GPU Engine");
            var mine = cat.GetInstanceNames()
                          .Where(n => pids.Any(pid => n.StartsWith($"pid_{pid}_", StringComparison.Ordinal)))
                          .ToList();

            if (mine.Count == 0)
                return new GpuCheck(true, 0, "no GPU activity seen yet (start the game, then try again)");

            var counters = mine.Select(n => new PerformanceCounter("GPU Engine", "Utilization Percentage", n, true)).ToList();
            try
            {
                foreach (var c in counters) c.NextValue();
                Thread.Sleep(1000);

                double total = 0;
                var perLuid = new Dictionary<string, double>();
                for (int i = 0; i < counters.Count; i++)
                {
                    double v = counters[i].NextValue();
                    string inst = mine[i];
                    if (inst.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase) ||
                        inst.Contains("engtype_Compute", StringComparison.OrdinalIgnoreCase))
                        total += v;

                    string luid = Luid.Match(inst).Groups[1].Value;
                    perLuid[luid] = perLuid.GetValueOrDefault(luid) + v;
                }

                string used = perLuid.OrderByDescending(k => k.Value).First().Key;
                string? dedicated = DedicatedAdapter();

                string where;
                if (!hasDedicatedGpu) where = "on the integrated GPU (your only GPU)";
                else if (dedicated == null) where = "on a GPU (could not tell which one)";
                else where = used == dedicated ? "on the DEDICATED GPU (good)" : "on the INTEGRATED GPU (wrong one - restart the emulator after GPU Boost)";

                return new GpuCheck(true, Math.Min(100, total), $"{where}, 3D load {Math.Min(100, total):0}%");
            }
            finally
            {
                foreach (var c in counters) c.Dispose();
            }
        }
        catch (Exception ex)
        {
            return new GpuCheck(false, 0, "GPU counters are not available on this Windows (" + ex.Message + ")");
        }
    }

    /// <summary>The adapter with the most dedicated video memory in use = the real graphics card.</summary>
    private static string? DedicatedAdapter()
    {
        try
        {
            var cat = new PerformanceCounterCategory("GPU Adapter Memory");
            string? best = null;
            double bestVal = -1;

            foreach (string inst in cat.GetInstanceNames())
            {
                using var c = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", inst, true);
                double v = c.NextValue();
                if (v > bestVal)
                {
                    bestVal = v;
                    var m = Luid.Match(inst);
                    best = m.Success ? m.Groups[1].Value : null;
                }
            }
            return best;
        }
        catch { return null; }
    }
}
