using System.Diagnostics;

namespace ZufeiBooster;

internal sealed partial class BoostEngine
{
    /// <summary>Emulator programs found on this PC (running now, or in the usual install folders on every drive).</summary>
    public static List<string> FindEmulatorExes()
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string name in EmulatorNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                using (p)
                {
                    try { var f = p.MainModule?.FileName; if (!string.IsNullOrEmpty(f)) found.Add(f); } catch { }
                }
            }
        }

        var roots = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                string r = d.RootDirectory.FullName;
                roots.Add(r);
                roots.Add(Path.Combine(r, "Program Files"));
                roots.Add(Path.Combine(r, "Program Files (x86)"));
            }
        }
        catch { }

        string[] relative =
        {
            @"BlueStacks_nxt\HD-Player.exe", @"BlueStacks\HD-Player.exe", @"BlueStacks_msi5\HD-Player.exe",
            @"BlueStacks_nxt_cn\HD-Player.exe",
            @"LDPlayer\LDPlayer9\dnplayer.exe", @"LDPlayer\LDPlayer4.0\dnplayer.exe",
            @"LDPlayer9\dnplayer.exe", @"LDPlayer4.0\dnplayer.exe",
            @"LDPlayer\LDPlayer9\Ld9BoxHeadless.exe", @"LDPlayer\LDPlayer9\LdVBoxHeadless.exe",
            @"Microvirt\MEmu\MEmu.exe", @"Microvirt\MEmuHyperv\MEmuHeadless.exe",
            @"Nox\bin\Nox.exe", @"Nox\bin\NoxVMHandle.exe",
            @"TxGameAssistant\ui\AndroidEmulator.exe", @"TxGameAssistant\AppMarket\AppMarket.exe"
        };

        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string rel in relative)
            {
                try
                {
                    string p = Path.Combine(root, rel);
                    if (File.Exists(p)) found.Add(p);
                }
                catch { }
            }
        }

        return found.ToList();
    }

    public static string GpuGuide(bool dedicated)
    {
        string common =
            "Then FULLY close the emulator and open it again - Windows applies the GPU choice only when a program starts.\n\n";

        if (dedicated)
            return
                "GPU Boost applied. To make the emulator really use your graphics card (do this once):\n\n" +
                "BlueStacks 5: Settings > Graphics: Graphics engine mode = Performance, Graphics renderer = DirectX " +
                "(or Vulkan), turn ON 'Prefer dedicated graphics'. Settings > Performance: CPU cores 4, Performance mode = " +
                "High performance, Frame rate 60/120 and turn ON 'High frame rate'.\n\n" +
                "LDPlayer / MEmu / Nox: Settings > Advanced / Engine / Performance: set the graphics (render) mode to " +
                "DirectX (or the faster option), CPU 4 cores, FPS 60 or more.\n\n" +
                common +
                "Note: GPU usage of 8-20% is normal for a mobile game. It means the GPU is not the limit - the emulator's " +
                "renderer and CPU are. Forcing the GPU to 100% would not give more FPS; the settings above do.";

        return
            "GPU Boost applied (integrated GPU).\n\n" +
            "An onboard GPU shares system RAM, so the best FPS gain comes from lighter settings:\n" +
            "- Emulator resolution 1280x720, DPI 160, graphics renderer DirectX.\n" +
            "- Emulator FPS 30-60, CPU 4 cores, RAM 3-4 GB.\n" +
            "- Close browsers and other apps (use Boost system + Free RAM).\n\n" +
            common;
    }

    public List<BoostStep> BuildGpuSteps(bool dedicated, string gpuName, string gpuExtra)
    {
        var paths = new List<string>();

        return new List<BoostStep>
        {
            new BoostStep { Title = "Detecting your GPU", Run = () =>
                (true, dedicated
                    ? $"{gpuName} (dedicated)" + (gpuExtra != "" ? $", also {gpuExtra}" : "")
                    : $"{gpuName} (integrated)") },

            new BoostStep { Title = "Finding installed emulators", Run = () =>
            {
                paths.AddRange(FindEmulatorExes());
                return (true, paths.Count > 0
                    ? $"{paths.Count} emulator program(s) found"
                    : "none found yet - open your emulator once, then run GPU Boost again");
            } },

            new BoostStep { Title = "Windows GPU preference", Run = () =>
            {
                int n = 0;
                foreach (string p in paths)
                {
                    try
                    {
                        SetString(@"Software\Microsoft\DirectX\UserGpuPreferences", p, "GpuPreference=2;");
                        s.AddLog($"GPU preference: {Path.GetFileName(p)} -> High performance GPU");
                        n++;
                    }
                    catch { }
                }
                s.Save();
                return (true, n > 0
                    ? $"{n} program(s) set to High performance GPU (restart the emulator to apply)"
                    : "nothing to set");
            } },

            new BoostStep { Title = "High performance power plan", Run = SetPowerPlan },

            new BoostStep { Title = "Where does the emulator render?", Run = () =>
            {
                var r = GpuUsage.CheckEmulator(dedicated);
                s.AddLog("GPU check: " + r.Verdict);
                return (true, r.Verdict);
            } },

            new BoostStep { Title = "GPU clock speed", Run = () =>
            {
                var c = GpuStats.Clocks();
                if (!c.ok) return (true, "not available (needs the NVIDIA driver)");
                string hint = c.maxMhz > 0 && c.curMhz < c.maxMhz * 0.5
                    ? " - the card is resting because the emulator asks little of it; use the DirectX/Vulkan renderer and a higher FPS cap"
                    : "";
                return (true, $"{c.pstate}, {c.curMhz}/{c.maxMhz} MHz{hint}");
            } },

            new BoostStep { Title = "Verifying", Run = () =>
            {
                int ok = 0;
                foreach (string p in paths)
                {
                    string? v = Microsoft.Win32.Registry.GetValue(
                        @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences", p, null) as string;
                    if (v != null && v.Contains("GpuPreference=2")) ok++;
                }
                return (true, $"{ok} of {paths.Count} GPU preference(s) confirmed");
            } }
        };
    }
}
