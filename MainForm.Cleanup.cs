using System.Text;

namespace ZufeiBooster;

public sealed partial class MainForm
{
    // =====================================================================
    //  CLEANUP
    // =====================================================================

    private void BuildCleanup()
    {
        var p = NewPage(2, "Cleanup Studio", "Free up space and keep your PC running smoothly");

        // stat tiles
        var t1 = C(p, 30, 72, 230, 56);
        L(t1, "Cache size", 14, 6, 150, 16, 8, Theme.Muted);
        tileCache = L(t1, "--", 14, 24, 200, 26, 12, Theme.Text, true);

        var t2 = C(p, 275, 72, 230, 56);
        L(t2, "Last cleanup", 14, 6, 150, 16, 8, Theme.Muted);
        tileLast = L(t2, LastCleanupText(), 14, 24, 200, 26, 12, Theme.Text, true);

        var t3 = C(p, 520, 72, 230, 56);
        L(t3, "Selected items", 14, 6, 150, 16, 8, Theme.Muted);
        tileSelected = L(t3, "0 items", 14, 24, 200, 26, 12, Theme.Text, true);

        // items
        items.Add(new CleanItem
        {
            Title = "Temporary files", Sub = "App and system temp files", DefaultOn = true,
            Measure = () => Cleaner.DirsSize(Cleaner.TempDirs),
            Clean = () => Cleaner.DeleteContents(Cleaner.TempDirs)
        });
        items.Add(new CleanItem
        {
            Title = "Browser cache", Sub = "Chrome, Edge and Brave cache", DefaultOn = true,
            Measure = () => Cleaner.DirsSize(Cleaner.BrowserCacheDirs),
            Clean = () => Cleaner.DeleteContents(Cleaner.BrowserCacheDirs)
        });
        items.Add(new CleanItem
        {
            Title = "Recycle bin", Sub = "Files in your recycle bin", DefaultOn = true,
            Measure = Cleaner.RecycleBinSize,
            Clean = Cleaner.EmptyRecycleBin
        });
        items.Add(new CleanItem
        {
            Title = "Downloads", Sub = "Everything in Downloads", NeedsConfirm = true,
            Measure = () => Cleaner.DirSize(Cleaner.Downloads),
            Clean = () => Cleaner.DeleteContents(Cleaner.Downloads)
        });
        items.Add(new CleanItem
        {
            Title = "Large files", Sub = "Over 500 MB, in Downloads", NeedsConfirm = true,
            Measure = Cleaner.LargeFilesSize,
            Clean = Cleaner.DeleteLargeFiles
        });

        L(p, "Items to clean", 30, 138, 300, 20, 9, Theme.Text, true);
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            int y = 164 + i * 38;

            it.Check = new RoundCheck { Location = new Point(34, y + 8), Checked = it.DefaultOn };
            it.Check.CheckedChanged += (_, _) => UpdateSelectedCount();
            p.Controls.Add(it.Check);

            L(p, it.Title, 66, y, 230, 18, 9, Theme.Text, true);
            L(p, it.Sub, 66, y + 18, 230, 16, 7.5f, Theme.Muted);
            it.SizeLabel = L(p, "--", 300, y + 6, 70, 20, 8.5f, Theme.Muted, true, ContentAlignment.MiddleRight);
        }

        // options
        L(p, "Options", 400, 138, 300, 20, 9, Theme.Text, true);
        AddOption(p, 0, "Scan on opening", "Refresh sizes when this page opens",
            settings.ScanOnOpening, v => settings.ScanOnOpening = v);
        AddOption(p, 1, "Skip files in use", "Leave open files untouched (quiet)",
            settings.SkipFilesInUse, v => settings.SkipFilesInUse = v);
        AddOption(p, 2, "Save cleanup report", "Keep a short summary file",
            settings.SaveReport, v => settings.SaveReport = v);
        AddOption(p, 3, "Completion notice", "Message when finished",
            settings.CompletionNotice, v => settings.CompletionNotice = v);

        runCleanup = B(p, "Run cleanup", 400, 318, 350, 38, () => Safe(RunCleanupAsync), primary: true);

        // activity
        var act = C(p, 30, 368, 720, 68);
        L(act, "Activity", 14, 6, 200, 16, 8.5f, Theme.Text, true);
        L(act, "Current session", 500, 6, 206, 16, 8, Theme.Muted, false, ContentAlignment.MiddleRight);
        activityText = L(act, "No activity yet.", 14, 24, 692, 42, 8, Theme.Muted);

