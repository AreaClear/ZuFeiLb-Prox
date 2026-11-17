using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZufeiBooster;

internal sealed record TrimResult(int Count, long Before, long After, bool Flushed, bool Aggressive);

/// <summary>
/// Gives RAM back from apps that are not the emulator. Nothing is killed.
/// - Normal: trims ordinary background apps.
/// - Aggressive (used automatically when RAM is 85%+ full): also trims services/Explorer/Defender.
/// - With administrator rights: also flushes Windows' modified + low-priority standby memory lists.
/// Trimmed pages are loaded back by Windows only if an app really needs them.
/// </summary>
internal static class RamCleaner
{
    [DllImport("psapi.dll")] private static extern bool EmptyWorkingSet(IntPtr process);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ntdll.dll")] private static extern uint NtSetSystemInformation(int infoClass, ref int info, int length);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool LookupPrivilegeValue(string? system, string name, out long luid);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES np, int len, IntPtr prev, IntPtr ret);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct TOKEN_PRIVILEGES { public int Count; public long Luid; public int Attributes; }

    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_SET_QUOTA = 0x0100;

    // never touched, in any mode
    private static readonly string[] Critical =
    {
        "Idle", "System", "Registry", "Memory Compression", "smss", "csrss", "wininit",
        "lsass", "winlogon", "dwm", "audiodg", "fontdrvhost", "ZufeiBooster", "taskmgr"
    };

    // only left alone in normal mode
    private static readonly string[] Careful =
    {
        "services", "svchost", "explorer", "MsMpEng", "sihost", "ctfmon", "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost"
    };

    private static readonly string[] EmulatorPrefixes = { "HD-", "Ld", "MEmu", "Nox", "Bluestacks", "dnplayer", "aow_", "GameLoop", "TxGame", "AndroidEmulator" };

    public static TrimResult Trim()
    {
        var emu = new HashSet<string>(BoostEngine.EmulatorNames, StringComparer.OrdinalIgnoreCase);
        var mem0 = SysInfo.Memory();
        bool aggressive = mem0.load >= 85;
        long before = (long)mem0.available;
        int count = 0;
        int self = Environment.ProcessId;

        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    string name = p.ProcessName;
                    if (p.Id == self || p.Id <= 4) continue;
                    if (Critical.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                    if (!aggressive && Careful.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                    if (emu.Contains(name)) continue;
                    if (EmulatorPrefixes.Any(x => name.StartsWith(x, StringComparison.OrdinalIgnoreCase))) continue;

                    IntPtr h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_SET_QUOTA, false, p.Id);
                    if (h == IntPtr.Zero) continue;
                    try { if (EmptyWorkingSet(h)) count++; }
                    finally { CloseHandle(h); }
                }
                catch { }
            }
        }

        bool flushed = FlushSystemLists();

        Thread.Sleep(700);
        long after = (long)SysInfo.Memory().available;
        return new TrimResult(count, before, after, flushed, aggressive);
    }

    private static bool EnablePrivilege(string name)
    {
        IntPtr token = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x28, out token)) return false;
            if (!LookupPrivilegeValue(null, name, out long luid)) return false;
            var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attributes = 2 };
            AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            return Marshal.GetLastWin32Error() == 0;
        }
        catch { return false; }
        finally { if (token != IntPtr.Zero) CloseHandle(token); }
    }

    /// <summary>Flush modified pages (3) and low-priority standby pages (5). Needs administrator.</summary>
    private static bool FlushSystemLists()
    {
        try
        {
            if (!AdminHelper.IsAdmin()) return false;
            if (!EnablePrivilege("SeProfileSingleProcessPrivilege")) return false;

            int cmd = 3;
            uint r1 = NtSetSystemInformation(80, ref cmd, sizeof(int));
            cmd = 5;
            uint r2 = NtSetSystemInformation(80, ref cmd, sizeof(int));
            return r1 == 0 || r2 == 0;
        }
        catch { return false; }
    }

    public static List<(string name, double mb)> TopRam(int n)
    {
        var map = new Dictionary<string, double>();
        int self = Environment.ProcessId;

        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    if (p.Id == self || p.Id <= 4) continue;
                    map[p.ProcessName] = map.GetValueOrDefault(p.ProcessName) + p.WorkingSet64 / 1048576d;
                }
                catch { }
            }
        }

        return map.OrderByDescending(k => k.Value).Take(n).Select(k => (k.Key, k.Value)).ToList();
    }

    public static string Describe(TrimResult r) =>
        $"{r.Count} app(s) trimmed{(r.Aggressive ? " (deep mode, RAM was 85%+ full)" : "")}" +
        $"{(r.Flushed ? ", Windows memory lists flushed" : "")}; " +
        $"available RAM {r.Before / 1073741824d:0.00} GB -> {r.After / 1073741824d:0.00} GB";
}
