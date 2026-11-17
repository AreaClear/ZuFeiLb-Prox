namespace ZufeiBooster;

public sealed partial class MainForm
{
    private Label monEmuName = null!, monEmuCpu = null!, monEmuRam = null!,
                  monOthCpu = null!, monOthRam = null!,
                  monGpuName = null!, monGpuPct = null!, monGpuInfo = null!,
                  monFps = null!, monFrame = null!, monFpsStatus = null!,
                  monTop = null!, monBefore = null!;
    private ThinBar monEmuBar = null!, monOthBar = null!, monGpuBar = null!;
    private Sparkline monSpark = null!;
    private PillButton fpsButton = null!;
    private double? beforeOthers;

    private static readonly string[] FpsTargets =
        { "HD-Player", "dnplayer", "MEmu", "Nox", "aow_exe", "AndroidEmulator", "GameLoop" };

    private void BuildMonitor()
    {
        var p = NewPage(PgMonitor, "Performance monitor", "Emulator vs all other apps · real numbers, nothing simulated");

        // --- emulator
        var emu = C(p, 30, 70, 232, 116);
        L(emu, "Emulator CPU", 14, 8, 200, 16, 8, Theme.Muted);
        monEmuName = L(emu, "Not running", 14, 24, 204, 18, 8.5f, Theme.Text, true);
        monEmuCpu = L(emu, "0.0%", 14, 42, 204, 38, 21, Theme.AccentHi, true);
        monEmuBar = Bar(emu, 14, 86, 204);
        monEmuRam = L(emu, "", 14, 94, 204, 16, 7.5f, Theme.Muted);

        // --- everything else
        var oth = C(p, 274, 70, 232, 116);
        L(oth, "All other apps CPU", 14, 8, 200, 16, 8, Theme.Muted);
        L(oth, "Browsers and background programs", 14, 24, 204, 18, 8.5f, Theme.Muted);
        monOthCpu = L(oth, "0.0%", 14, 42, 204, 38, 21, Theme.Text, true);
        monOthBar = Bar(oth, 14, 86, 204);
        monOthRam = L(oth, "", 14, 94, 204, 16, 7.5f, Theme.Muted);

        // --- GPU
        var gpu = C(p, 518, 70, 232, 116);
        L(gpu, "GPU load", 14, 8, 200, 16, 8, Theme.Muted);
        monGpuName = L(gpu, "", 14, 24, 204, 18, 8.5f, Theme.Text, true);
        monGpuPct = L(gpu, "--", 14, 42, 204, 38, 21, Theme.Text, true);
        monGpuBar = Bar(gpu, 14, 86, 204);
        monGpuInfo = L(gpu, "", 14, 94, 204, 16, 7.5f, Theme.Muted);

        // --- FPS
        var f = C(p, 30, 196, 232, 108);
        f.Glow = true;
        L(f, "Real FPS (PresentMon)", 14, 8, 200, 16, 8, Theme.Muted);
        monFps = L(f, "--", 14, 24, 204, 40, 24, Theme.Good, true);
        monFrame = L(f, "", 14, 64, 204, 16, 8, Theme.Text);
        monFpsStatus = L(f, "", 14, 80, 206, 26, 7.5f, Theme.Muted);

        // --- history chart
        var chart = C(p, 274, 196, 476, 108);
        L(chart, "CPU history", 14, 8, 200, 16, 8.5f, Theme.Text, true);
        L(chart, "Emulator", 300, 8, 80, 16, 8, Theme.AccentHi, true, ContentAlignment.MiddleRight);
        L(chart, "Other apps", 384, 8, 80, 16, 8, Theme.Muted, true, ContentAlignment.MiddleRight);
        monSpark = new Sparkline(2)
        {
            Location = new Point(14, 28), Size = new Size(448, 70),
            Colors = new[] { Theme.AccentHi, Theme.Muted }
        };
        chart.Controls.Add(monSpark);

        monTop = L(p, "Heaviest other apps: --", 30, 312, 720, 20, 8.5f, Theme.Muted);
        monBefore = L(p, "Press Boost system, then compare the 'All other apps' number.", 30, 334, 720, 20, 8.5f, Theme.Text);
        L(p, "Protected Windows processes cannot be measured and are not counted. Real FPS needs PresentMon (the app can download it) and administrator rights.",
            30, 356, 720, 28, 7.5f, Theme.Muted);

        B(p, "Boost system", 30, 394, 136, 34, () => Safe(BoostSystemAsync), primary: true);
        B(p, "GPU Boost", 174, 394, 124, 34, () => Safe(GpuBoostAsync), primary: true);
        B(p, "Free RAM", 306, 394, 112, 34, () => Safe(FreeRamAsync));
        B(p, "Undo boost", 426, 394, 112, 34, RestoreBoost);
        fpsButton = B(p, fps.Running ? "Stop FPS" : "Start FPS", 546, 394, 110, 34, () => Safe(ToggleFpsAsync));
        B(p, "Admin", 664, 394, 86, 34, () => Safe(AdminAsync));
	B(p, "Admin", 664, 394, 86, 34, () => Safe(AdminAsync));

        BuildFakeFps(p);          // <-- add
    }

