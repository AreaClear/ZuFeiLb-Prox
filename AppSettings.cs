using System.Text.Json;

namespace ZufeiBooster;

internal sealed class AppSettings
{
    public bool ScanOnOpening { get; set; } = true;
    public bool SkipFilesInUse { get; set; } = true;
    public bool SaveReport { get; set; } = false;
    public bool CompletionNotice { get; set; } = true;
    public DateTime? LastCleanup { get; set; }
    public bool KeepBoostOnClose { get; set; } = true;
    public bool AutoBoost { get; set; } = false;
    public bool MinimizeToTray { get; set; } = false;
    public bool ShowWelcome { get; set; } = true;
    public float UiScale { get; set; } = 1.0f;
    public List<string> ChangeLog { get; set; } = new();
    public bool FakeFps { get; set; } = false;
    public int FakeFpsTarget { get; set; } = 120;
    public bool FakeFpsScale { get; set; } = true;
    public bool FakeFpsCover { get; set; } = true;
    public void AddLog(string message)
    {
        ChangeLog.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}");
        if (ChangeLog.Count > 300) ChangeLog.RemoveRange(0, ChangeLog.Count - 300);
    }
    public string ThemeName { get; set; } = "Cyan";
    public string? PowerPlanBackup { get; set; }
    public Dictionary<string, string?> RegBackup { get; set; } = new();

    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZufeiBooster");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
        }
        catch { }
    }
}
