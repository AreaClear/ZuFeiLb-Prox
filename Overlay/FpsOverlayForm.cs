using System.Runtime.InteropServices;

namespace ZufeiBooster.Overlay;

/// <summary>Click-through, always-on-top counter that rides on the emulator window.</summary>
public sealed class FpsOverlayForm : Form
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);

    private readonly IntPtr target;
    private readonly FakeFpsEngine engine;
    private readonly Point offset;
    private readonly double scale;
    private readonly Label label = new();
    private readonly System.Windows.Forms.Timer tick = new();
    private readonly Random jitter = new();

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000   // WS_EX_LAYERED
                       | 0x00000020    // WS_EX_TRANSPARENT  (click-through)
                       | 0x00000080    // WS_EX_TOOLWINDOW
                       | 0x08000000;   // WS_EX_NOACTIVATE
            return cp;
        }
    }

    public FpsOverlayForm(IntPtr target, FakeFpsEngine engine, Point offset, float fontSize = 15f, bool cover = false)
    {
        this.target = target;
        this.engine = engine;
        this.offset = offset;
        scale = EmulatorWindows.SystemScale;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size((int)(110 / scale), (int)(30 / scale));

        if (cover) { BackColor = Color.Black; Opacity = 0.55; }
        else { BackColor = Color.FromArgb(0, 0, 1); TransparencyKey = Color.FromArgb(0, 0, 1); }

        label.AutoSize = true;
        label.ForeColor = Color.Lime;
        label.BackColor = BackColor;
        label.Font = new Font("Consolas", fontSize / (float)scale, FontStyle.Bold);
        label.Location = new Point(2, 1);
        Controls.Add(label);

        tick.Interval = 250;
        tick.Tick += OnTick;
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); tick.Start(); }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!EmulatorWindows.IsAlive(target))
        {
            if (Visible) Hide();
        }
        else
        {
            if (!Visible) Show();

            label.Text = $"{engine.Next()} FPS";

            if (GetWindowRect(target, out var r))
                Location = new Point((int)((r.Left + offset.X) / scale),
                                     (int)((r.Top + offset.Y) / scale));
        }

        tick.Interval = 160 + jitter.Next(0, 220);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        tick.Stop();
        tick.Tick -= OnTick;
        tick.Dispose();
        base.OnFormClosing(e);
    }
}
