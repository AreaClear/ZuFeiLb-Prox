using System.Runtime.InteropServices;

namespace ZufeiBooster.Overlay;

/// <summary>Click-through, always-on-top counter that rides on the emulator window.</summary>
public sealed class FpsOverlayForm : Form
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

    private readonly IntPtr target;
    private readonly FakeFpsEngine engine;
    private readonly double scale;
    private readonly Label label = new();
    private readonly System.Windows.Forms.Timer tick = new();
    private readonly Random jitter = new();
    private Point offset;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000 | 0x00000020 | 0x00000080 | 0x08000000;
            return cp;
        }
    }

    public void SetOffset(Point p) => offset = p;

    public FpsOverlayForm(IntPtr target, FakeFpsEngine engine, Point offset, bool cover = false)
    {
        this.target = target;
        this.engine = engine;
        this.offset = offset;
        scale = EmulatorWindows.SystemScale;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size((int)(124 / scale), (int)(30 / scale));

        if (cover) { BackColor = Color.Black; Opacity = 0.92; }
        else { BackColor = Color.FromArgb(0, 0, 1); TransparencyKey = Color.FromArgb(0, 0, 1); }

        label.AutoSize = false;
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.ForeColor = Color.White;
        label.BackColor = BackColor;
        label.Font = new Font("Consolas", 13f / (float)scale, FontStyle.Bold);
        label.Padding = new Padding((int)(4 / scale), 0, 0, 0);
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

            label.Text = $"FPS {engine.Next()}";

            if (GetWindowRect(target, out var r))
            {
                Location = new Point((int)((r.Left + offset.X) / scale),
                                     (int)((r.Bottom - offset.Y) / scale));
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
            }
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
