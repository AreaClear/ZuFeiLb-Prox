namespace ZufeiBooster;

public sealed partial class MainForm
{
    // ---- fields built by BuildHome / BuildHardware ----
    private ProgressRing homeCpuRing = null!, homeRamRing = null!;
    private Label homeRamText = null!;
    private Sparkline homeSpark = null!;

    private Label hwCpuLoad = null!, hwRamUse = null!, hwStorageFree = null!, lastScan = null!;
    private ThinBar hwCpuBar = null!, hwRamBar = null!, hwStorageBar = null!;
    private Label hwGpuName = null!, hwGpuVram = null!, hwCpuName = null!, hwCpuCores = null!,
                  hwRamTotal = null!, hwStorage = null!, hwOs = null!, hwOsBuild = null!, hwBoard = null!;

    // =====================================================================
    //  HOME
    // =====================================================================

    private void BuildHome()
    {
        var p = NewPage(PgHome, AppName, "Free performance toolkit for Windows");

        var left = C(p, 30, 76, 300, 290);
        left.Glow = true;
        left.Controls.Add(new RingIcon { Location = new Point(95, 20) });
        L(left, "Welcome back", 0, 148, 300, 26, 12, Theme.Text, true, ContentAlignment.MiddleCenter);
        L(left, "Your system is ready to boost.", 0, 176, 300, 20, 8.5f, Theme.Muted, false, ContentAlignment.MiddleCenter);
        B(left, "Open Booster", 24, 214, 252, 40, () => Navigate(PgHardware), primary: true);

        var cpuCard = C(p, 350, 76, 195, 142);
        homeCpuRing = new ProgressRing { Location = new Point(48, 8), Size = new Size(100, 100) };
        cpuCard.Controls.Add(homeCpuRing);
        L(cpuCard, "CPU load", 0, 114, 195, 20, 8.5f, Theme.Muted, false, ContentAlignment.MiddleCenter);

        var ramCard = C(p, 555, 76, 195, 142);
        homeRamRing = new ProgressRing { Location = new Point(48, 8), Size = new Size(100, 100) };
        ramCard.Controls.Add(homeRamRing);
        homeRamText = L(ramCard, "RAM", 0, 114, 195, 20, 8.5f, Theme.Muted, false, ContentAlignment.MiddleCenter);

        var hist = C(p, 350, 228, 400, 84);
        L(hist, "Usage history", 14, 6, 200, 16, 8.5f, Theme.Text, true);
        L(hist, "CPU", 290, 6, 46, 16, 8, Theme.AccentHi, true, ContentAlignment.MiddleRight);
        L(hist, "RAM", 340, 6, 46, 16, 8, Theme.Muted, true, ContentAlignment.MiddleRight);
        homeSpark = new Sparkline(2)
        {
            Location = new Point(14, 26), Size = new Size(372, 50),
            Colors = new[] { Theme.AccentHi, Theme.Muted }
        };
        hist.Controls.Add(homeSpark);

        var free = C(p, 350, 322, 400, 44);
        free.Stroke = Theme.Good;
        free.Fill = Color.FromArgb(22, 38, 46);
        L(free, "Free forever · Not for sale", 14, 4, 372, 18, 9, Theme.Good, true);
        L(free, "If you paid for this, ask for a refund.", 14, 23, 372, 16, 7.5f, Theme.Muted);

        L(p, $"</>  Built by {Creator} · free to share", 30, 386, 400, 20, 8, Theme.Muted);
        L(p, Version, 650, 386, 100, 20, 8, Theme.Muted, false, ContentAlignment.MiddleRight);
    }

    // =====================================================================
    //  HARDWARE
    // =====================================================================

