using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ZufeiBooster;

internal static class SysInfo
{
    // ---------- registry helpers ----------
    private static string Reg(string path, string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return (key?.GetValue(name)?.ToString() ?? "").Trim();
        }
        catch { return ""; }
    }

    public static string Cpu()
    {
        string n = Reg(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString");
        return string.IsNullOrWhiteSpace(n) ? "Unknown CPU" : System.Text.RegularExpressions.Regex.Replace(n, @"\s+", " ");
    }

    private static readonly System.Text.RegularExpressions.Regex DedicatedGpu = new(
        @"NVIDIA|GeForce|GTX|RTX|Quadro|Radeon\s*(RX|R\d|Pro|HD\s*\d{4})|\bRX\s*\d|Intel\(R\)\s*Arc",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static bool IsDedicatedGpu(string name) => DedicatedGpu.IsMatch(name);

    /// <summary>Returns main GPU (dedicated card preferred), its VRAM text, other adapters, and whether main is dedicated.</summary>
    public static (string name, string vram, string extra, bool dedicated) Gpu()
    {
        const string keyPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        var found = new List<(string name, long vram)>();
        string basic = "";

        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(keyPath);
            if (root != null)
            {
                foreach (string sub in root.GetSubKeyNames())
                {
                    if (!sub.All(char.IsDigit)) continue;
                    using var key = root.OpenSubKey(sub);
                    string? name = (key?.GetValue("DriverDesc") as string)?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase))
                    { basic = name; continue; }
                    if (name.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Mirror", StringComparison.OrdinalIgnoreCase)) continue;

                    object? v = key!.GetValue("HardwareInformation.qwMemorySize");
                    long bytes = v switch
                    {
                        long l => l,
                        int i => (uint)i,
                        byte[] bt when bt.Length >= 8 => BitConverter.ToInt64(bt, 0),
                        byte[] bt when bt.Length >= 4 => BitConverter.ToUInt32(bt, 0),
                        _ => 0
                    };
                    if (!found.Any(f => f.name == name)) found.Add((name, bytes));
                }
            }
        }
        catch { }

        if (found.Count == 0)
        {
            if (basic != "") return (basic + " (install the GPU driver)", "", "", false);
            return ("Unknown GPU", "", "", false);
        }

        var main = found.FirstOrDefault(f => IsDedicatedGpu(f.name));
        if (main.name == null) main = found[0];

        string vram = main.vram > 0 ? $"{main.vram / 1048576d:0} MB VRAM" : "";
        string extra = string.Join(", ", found.Where(f => f.name != main.name).Select(f => f.name));
        return (main.name, vram, extra, IsDedicatedGpu(main.name));
    }

    public static (string name, string build) Os()
    {
        const string p = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        string product = Reg(p, "ProductName");
        string build = Reg(p, "CurrentBuild");
        string ver = Reg(p, "DisplayVersion");
        if (string.IsNullOrEmpty(ver)) ver = Reg(p, "ReleaseId");

        if (int.TryParse(build, out int b) && b >= 22000)
            product = product.Replace("Windows 10", "Windows 11");
        if (string.IsNullOrEmpty(product)) product = "Windows";

        return (product, $"Build {build}" + (ver != "" ? $" · {ver}" : ""));
    }

    public static string Board()
    {
        const string p = @"HARDWARE\DESCRIPTION\System\BIOS";
        string m = Reg(p, "BaseBoardManufacturer");
        string n = Reg(p, "BaseBoardProduct");
        string s = $"{m} {n}".Trim();
        return s == "" ? "Unknown" : s;
    }

    // ---------- memory ----------
    public static (ulong total, ulong available, uint load) Memory()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        return GlobalMemoryStatusEx(ref m)
            ? (m.ullTotalPhys, m.ullAvailPhys, m.dwMemoryLoad)
            : (0, 0, 0);
    }

    // ---------- storage ----------
    public static (long total, long free) SystemDrive()
    {
        try
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var d = new DriveInfo(root);
            return (d.TotalSize, d.AvailableFreeSpace);
        }
        catch { return (0, 0); }
    }

    // ---------- cpu load ----------
    private static long oldIdle, oldKernel, oldUser;
    private static bool first = true;

    public static float CpuUsage()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        long i = ToLong(idle), k = ToLong(kernel), u = ToLong(user);

        if (first)
        {
            oldIdle = i; oldKernel = k; oldUser = u; first = false;
            return 0;
        }

        long idleDiff = i - oldIdle;
        long total = (k - oldKernel) + (u - oldUser);
        oldIdle = i; oldKernel = k; oldUser = u;
        if (total <= 0) return 0;
        return (float)Math.Clamp(100d * (total - idleDiff) / total, 0, 100);
    }

    private static long ToLong(FILETIME t) => ((long)t.dwHighDateTime << 32) | t.dwLowDateTime;

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        double mb = bytes / 1048576d;
        if (mb >= 1024) return $"{mb / 1024d:0.0} GB";
        if (mb < 1) return "<1 MB";
        return $"{mb:0} MB";
    }

    // ---------- native ----------
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile,
                     ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
}