    private void UpdateMonitor()
    {
        var r = procMonitor.Take();

        monEmuName.Text = r.EmuCount > 0 ? r.EmuNames : "Not running - start your emulator";
        monEmuCpu.Text = $"{r.EmuCpu:0.0}%";
        monEmuBar.Value = (int)Math.Min(100, Math.Round(r.EmuCpu));
        monEmuRam.Text = $"{r.EmuRamMb:0} MB RAM · {r.EmuCount} process(es)";

        monOthCpu.Text = $"{r.OtherCpu:0.0}%";
        monOthBar.Value = (int)Math.Min(100, Math.Round(r.OtherCpu));
        monOthRam.Text = $"{r.OtherRamMb:0} MB RAM";

        monTop.Text = r.Top.Count == 0
            ? "Heaviest other apps: --"
            : "Heaviest other apps: " + string.Join("  ·  ", r.Top.Select(t => $"{t.name} {t.cpu:0.0}%"));

        monBefore.Text = beforeOthers is double b
            ? $"Other apps CPU at last boost: {b:0.0}%    ·    now: {r.OtherCpu:0.0}%"
            : "Press Boost system, then compare the 'All other apps' number.";

        monSpark.Add(0, r.EmuCpu);
        monSpark.Add(1, r.OtherCpu);
        monSpark.Redraw();

        // FPS
        if (fps.Running)
        {
            var (f, ms) = fps.Current();
            monFps.Text = f > 0 ? $"{f:0} FPS" : "...";
            monFrame.Text = f > 0 ? $"{ms:0.0} ms per frame" : "waiting for frames";
            monFpsStatus.Text = f == 0 && (DateTime.UtcNow - fps.StartedAt).TotalSeconds > 6
                ? "No frames yet. Open the game and keep the emulator window visible (not minimized)."
                : fps.Status;
        }
        else
        {
            monFps.Text = "--";
            monFrame.Text = "";
            monFpsStatus.Text = FpsMonitor.FindExe() == null
                ? "Press Start FPS - the app can download PresentMon for you."
                : "Start your emulator, then press Start FPS.";
            fpsButton.Text = "Start FPS";
			UpdateFakeFpsLabels();    // <-- add
        }

        // GPU (nvidia-smi, off the UI thread)
        monGpuName.Text = gpuName == "" ? "GPU" : gpuName;
        if (!gpuBusy)
        {
            gpuBusy = true;
            Task.Run(GpuStats.Query).ContinueWith(t =>
            {
                try
                {
                    var res = t.Result;
                    BeginInvoke(new Action(() => { gpuBusy = false; ShowGpu(res); }));
                }
                catch { gpuBusy = false; }
            });
        }
    }

    private void ShowGpu((bool ok, int util, int temp, int usedMb, int totalMb) g)
    {
        try
        {
            if (g.ok)
            {
                monGpuPct.Text = $"{g.util}%";
                monGpuBar.Value = g.util;
                monGpuInfo.Text = $"{g.temp}°C · {g.usedMb}/{g.totalMb} MB VRAM";
            }
            else
            {
                monGpuPct.Text = "n/a";
                monGpuBar.Value = 0;
                monGpuInfo.Text = "Live GPU load needs the NVIDIA driver (nvidia-smi).";
            }
        }
        catch { }
    }

