namespace ZufeiBooster;

/// <summary>Lets the user pick the window size by scaling every control and font from the 100% layout.</summary>
internal static class UiScale
{
    public static float Factor = 1f;

    public static int S(int v) => (int)Math.Round(v * Factor);

    /// <summary>Keeps the window inside the screen (no scale makes it bigger than the desktop).</summary>
    public static float Clamp(float f)
    {
        float max = 1.6f;
        try
        {
            var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
            max = Math.Min(max, Math.Min((wa.Height - 20) / 560f, (wa.Width - 20) / 780f));
        }
        catch { }
        return Math.Clamp(f, 0.7f, Math.Max(0.7f, max));
    }

    private static void ScaleTree(Control c, float f)
    {
        foreach (Control k in c.Controls) ScaleTree(k, f);

        var b = c.Bounds;
        int x1 = (int)Math.Round(b.X * f), y1 = (int)Math.Round(b.Y * f);
        int x2 = (int)Math.Round(b.Right * f), y2 = (int)Math.Round(b.Bottom * f);
        c.SetBounds(x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1));

        try { c.Font = new Font(c.Font.FontFamily, c.Font.Size * f, c.Font.Style); } catch { }
    }

    /// <summary>Scale one control tree that was built with 100% coordinates.</summary>
    public static void ScaleSelf(Control c)
    {
        if (Math.Abs(Factor - 1f) < 0.01f) return;
        ScaleTree(c, Factor);
    }

    public static void ApplyChildren(Control root, Control? skip)
    {
        if (Math.Abs(Factor - 1f) < 0.01f) return;
        foreach (var c in root.Controls.Cast<Control>().ToList())
            if (c != skip) ScaleTree(c, Factor);
    }
}
