using System.Diagnostics;
using System.Security.Principal;

namespace ZufeiBooster;

internal static class AdminHelper
{
    public static bool IsAdmin()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>Starts a new copy with the Windows "Run as administrator" prompt. Returns false if the user said no.</summary>
    public static bool RestartAsAdmin()
    {
        try
        {
            var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
            Process.Start(psi);
            return true;
        }
        catch { return false; }
    }
}