    private void BuildHardware()
    {
        var p = NewPage(PgHardware, "Your hardware at a glance", "Processor, graphics, memory, storage and system");

        var gpu = C(p, 30, 76, 200, 300);
        var ring = new RingIcon { Location = new Point(45, 24), Kind = "hardware" };
        gpu.Controls.Add(ring);
        L(gpu, "GPU", 16, 190, 170, 16, 8, Theme.Muted);
        hwGpuName = L(gpu, "--", 16, 208, 172, 44, 9.5f, Theme.AccentHi, true);
        hwGpuVram = L(gpu, "", 16, 262, 172, 34, 8, Theme.Muted);

        var cpu = C(p, 250, 76, 500, 104);
        L(cpu, "CPU", 16, 10, 100, 16, 8, Theme.Muted);
        L(cpu, "Load", 380, 10, 104, 16, 8, Theme.Muted, false, ContentAlignment.MiddleRight);
        hwCpuName = L(cpu, "--", 16, 30, 468, 22, 10.5f, Theme.Text, true);
        hwCpuCores = L(cpu, "", 16, 58, 300, 16, 8, Theme.Muted);
        hwCpuLoad = L(cpu, "0%", 380, 28, 104, 22, 10, Theme.AccentHi, true, ContentAlignment.MiddleRight);
        hwCpuBar = Bar(cpu, 16, 84, 468);

        var ram = C(p, 250, 190, 245, 90);
        L(ram, "RAM", 16, 8, 100, 16, 8, Theme.Muted);
        hwRamTotal = L(ram, "--", 16, 26, 210, 22, 11, Theme.Text, true);
        hwRamBar = Bar(ram, 16, 56, 213);
        hwRamUse = L(ram, "", 16, 66, 213, 16, 8, Theme.Muted);

        var sto = C(p, 505, 190, 245, 90);
        L(sto, "Storage (C:)", 16, 8, 150, 16, 8, Theme.Muted);
        hwStorage = L(sto, "--", 16, 26, 210, 22, 11, Theme.Text, true);
        hwStorageBar = Bar(sto, 16, 56, 213);
        hwStorageFree = L(sto, "", 16, 66, 213, 16, 8, Theme.Muted);

        var os = C(p, 250, 290, 245, 86);
        L(os, "OS", 16, 8, 100, 16, 8, Theme.Muted);
        hwOs = L(os, "--", 16, 28, 215, 22, 11, Theme.Text, true);
        hwOsBuild = L(os, "", 16, 56, 215, 16, 8, Theme.Muted);

        var mb = C(p, 505, 290, 245, 86);
        L(mb, "Motherboard", 16, 8, 150, 16, 8, Theme.Muted);
        hwBoard = L(mb, "--", 16, 28, 215, 44, 9.5f, Theme.Text, true);

        lastScan = L(p, "Last scan --", 30, 396, 190, 24, 8, Theme.Muted, false, ContentAlignment.MiddleLeft);
        B(p, "Refresh", 226, 392, 88, 32, () => { LoadHardware(); UpdateLive(); });
        B(p, "Report", 320, 392, 84, 32, () => Navigate(PgReport));
        B(p, "GPU Boost", 410, 392, 112, 32, () => Safe(GpuBoostAsync), primary: true);
        B(p, "Boost system", 528, 392, 120, 32, () => Safe(BoostSystemAsync), primary: true);
        B(p, "Clean up", 654, 392, 96, 32, () => Navigate(PgCleanup), danger: true);
    }

    private void LoadHardware()
    {
        cpuName = SysInfo.Cpu();
        (gpuName, gpuVram, gpuExtra, dedicatedGpu) = SysInfo.Gpu();
        (osName, osBuild) = SysInfo.Os();
        board = SysInfo.Board();

        var mem = SysInfo.Memory();
        var drive = SysInfo.SystemDrive();

        hwGpuName.Text = gpuName;
        hwGpuVram.Text = gpuVram + (gpuExtra != "" ? $"\n+ {gpuExtra}" : "");
        hwCpuName.Text = cpuName;
        hwCpuCores.Text = $"{Environment.ProcessorCount} logical processors";
        hwRamTotal.Text = $"{mem.total / 1073741824d:0.0} GB";
        hwStorage.Text = $"{(drive.total - drive.free) / 1073741824d:0.0} GB / {drive.total / 1073741824d:0.0} GB";
        hwOs.Text = osName;
        hwOsBuild.Text = osBuild;
        hwBoard.Text = board;
        lastScan.Text = $"Last scan {DateTime.Now:HH:mm}";
    }


    // ---- Boost system: opens the progress panel, runs real reversible tweaks ----

    private string HardwareProfile()
    {
        var mem = SysInfo.Memory();
        double gb = mem.total / 1073741824d;
        int threads = Environment.ProcessorCount;

        if (gb <= 4 || threads <= 2) return "Ultra Low";
        if (gb <= 8 && !dedicatedGpu) return "Low";
        if (gb <= 8 || threads < 6) return "Balanced";
        return "Gaming";
    }

    private async Task BoostSystemAsync()
    {
        // measure "other apps" CPU for ~1 second before and after, so the comparison is real
        var probe = new ProcessMonitor();
        beforeOthers = await Task.Run(() =>
        {
            probe.Take();
            Thread.Sleep(1000);
            return probe.Take().OtherCpu;
        });

        settings.AddLog($"Boost started (profile {HardwareProfile()}), other apps CPU {beforeOthers:0.0}%");

        using (var dlg = new BoostDialog("Boost system", engine.BuildSteps(HardwareProfile(), dedicatedGpu)))
            dlg.ShowDialog(this);

        var probe2 = new ProcessMonitor();
        double after = await Task.Run(() =>
        {
            probe2.Take();
            Thread.Sleep(1500);
            return probe2.Take().OtherCpu;
        });

        settings.AddLog($"Boost finished: other apps CPU {beforeOthers:0.0}% -> {after:0.0}%");
        settings.Save();
    }

    private void RestoreBoost()
    {
        engine.RestoreAll();
        MessageBox.Show("Everything was put back to how it was (power plan, Game Mode, recording and process priorities).",
            AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

}