        UpdateSelectedCount();
    }

    private void AddOption(Control p, int row, string title, string sub, bool value, Action<bool> set)
    {
        int y = 164 + row * 38;
        L(p, title, 400, y, 270, 18, 9, Theme.Text, true);
        L(p, sub, 400, y + 18, 270, 16, 7.5f, Theme.Muted);

        var t = new Toggle { Location = new Point(706, y + 6), Checked = value };
        t.CheckedChanged += (_, _) => { set(t.Checked); settings.Save(); };
        p.Controls.Add(t);
    }

    private string LastCleanupText()
    {
        if (settings.LastCleanup is not DateTime d) return "Never";
        int days = (int)(DateTime.Now.Date - d.Date).TotalDays;
        return days <= 0 ? "Today" : days == 1 ? "1 day ago" : $"{days} days ago";
    }

    private void UpdateSelectedCount()
    {
        int n = items.Count(i => i.Check.Checked);
        tileSelected.Text = n == 1 ? "1 item" : $"{n} items";
    }

    private void AddLog(string msg)
    {
        log.Add((msg, DateTime.Now));
        var last = log.AsEnumerable().Reverse().Take(3)
            .Select(l => $"•  {l.msg}   ·   {l.time:HH:mm:ss}");
        activityText.Text = string.Join("\n", last);
    }

    private async Task ScanAsync()
    {
        if (busy) return;
        busy = true;
        runCleanup.Enabled = false;

        try
        {
            foreach (var it in items) it.SizeLabel.Text = "...";

            foreach (var it in items)
            {
                var item = it;
                item.Size = await Task.Run(item.Measure);
                item.SizeLabel.Text = SysInfo.FormatSize(item.Size);
            }

            long cache = items.Take(3).Sum(i => i.Size);
            tileCache.Text = SysInfo.FormatSize(cache);
            tileLast.Text = LastCleanupText();
            AddLog("Scan finished");
        }
        finally
        {
            busy = false;
            runCleanup.Enabled = true;
        }
    }

    private async Task RunCleanupAsync()
    {
        if (busy) return;

        var selected = items.Where(i => i.Check.Checked).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Select at least one item to clean.", AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var risky = selected.Where(i => i.NeedsConfirm).Select(i => i.Title).ToList();
        if (risky.Count > 0)
        {
            var ok = MessageBox.Show(
                $"These items will be deleted PERMANENTLY (no recycle bin):\n\n• {string.Join("\n• ", risky)}\n\nContinue?",
                AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (ok != DialogResult.Yes) return;
        }

        busy = true;
        runCleanup.Enabled = false;
        runCleanup.Text = "Cleaning...";

        long total = 0;
        int failed = 0;
        var report = new StringBuilder();
        report.AppendLine($"{AppName} cleanup report - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        try
        {
            // Large files first, so "Downloads" afterwards just sweeps the rest.
            foreach (var it in selected.OrderBy(i => i.Title == "Large files" ? 0 : 1))
            {
                var item = it;
                var (freed, fail) = await Task.Run(item.Clean);
                total += freed;
                failed += fail;
                report.AppendLine($"{item.Title}: freed {SysInfo.FormatSize(freed)}, skipped {fail}");
                settings.AddLog($"Cleanup: {item.Title} - freed {SysInfo.FormatSize(freed)}, skipped {fail} file(s) in use");
                AddLog($"{item.Title} cleaned ({SysInfo.FormatSize(freed)})");
            }

            settings.LastCleanup = DateTime.Now;
            settings.AddLog($"Cleanup finished - total freed {SysInfo.FormatSize(total)}");
            settings.Save();
            report.AppendLine($"Total freed: {SysInfo.FormatSize(total)}");

            if (settings.SaveReport)
            {
                try
                {
                    Directory.CreateDirectory(AppSettings.Dir);
                    string path = Path.Combine(AppSettings.Dir, $"cleanup-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                    File.WriteAllText(path, report.ToString());
                    AddLog("Report saved");
                }
                catch { }
            }
        }
        finally
        {
            runCleanup.Text = "Run cleanup";
            busy = false;
            runCleanup.Enabled = true;
        }

        await ScanAsync();

        if (settings.CompletionNotice)
        {
            string extra = failed > 0 && !settings.SkipFilesInUse
                ? $"\n{failed} file(s) were in use or protected and were skipped."
                : "";
            MessageBox.Show($"Cleanup finished.\nFreed about {SysInfo.FormatSize(total)}.{extra}",
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

}
