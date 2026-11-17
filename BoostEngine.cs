using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ZufeiBooster;

internal sealed class BoostStep
{
    public string Title = "";
    public Func<(bool ok, string detail)> Run = () => (true, "");
}

/// <summary>
/// Real, reversible tweaks. Nothing here kills processes, disables Defender/Firewall,
/// or shows fake numbers. Everything is undone by RestoreAll() (also on app close).
/// </summary>
internal sealed partial class BoostEngine
{
    private const string HighPerfPlan = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    private readonly AppSettings s;
    private readonly Dictionary<int, ProcessPriorityClass> saved = new();

    public BoostEngine(AppSettings settings) => s = settings;

    // Emulator main + VM worker processes (these do the heavy work)
    internal static readonly string[] EmulatorNames =
    {
        "HD-Player", "dnplayer", "LdVBoxHeadless", "Ld9BoxHeadless",
        "MEmu", "MEmuHeadless", "Nox", "NoxVMHandle",
        "AndroidEmulator", "aow_exe", "GameLoop", "TxGameAssistant"
    };

    private static readonly string[] BackgroundBasic =
    {
        "OneDrive", "Teams", "ms-teams", "Spotify", "Dropbox", "GoogleDriveFS",
        "Skype", "YourPhone", "PhoneExperienceHost"
    };

    private static readonly string[] BackgroundLowEnd =
    {
        "chrome", "msedge", "firefox", "brave", "opera",
        "Discord", "steamwebhelper", "WhatsApp", "Telegram"
    };