    private async Task FreeRamAsync()
    {
        var r = await Task.Run(RamCleaner.Trim);
        var top = await Task.Run(() => RamCleaner.TopRam(5));
        settings.AddLog("RAM cleaner: " + RamCleaner.Describe(r));
        settings.Save();

        string hogs = string.Join("\n", top.Select(t => $"   {t.name}  -  {t.mb:0} MB"));
        string admin = r.Flushed || AdminHelper.IsAdmin()
            ? ""
            : "\n\nTip: run as administrator (Monitor > Admin) to also flush Windows' memory lists.";

        MessageBox.Show(
            RamCleaner.Describe(r) +
            "\n\nThe emulator and critical Windows processes were not touched." +
            "\n\nBiggest RAM users right now:\n" + hogs +
            "\n\nIf RAM stays near 99%, close the apps at the top of this list (or lower the emulator's RAM setting)." + admin,
            AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private Task GpuBoostAsync()
    {
        var steps = engine.BuildGpuSteps(dedicatedGpu, gpuName, gpuExtra);

        using (var dlg = new BoostDialog("GPU Boost", steps))
            dlg.ShowDialog(this);

        settings.AddLog($"GPU Boost run ({(dedicatedGpu ? "dedicated" : "integrated")} GPU: {gpuName})");
        settings.Save();

        MessageBox.Show(BoostEngine.GpuGuide(dedicatedGpu), "GPU Boost - what to do next",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        return Task.CompletedTask;
    }

    private Task AdminAsync()
    {
        if (AdminHelper.IsAdmin())
        {
            MessageBox.Show("Zufei Booster is already running as administrator.", AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }

        var ans = MessageBox.Show(
            "Administrator mode lets the app measure real FPS, flush Windows memory lists and change startup items for all users.\n\nRestart as administrator now?",
            AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (ans == DialogResult.Yes && AdminHelper.RestartAsAdmin()) Close();
        return Task.CompletedTask;
    }

    private async Task ToggleFpsAsync()
    {
        if (fps.Running)
        {
            fps.Stop();
            fpsButton.Text = "Start FPS";
            return;
        }

        // 1) PresentMon present?
        if (FpsMonitor.FindExe() == null)
        {
            var ask = MessageBox.Show(
                "Real FPS needs PresentMon, a free tool from Intel (official GitHub release, about 1-2 MB).\n\nDownload it now?",
                AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ask != DialogResult.Yes) return;

            fpsButton.Enabled = false;
            string? path = await FpsMonitor.DownloadAsync(s => monFpsStatus.Text = s);
            fpsButton.Enabled = true;

            if (path == null)
            {
                MessageBox.Show(
                    "Could not download PresentMon.\nYou can download it yourself from github.com/GameTechDev/PresentMon/releases " +
                    "and put PresentMon*.exe next to ZufeiBooster.exe.",
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }

        // 2) administrator?
        if (!AdminHelper.IsAdmin())
        {
            var ask = MessageBox.Show(
                "Measuring FPS needs administrator rights (Windows rule for frame tracing).\n\nRestart Zufei Booster as administrator now?",
                AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ask == DialogResult.Yes && AdminHelper.RestartAsAdmin()) Close();
            return;
        }

        // 3) emulator running?
        string? exe = null;
        foreach (string name in FpsTargets)
        {
            var list = System.Diagnostics.Process.GetProcessesByName(name);
            bool any = list.Length > 0;
            foreach (var p in list) p.Dispose();
            if (any) { exe = name + ".exe"; break; }
        }

        if (exe == null)
        {
            MessageBox.Show("No emulator is running. Start your emulator and the game first, then press Start FPS.",
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        fpsButton.Enabled = false;
        monFpsStatus.Text = "Starting PresentMon...";
        bool ok = await fps.StartAsync(exe);
        fpsButton.Enabled = true;
        fpsButton.Text = ok ? "Stop FPS" : "Start FPS";

        if (!ok)
            MessageBox.Show(fps.Status, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
