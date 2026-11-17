using System.Diagnostics;

namespace ZufeiBooster;

/// <summary>GPU load / temperature through nvidia-smi (installed with NVIDIA drivers). Other brands: not available.</summary>
internal static class GpuStats
{
    private static bool unavailable;

    public static (bool ok, int util, int temp, int usedMb, int totalMb) Query()
    {
        if (unavailable) return (false, 0, 0, 0, 0);

        try
        {
            var psi = new ProcessStartInfo("nvidia-smi",
                "--query-gpu=utilization.gpu,temperature.gpu,memory.used,memory.total --format=csv,noheader,nounits")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) { unavailable = true; return (false, 0, 0, 0, 0); }

            string line = (p.StandardOutput.ReadLine() ?? "").Trim();
            p.WaitForExit(3000);

            var parts = line.Split(',').Select(x => x.Trim()).ToArray();
            if (parts.Length >= 4 &&
                int.TryParse(parts[0], out int u) && int.TryParse(parts[1], out int t) &&
                int.TryParse(parts[2], out int m) && int.TryParse(parts[3], out int tm))
                return (true, u, t, m, tm);
        }
        catch { unavailable = true; }

        return (false, 0, 0, 0, 0);
    }

    public static (bool ok, string pstate, int curMhz, int maxMhz) Clocks()
    {
        if (unavailable) return (false, "", 0, 0);

        try
        {
            var psi = new ProcessStartInfo("nvidia-smi",
                "--query-gpu=pstate,clocks.current.graphics,clocks.max.graphics --format=csv,noheader,nounits")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return (false, "", 0, 0);

            string line = (p.StandardOutput.ReadLine() ?? "").Trim();
            p.WaitForExit(3000);

            var parts = line.Split(',').Select(x => x.Trim()).ToArray();
            if (parts.Length >= 3 && int.TryParse(parts[1], out int cur) && int.TryParse(parts[2], out int max))
                return (true, parts[0], cur, max);
        }
        catch { }

        return (false, "", 0, 0);
    }
}