    public List<BoostStep> BuildSteps(string profile, bool dedicatedGpu)
    {
        bool lowEnd = profile is "Ultra Low" or "Low";

        return new List<BoostStep>
        {
            new BoostStep { Title = "Checking hardware profile",
                Run = () => (true, $"profile: {profile}" + (lowEnd ? " (extra background trimming on)" : "")) },

            new BoostStep { Title = "High performance power plan", Run = SetPowerPlan },

            new BoostStep { Title = "Windows Game Mode", Run = () =>
            {
                SetDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
                SetDword(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
                return (true, "turned on");
            } },

            new BoostStep { Title = "Pause background game recording", Run = () =>
            {
                SetDword(@"System\GameConfigStore", "GameDVR_Enabled", 0);
                SetDword(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
                return (true, "Game DVR off");
            } },

            new BoostStep { Title = "Emulator priority and GPU", Run = () => BoostEmulators(dedicatedGpu) },

            new BoostStep { Title = "Lower background apps", Run = () => LowerBackground(lowEnd) },

            new BoostStep { Title = "Free RAM from background apps", Run = () =>
            {
                var r = RamCleaner.Trim();
                s.AddLog("RAM: " + RamCleaner.Describe(r));
                return (true, RamCleaner.Describe(r));
            } },

            new BoostStep { Title = "Verifying changes", Run = Verify }
        };
    }

    // ---------------- steps ----------------

    private (bool ok, string detail) SetPowerPlan()
    {
        var (current, currentName) = ActivePlan();
        if (current == HighPerfPlan) return (true, "already High performance");

        if (current != null && s.PowerPlanBackup == null)
        {
            s.PowerPlanBackup = current;
            s.Save();
        }

        var r = Exec("powercfg", $"/setactive {HighPerfPlan}");
        if (r.code != 0) return (false, "High performance plan not available on this PC");

        s.AddLog($"Power plan: {(currentName == "" ? "unknown" : currentName)} -> High performance");
        s.Save();
        return (true, "switched to High performance");
    }

    private static (string? guid, string name) ActivePlan()
    {
        var r = Exec("powercfg", "/getactivescheme");
        var m = Regex.Match(r.output, @"[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}");
        var n = Regex.Match(r.output, @"\(([^)]+)\)");
        return (m.Success ? m.Value.ToLowerInvariant() : null, n.Success ? n.Groups[1].Value : "");
    }

    public static string CurrentPlanName()
    {
        var (_, name) = ActivePlan();
        return name == "" ? "unknown" : name;
    }

    public static bool AnyEmulatorRunning()
    {
        foreach (string name in EmulatorNames)
        {
            try
            {
                var list = Process.GetProcessesByName(name);
                bool any = list.Length > 0;
                foreach (var p in list) p.Dispose();
                if (any) return true;
            }
            catch { }
        }
        return false;
    }

    private (bool ok, string detail) BoostEmulators(bool dedicatedGpu)
    {
        int count = 0, gpu = 0;

        foreach (string name in EmulatorNames)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                using (proc)
                {
                    try
                    {
                        var before = proc.PriorityClass;
                        if (!saved.ContainsKey(proc.Id)) saved[proc.Id] = before;
                        proc.PriorityClass = ProcessPriorityClass.High;
                        count++;
                        s.AddLog($"CPU priority: {proc.ProcessName} (PID {proc.Id}) {before} -> High");

                        if (dedicatedGpu)
                        {
                            string? path = null;
                            try { path = proc.MainModule?.FileName; } catch { }
                            if (!string.IsNullOrEmpty(path))
                            {
                                SetString(@"Software\Microsoft\DirectX\UserGpuPreferences", path, "GpuPreference=2;");
                                gpu++;
                                s.AddLog($"GPU preference: {Path.GetFileName(path)} -> High performance GPU");
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        if (count == 0)
            return (true, "no emulator running - open it, then boost again");

        return (true, $"{count} process(es) set to High" + (gpu > 0 ? ", high-performance GPU chosen" : ""));
    }

    private (bool ok, string detail) LowerBackground(bool lowEnd)
    {
        int count = 0;
        var perName = new Dictionary<string, int>();
        var names = lowEnd ? BackgroundBasic.Concat(BackgroundLowEnd) : BackgroundBasic;

        foreach (string name in names)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                using (proc)
                {
                    try
                    {
                        var pc = proc.PriorityClass;
                        if (pc is ProcessPriorityClass.Normal or ProcessPriorityClass.AboveNormal)
                        {
                            if (!saved.ContainsKey(proc.Id)) saved[proc.Id] = pc;
                            proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                            count++;
                            perName[name] = perName.GetValueOrDefault(name) + 1;
                        }
                    }
                    catch { }
                }
            }
        }

        foreach (var kv in perName)
            s.AddLog($"CPU priority lowered: {kv.Key} x{kv.Value} (Normal -> BelowNormal)");

        return (true, count > 0 ? $"{count} background process(es) set to Below normal" : "nothing to lower");
    }

    private (bool ok, string detail) Verify()
    {
        int changed = 0;
        foreach (var kv in saved)
        {
            try
            {
                using var p = Process.GetProcessById(kv.Key);
                if (p.PriorityClass != kv.Value) changed++;
            }
            catch { }
        }

        string plan = ActivePlan().guid == HighPerfPlan ? "High performance" : "unchanged";
        return (true, $"power plan: {plan}, {changed} process priority change(s) confirmed");
    }

    // ---------------- restore ----------------

    public void RestoreAll()
    {
        int nPrio = saved.Count, nReg = s.RegBackup.Count;
        bool hadPlan = s.PowerPlanBackup != null;

        foreach (var kv in saved.ToArray())
        {
            try
            {
                using var p = Process.GetProcessById(kv.Key);
                p.PriorityClass = kv.Value;
            }
            catch { }
        }
        saved.Clear();

        foreach (var kv in s.RegBackup.ToArray())
        {
            try
            {
                int idx = kv.Key.LastIndexOf('|');
                if (idx < 0) continue;
                string path = kv.Key[..idx], name = kv.Key[(idx + 1)..];

                using var key = Registry.CurrentUser.OpenSubKey(path, true);
                if (key == null) continue;

                if (kv.Value == null) key.DeleteValue(name, false);
                else if (kv.Value.StartsWith("D:") && int.TryParse(kv.Value[2..], out int d))
                    key.SetValue(name, d, RegistryValueKind.DWord);
                else if (kv.Value.StartsWith("S:"))
                    key.SetValue(name, kv.Value[2..], RegistryValueKind.String);
            }
            catch { }
        }
        s.RegBackup.Clear();

        if (s.PowerPlanBackup != null)
        {
            Exec("powercfg", $"/setactive {s.PowerPlanBackup}");
            s.PowerPlanBackup = null;
        }

        s.AddLog($"Undo boost: restored {nPrio} process priority(ies), {nReg} registry value(s)" +
                 (hadPlan ? ", power plan" : ""));
        s.Save();
    }

    // ---------------- helpers ----------------

    private void Backup(RegistryKey key, string path, string name)
    {
        string id = $"{path}|{name}";
        if (s.RegBackup.ContainsKey(id)) return;

        object? old = key.GetValue(name);
        s.RegBackup[id] = old switch
        {
            null => null,
            int i => $"D:{i}",
            _ => $"S:{old}"
        };
        s.Save();
    }

    private void SetDword(string path, string name, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path, true);
        Backup(key, path, name);
        object? old = key.GetValue(name);
        key.SetValue(name, value, RegistryValueKind.DWord);
        if (old == null || Convert.ToString(old) != value.ToString())
            s.AddLog($"Registry HKCU\\{path}\\{name}: {old ?? "not set"} -> {value}");
    }

    private void SetString(string path, string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path, true);
        Backup(key, path, name);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    private static (int code, string output) Exec(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return (p.ExitCode, o);
        }
        catch { return (-1, ""); }
    }
}
