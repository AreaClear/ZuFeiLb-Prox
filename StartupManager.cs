using Microsoft.Win32;

namespace ZufeiBooster;

internal sealed class StartupEntry
{
    public string Name = "";
    public string Command = "";
    public bool UserLevel;           // HKCU (true) or HKLM (false)
    public string RunKey = "";
    public string ApprovedKey = "";
    public bool Enabled;
}

/// <summary>
/// Lists programs that start with Windows and switches them on/off the same way Task Manager does
/// (StartupApproved). Nothing is deleted, so it is fully reversible.
/// </summary>
internal static class StartupManager
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32Path = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRun = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedRun32 = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";

    public static List<StartupEntry> List()
    {
        var list = new List<StartupEntry>();
        Read(list, Registry.CurrentUser, true, RunPath, ApprovedRun);
        Read(list, Registry.LocalMachine, false, RunPath, ApprovedRun);
        Read(list, Registry.LocalMachine, false, Run32Path, ApprovedRun32);
        return list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Read(List<StartupEntry> list, RegistryKey hive, bool user, string runPath, string approvedPath)
    {
        try
        {
            using var run = hive.OpenSubKey(runPath);
            if (run == null) return;
            using var approved = hive.OpenSubKey(approvedPath);

            foreach (string name in run.GetValueNames())
            {
                string cmd = run.GetValue(name)?.ToString() ?? "";
                bool enabled = true;

                if (approved?.GetValue(name) is byte[] b && b.Length > 0)
                    enabled = (b[0] & 1) == 0; // 2/6 = enabled, 3 = disabled

                list.Add(new StartupEntry
                {
                    Name = name, Command = cmd, UserLevel = user,
                    RunKey = runPath, ApprovedKey = approvedPath, Enabled = enabled
                });
            }
        }
        catch { }
    }

    public static bool SetEnabled(StartupEntry e, bool enable)
    {
        try
        {
            var hive = e.UserLevel ? Registry.CurrentUser : Registry.LocalMachine;
            using var key = hive.CreateSubKey(e.ApprovedKey, true);
            if (key == null) return false;

            var data = new byte[12];
            data[0] = (byte)(enable ? 2 : 3);
            key.SetValue(e.Name, data, RegistryValueKind.Binary);
            e.Enabled = enable;
            return true;
        }
        catch { return false; }
    }
}
