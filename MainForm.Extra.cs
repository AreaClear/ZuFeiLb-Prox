using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace ZufeiBooster;

public sealed partial class MainForm
{
    // =====================================================================
    //  REPORT  (what the PC looks like + everything this app changed)
    // =====================================================================

    private TextBox reportBox = null!;

    private void BuildReport()
    {
        var p = NewPage(PgReport, "Advanced report", "Your PC, and everything this app changed on it");

        var card = C(p, 30, 70, 720, 316);
        reportBox = new TextBox
        {
            Multiline = true, ReadOnly = true, WordWrap = true,
            ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(Math.Min(255, Theme.Surface.R + 6), Math.Min(255, Theme.Surface.G + 6), Math.Min(255, Theme.Surface.B + 6)),
            ForeColor = Theme.Text, Font = new Font("Consolas", 8.5f),
            Location = new Point(12, 12), Size = new Size(696, 292)
        };
        card.Controls.Add(reportBox);

        B(p, "Refresh", 30, 396, 120, 34, RefreshReport);
        B(p, "Copy", 160, 396, 120, 34, () =>
        {
            Clipboard.SetText(reportBox.Text);
            MessageBox.Show("Report copied to clipboard.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, primary: true);
        B(p, "Save .txt", 290, 396, 120, 34, SaveReportFile);
        B(p, "Clear history", 420, 396, 140, 34, () =>
        {
            settings.ChangeLog.Clear();
            settings.Save();
            RefreshReport();
        }, danger: true);
    }

    private void RefreshReport()
    {
        if (reportBox == null || reportBox.IsDisposed) return;
        reportBox.Text = BuildFullReport();
        reportBox.SelectionStart = 0;
        reportBox.ScrollToCaret();
    }

    private void SaveReportFile()
    {
        string dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string path = Path.Combine(dir, $"ZufeiBooster-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(path, reportBox.Text, Encoding.UTF8);
        MessageBox.Show($"Saved to your Desktop:\n{Path.GetFileName(path)}", AppName,
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string RegText(string key, string name)
    {
        try
        {
            object? v = Registry.GetValue(key, name, null);
            return v == null ? "not set" : v.ToString() ?? "not set";
        }
        catch { return "unknown"; }
    }

    private string BuildFullReport()
    {
        var mem = SysInfo.Memory();
        var drive = SysInfo.SystemDrive();
        var sb = new StringBuilder();

        sb.AppendLine($"{AppName.ToUpperInvariant()} {Version} - ADVANCED REPORT");
        sb.AppendLine($"Generated : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        sb.AppendLine("== YOUR PC ==");
        sb.AppendLine($"CPU       : {cpuName} ({Environment.ProcessorCount} threads)");
        sb.AppendLine($"GPU       : {gpuName} {gpuVram}".TrimEnd());
        if (gpuExtra != "") sb.AppendLine($"Also      : {gpuExtra}");
        sb.AppendLine($"RAM       : {mem.total / 1073741824d:0.0} GB total, {mem.available / 1073741824d:0.0} GB free now ({mem.load}% in use)");
        sb.AppendLine($"Storage   : C: {(drive.total - drive.free) / 1073741824d:0.0} / {drive.total / 1073741824d:0.0} GB used, {drive.free / 1073741824d:0.0} GB free");
        sb.AppendLine($"OS        : {osName} ({osBuild})");
        sb.AppendLine($"Board     : {board}");
        sb.AppendLine($"Profile   : {HardwareProfile()}");
        sb.AppendLine();

        sb.AppendLine("== SETTINGS RIGHT NOW ==");
        sb.AppendLine($"Power plan        : {BoostEngine.CurrentPlanName()}");
        sb.AppendLine($"Windows Game Mode : {RegText(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled")} (1 = on)");
        sb.AppendLine($"Game DVR          : {RegText(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled")} (0 = off)");
        sb.AppendLine($"Emulator          : {(BoostEngine.AnyEmulatorRunning() ? "running" : "not running")}");
        sb.AppendLine();

        sb.AppendLine("== WHAT THIS APP CHANGED ON YOUR PC (newest last) ==");
        if (settings.ChangeLog.Count == 0)
            sb.AppendLine("Nothing yet. Press Boost system or Run cleanup and it will be listed here.");
        else
            foreach (var line in settings.ChangeLog.TakeLast(80)) sb.AppendLine(line);
        sb.AppendLine();

        sb.AppendLine("== CAN BE UNDONE ==");
        sb.AppendLine($"Saved registry values : {settings.RegBackup.Count}");
        sb.AppendLine($"Saved power plan      : {(settings.PowerPlanBackup ?? "none")}");
        sb.AppendLine("Press 'Undo boost' (Monitor or Settings page) to put everything back.");
        sb.AppendLine();

        sb.AppendLine("== NEVER DONE BY THIS APP ==");
        sb.AppendLine("No process is killed. Defender, Firewall and Windows services are not touched.");
        sb.AppendLine("Only files in the Cleanup items you ticked are deleted.");

        return sb.ToString();
    }

    // =====================================================================
    //  STARTUP MANAGER
    // =====================================================================

    private Panel startupList = null!;

    private void BuildStartup()
    {
        var p = NewPage(PgStartup, "Startup manager", "Programs that start with Windows - switch off what you do not need");

        var card = C(p, 30, 70, 720, 308);
        startupList = new Panel
        {
            Location = new Point(8, 8), Size = new Size(704, 292), AutoScroll = true,
            BackColor = Color.FromArgb(Math.Min(255, Theme.Surface.R + 4), Math.Min(255, Theme.Surface.G + 4), Math.Min(255, Theme.Surface.B + 4))
        };
        card.Controls.Add(startupList);

        L(p, "Turning a program off only stops it from starting with Windows. Nothing is deleted - switch it back on any time. " +
             "Items for 'All users' may need administrator rights.",
          30, 386, 720, 34, 8, Theme.Muted);
    }

    private void RefreshStartupList()
    {
        if (startupList == null || startupList.IsDisposed) return;

        foreach (var c in startupList.Controls.Cast<Control>().ToList())
        {
            startupList.Controls.Remove(c);
            c.Dispose();
        }

        var entries = StartupManager.List();
        var rowBack = startupList.BackColor;

        // build at 100% coordinates inside a host, then scale the host once
        var host = new Panel { Location = new Point(0, 0), Size = new Size(684, Math.Max(40, entries.Count * 46)), BackColor = rowBack };

        if (entries.Count == 0)
            L(host, "No startup programs found.", 12, 12, 600, 20, 9.5f, Theme.Muted);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            int y = i * 46;

            var nameLbl = L(host, entry.Name, 12, y + 4, 420, 20, 9, Theme.Text, true);
            nameLbl.BackColor = rowBack;
            var cmdLbl = L(host, entry.Command, 12, y + 24, 520, 16, 7.5f, Theme.Muted);
            cmdLbl.BackColor = rowBack;
            var scopeLbl = L(host, entry.UserLevel ? "Your account" : "All users", 470, y + 4, 110, 20, 8, Theme.AccentHi, true, ContentAlignment.MiddleRight);
            scopeLbl.BackColor = rowBack;

            var t = new Toggle { Location = new Point(610, y + 10), Checked = entry.Enabled };
            bool reverting = false;
            t.CheckedChanged += (_, _) =>
            {
                if (reverting) return;

                if (StartupManager.SetEnabled(entry, t.Checked))
                {
                    settings.AddLog($"Startup: {entry.Name} {(t.Checked ? "enabled" : "disabled")}");
                    settings.Save();
                }
                else
                {
                    reverting = true;
                    t.Checked = !t.Checked;
                    reverting = false;
                    MessageBox.Show("Windows did not allow that change. Run Zufei Booster as administrator for 'All users' items.",
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            host.Controls.Add(t);
        }

        UiScale.ScaleSelf(host);
        startupList.Controls.Add(host);
    }

    // =====================================================================
    //  SETTINGS
    // =====================================================================

    private void BuildSettings()
    {
        var p = NewPage(PgSettings, "Settings", "Colours, window size and behaviour");

        var about = C(p, 30, 68, 720, 60);
        L(about, $"{AppName}  {Version}", 20, 6, 500, 26, 13, Theme.AccentHi, true);
        L(about, $"Creator: {Creator}   ·   Free to share · Not for sale · No fake FPS", 20, 34, 500, 20, 8.5f, Theme.Muted);

        bool admin = AdminHelper.IsAdmin();
        L(about, admin ? "Administrator: yes" : "Administrator: no", 540, 6, 166, 20, 8.5f,
            admin ? Theme.Good : Theme.Muted, true, ContentAlignment.MiddleRight);
        if (!admin)
            B(about, "Restart as admin", 556, 28, 150, 26, () => { if (AdminHelper.RestartAsAdmin()) Close(); });

        // colour + size
        var look = C(p, 30, 136, 720, 92);
        L(look, "Colour theme", 20, 8, 300, 20, 9.5f, Theme.Text, true);
        for (int i = 0; i < Theme.Names.Length; i++)
        {
            string name = Theme.Names[i];
            var sw = new Swatch
            {
                Tint = Theme.Preview(name),
                Selected = name == Theme.Current,
                Location = new Point(16 + i * 46, 36)
            };
            tip.SetToolTip(sw, name);
            sw.Click += (_, _) => BeginInvoke(new Action(() => ApplyTheme(name)));
            look.Controls.Add(sw);
        }
        L(look, Theme.Current, 16, 70, 200, 18, 8.5f, Theme.AccentHi, true);

        L(look, "Window size", 330, 8, 300, 20, 9.5f, Theme.Text, true);
        float[] sizes = { 0.75f, 0.85f, 1.0f, 1.15f, 1.3f, 1.5f };
        for (int i = 0; i < sizes.Length; i++)
        {
            float f = UiScale.Clamp(sizes[i]);
            var b = new PillButton
            {
                Text = $"{(int)Math.Round(sizes[i] * 100)}%",
                Location = new Point(330 + i * 63, 38),
                Size = new Size(58, 32),
                Primary = Math.Abs(settings.UiScale - f) < 0.02f
            };
            b.Click += (_, _) => BeginInvoke(new Action(() => ApplyScale(f)));
            look.Controls.Add(b);
        }
        L(look, "Bigger or smaller panel - everything scales. Sizes too big for your screen are limited automatically.",
            330, 72, 380, 16, 7.5f, Theme.Muted);

        // behaviour
        var beh = C(p, 30, 236, 720, 96);
        AddBehaviour(beh, 20, 8, "Keep boost after closing", "Leave boost on when this app is closed",
            settings.KeepBoostOnClose, v => settings.KeepBoostOnClose = v);
        AddBehaviour(beh, 380, 8, "Auto boost with emulator", "Boost when an emulator starts, undo when it closes",
            settings.AutoBoost, v => settings.AutoBoost = v);
        AddBehaviour(beh, 20, 52, "Minimize to tray", "Hide in the tray instead of the taskbar",
            settings.MinimizeToTray, v => settings.MinimizeToTray = v);
        AddBehaviour(beh, 380, 52, "Welcome screen", "Show the animated intro on start",
            settings.ShowWelcome, v => settings.ShowWelcome = v);

        // shortcuts
        var tools = C(p, 30, 340, 720, 70);
        L(tools, "Shortcuts", 20, 6, 200, 18, 9, Theme.Text, true);
        B(tools, "Game Mode", 16, 30, 130, 32, () => OpenUri("ms-settings:gaming-gamemode"));
        B(tools, "Graphics", 154, 30, 130, 32, () => OpenUri("ms-settings:display-graphics"));
        B(tools, "Task Manager", 292, 30, 130, 32, () => OpenUri("taskmgr.exe"));
        B(tools, "Undo boost", 430, 30, 130, 32, RestoreBoost);
        B(tools, "Reset all", 568, 30, 130, 32, ResetAll, danger: true);
    }

    private void AddBehaviour(Control parent, int x, int y, string title, string sub, bool value, Action<bool> set)
    {
        L(parent, title, x, y, 280, 18, 9, Theme.Text, true);
        L(parent, sub, x, y + 18, 290, 16, 7.5f, Theme.Muted);
        var t = new Toggle { Location = new Point(x + 296, y + 6), Checked = value };
        t.CheckedChanged += (_, _) => { set(t.Checked); settings.Save(); };
        parent.Controls.Add(t);
    }

    private void ResetAll()
    {
        var d = new AppSettings();
        settings.ScanOnOpening = d.ScanOnOpening;
        settings.SkipFilesInUse = d.SkipFilesInUse;
        settings.SaveReport = d.SaveReport;
        settings.CompletionNotice = d.CompletionNotice;
        settings.KeepBoostOnClose = d.KeepBoostOnClose;
        settings.AutoBoost = d.AutoBoost;
        settings.MinimizeToTray = d.MinimizeToTray;
        settings.ShowWelcome = d.ShowWelcome;
        settings.ThemeName = d.ThemeName;
        settings.UiScale = d.UiScale;
        settings.LastCleanup = null;
        settings.Save();

        Theme.Apply(settings.ThemeName);
        UiScale.Factor = settings.UiScale;
        BeginInvoke(new Action(() => Rebuild(PgSettings)));
    }

    private static void OpenUri(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }
}
