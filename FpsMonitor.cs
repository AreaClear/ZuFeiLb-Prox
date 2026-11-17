using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace ZufeiBooster;

/// <summary>
/// REAL FPS using PresentMon (free Intel tool, needs administrator rights).
/// The app can download PresentMon for you (official GitHub release) into %AppData%\ZufeiBooster\PresentMon.
/// Nothing here is simulated.
/// </summary>
internal sealed class FpsMonitor : IDisposable
{
    private Process? proc;
    private readonly object gate = new();
    private readonly Queue<(DateTime t, double ms)> frames = new();
    private int msColumn = -1;
    private string lastError = "";

    public string Status { get; private set; } = "";
    public DateTime StartedAt { get; private set; } = DateTime.MinValue;
    public bool Running => proc != null && !proc.HasExited;

    private static string DownloadDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZufeiBooster", "PresentMon");

    public static string? FindExe()
    {
        try
        {
            foreach (var d in new[] { AppContext.BaseDirectory, Path.Combine(AppContext.BaseDirectory, "PresentMon"), DownloadDir })
            {
                if (!Directory.Exists(d)) continue;
                var f = Directory.GetFiles(d, "PresentMon*.exe").FirstOrDefault();
                if (f != null) return f;
            }
        }
        catch { }
        return null;
    }

    /// <summary>Downloads the latest official PresentMon console exe. Returns the file path or null.</summary>
    public static async Task<string?> DownloadAsync(Action<string> status)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ZufeiBooster");

            status("Looking for the latest PresentMon...");
            string json = await http.GetStringAsync("https://api.github.com/repos/GameTechDev/PresentMon/releases/latest");

            string? url = null, name = null;
            using (var doc = JsonDocument.Parse(json))
            {
                foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
                {
                    string n = a.GetProperty("name").GetString() ?? "";
                    if (n.StartsWith("PresentMon", StringComparison.OrdinalIgnoreCase) &&
                        n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        n.Contains("x64", StringComparison.OrdinalIgnoreCase) &&
                        !n.Contains("Installer", StringComparison.OrdinalIgnoreCase) &&
                        !n.Contains("Capture", StringComparison.OrdinalIgnoreCase))
                    {
                        url = a.GetProperty("browser_download_url").GetString();
                        name = n;
                        break;
                    }
                }
            }

            if (url == null || name == null)
            {
                status("No PresentMon console exe found in the latest release.");
                return null;
            }

            status($"Downloading {name}...");
            byte[] data = await http.GetByteArrayAsync(url);

            Directory.CreateDirectory(DownloadDir);
            string path = Path.Combine(DownloadDir, name);
            await File.WriteAllBytesAsync(path, data);
            status("PresentMon downloaded.");
            return path;
        }
        catch (Exception ex)
        {
            status("Download failed: " + ex.Message);
            return null;
        }
    }

    public async Task<bool> StartAsync(string processExe)
    {
        Stop();

        string? exe = FindExe();
        if (exe == null)
        {
            Status = "PresentMon.exe not found.";
            return false;
        }

        string[] attempts =
        {
            $"--process_name {processExe} --output_stdout --no_console_stats --stop_existing_session",
            $"--process_name {processExe} --output_stdout --no_console_stats --terminate_existing_session",
            $"--process_name {processExe} --output_stdout --no_console_stats",
            $"-process_name {processExe} -output_stdout -stop_existing_session"
        };

        lastError = "";
        foreach (string args in attempts)
        {
            if (TryLaunch(exe, args))
            {
                await Task.Delay(1500);
                if (Running)
                {
                    Status = $"Measuring {processExe}";
                    StartedAt = DateTime.UtcNow;
                    return true;
                }
            }
            Stop();
        }

        Status = lastError != "" ? $"PresentMon: {lastError}" : "PresentMon could not start. Run Zufei Booster as administrator.";
        return false;
    }

    private bool TryLaunch(string exe, string args)
    {
        try
        {
            lock (gate) { frames.Clear(); }
            msColumn = -1;

            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            proc = Process.Start(psi);
            if (proc == null) return false;

            proc.OutputDataReceived += (_, e) => Parse(e.Data);
            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data)) lastError = e.Data.Trim();
            };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            return true;
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            return false;
        }
    }

    private void Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var cols = line.Split(',');

        if (msColumn < 0)
        {
            for (int i = 0; i < cols.Length; i++)
            {
                string c = cols[i].Trim().ToLowerInvariant();
                if (c == "msbetweenpresents" || c == "frametime") { msColumn = i; break; }
            }
            if (msColumn < 0 && !line.Contains(',')) lastError = line.Trim();
            return; // header line
        }

        if (msColumn < cols.Length &&
            double.TryParse(cols[msColumn], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms) && ms > 0)
        {
            lock (gate)
            {
                frames.Enqueue((DateTime.UtcNow, ms));
                while (frames.Count > 0 && (DateTime.UtcNow - frames.Peek().t).TotalSeconds > 2) frames.Dequeue();
            }
        }
    }

    /// <summary>Average FPS and frame time over the last second.</summary>
    public (double fps, double frameMs) Current()
    {
        lock (gate)
        {
            var now = DateTime.UtcNow;
            var recent = frames.Where(f => (now - f.t).TotalSeconds <= 1).ToList();
            if (recent.Count == 0) return (0, 0);
            double avg = recent.Average(f => f.ms);
            return (1000d / avg, avg);
        }
    }

    public void Stop()
    {
        try
        {
            if (proc != null && !proc.HasExited) proc.Kill(true);
        }
        catch { }
        proc?.Dispose();
        proc = null;
    }

    public void Dispose() => Stop();
}
