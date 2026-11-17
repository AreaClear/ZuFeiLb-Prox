using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZufeiBooster.Overlay;

public static class EmulatorWindows
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();

    /// <summary>The app is DpiUnaware, so window rects must be divided by this.</summary>
    public static double SystemScale
    {
        get
        {
            try { return Math.Max(1.0, GetDpiForSystem() / 96.0); }
            catch { return 1.0; }
        }
    }

    public static bool IsAlive(IntPtr h) => IsWindow(h) && IsWindowVisible(h) && !IsIconic(h);

    public static bool GetBounds(IntPtr h, out Rectangle bounds)
    {
        if (GetWindowRect(h, out var r))
        {
            bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            return true;
        }
        bounds = Rectangle.Empty;
        return false;
    }

    /// <summary>Largest visible top-level window owned by this process.</summary>
    public static IntPtr FindMainWindow(Process process)
    {
        IntPtr best = IntPtr.Zero;
        int bestArea = 0;

        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            GetWindowThreadProcessId(h, out uint pid);
            if (pid != process.Id) return true;
            if (!GetBounds(h, out var b)) return true;

            int area = b.Width * b.Height;
            if (b.Width > 400 && b.Height > 300 && area > bestArea)
            {
                bestArea = area;
                best = h;
            }
            return true;
        }, IntPtr.Zero);

        return best;
    }
}
