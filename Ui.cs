using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ZufeiBooster;

internal static class Theme
{
    public static Color Bg = Color.FromArgb(8, 16, 24);
    public static Color Surface = Color.FromArgb(14, 28, 40);
    public static Color SurfaceHi = Color.FromArgb(22, 44, 60);
    public static Color Border = Color.FromArgb(28, 60, 80);
    public static Color BgEnd = Color.FromArgb(10, 36, 52);
    public static Color Accent = Color.FromArgb(0, 200, 230);
    public static Color AccentHi = Color.FromArgb(80, 225, 245);
    public static readonly Color Text = Color.FromArgb(236, 242, 250);
    public static readonly Color Muted = Color.FromArgb(150, 165, 185);
    public static readonly Color Good = Color.FromArgb(80, 220, 140);
    public static readonly Color Danger = Color.FromArgb(235, 80, 95);

    public static readonly string[] Names = { "Cyan", "Red", "Purple", "Green", "Orange", "Blue" };
    public static string Current = "Cyan";

    private static (Color accent, Color hi, Color bg, Color surface, Color surfHi, Color border) Palette(string n) => n switch
    {
        "Red" => (C(235, 60, 75), C(255, 100, 110), C(20, 10, 12), C(34, 18, 22), C(52, 26, 32), C(84, 38, 46)),
        "Purple" => (C(124, 92, 255), C(146, 120, 255), C(18, 14, 34), C(29, 23, 54), C(42, 34, 76), C(55, 46, 96)),
        "Green" => (C(50, 210, 120), C(100, 235, 160), C(8, 18, 14), C(14, 32, 24), C(22, 50, 38), C(30, 70, 52)),
        "Orange" => (C(255, 150, 40), C(255, 185, 90), C(20, 14, 8), C(36, 26, 14), C(54, 38, 20), C(84, 58, 30)),
        "Blue" => (C(70, 130, 255), C(120, 165, 255), C(10, 13, 28), C(18, 24, 48), C(28, 38, 74), C(40, 54, 100)),
        _ => (C(0, 200, 230), C(80, 225, 245), C(8, 16, 24), C(14, 28, 40), C(22, 44, 60), C(28, 60, 80)),
    };

    private static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);

    public static Color Mix(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    public static Color Preview(string name) => Palette(name).accent;

    public static void Apply(string name)
    {
        if (!Names.Contains(name)) name = "Cyan";
        var p = Palette(name);
        Current = name;
        Accent = p.accent; AccentHi = p.hi; Bg = p.bg;
        Surface = p.surface; SurfaceHi = p.surfHi; Border = p.border;
        BgEnd = Mix(p.bg, p.accent, 0.22f);
    }

    public static Font F(float size, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", size, style);

    private static string? cjk;

    /// <summary>Installed font that can show Chinese / Japanese characters (falls back to Segoe UI).</summary>
    public static string CjkFamily
    {
        get
        {
            if (cjk == null)
            {
                var installed = new InstalledFontCollection().Families
                    .Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                cjk = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Yu Gothic UI", "Microsoft JhengHei UI",
                              "Meiryo UI", "MS Gothic", "SimSun" }
                    .FirstOrDefault(installed.Contains) ?? "Segoe UI";
            }
            return cjk;
        }
    }

    public static bool HasCjk(string s) => s.Any(c => c >= 0x2E80);

    /// <summary>Font that is guaranteed to show the characters of <paramref name="text"/>.</summary>
    public static Font FontFor(string text, float size, FontStyle style = FontStyle.Regular) =>
        new(HasCjk(text) ? CjkFamily : "Segoe UI", size, style);

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Prepare(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }
}

/// <summary>Rounded card panel.</summary>
internal class Card : Panel
{
    public Color Fill { get; set; } = Theme.Surface;
    public Color Stroke { get; set; } = Theme.Border;
    public int Radius { get; set; } = 12;
    public int Alpha { get; set; } = 175;
    public bool Glow { get; set; }

    public Card()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.Round(r, Radius);
        using var fill = new SolidBrush(Color.FromArgb(Alpha, Fill));
        using var pen = new Pen(Stroke);
        e.Graphics.FillPath(fill, path);
        if (Glow)
        {
            using var gp = new Pen(Color.FromArgb(55, Theme.Accent), 4f);
            e.Graphics.DrawPath(gp, path);
        }
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>Rounded button. Primary = purple, Danger = red, Selected = nav highlight.</summary>
internal class PillButton : Control
{
    public bool Primary { get; set; }
    public bool Danger { get; set; }
    public string? Icon { get; set; }
    private bool selected;
    public bool Selected { get => selected; set { selected = value; Invalidate(); } }
    private bool hover, down;

    public PillButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Theme.F(9f, FontStyle.Bold);
        ForeColor = Theme.Text;
        Size = new Size(120, 36);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);

        Color fill, text = Theme.Text;
        if (!Enabled) { fill = Theme.SurfaceHi; text = Theme.Muted; }
        else if (Danger) fill = down ? Color.FromArgb(190, 55, 70) : hover ? Color.FromArgb(250, 100, 112) : Theme.Danger;
        else if (Primary || Selected) fill = down ? Color.FromArgb(100, 70, 220) : hover ? Theme.AccentHi : Theme.Accent;
        else { fill = hover ? Theme.SurfaceHi : Color.Transparent; text = hover ? Theme.Text : Theme.Muted; }

        using (var path = Theme.Round(r, Math.Min(Height / 2, 14)))
        using (var b = new SolidBrush(fill))
            e.Graphics.FillPath(b, path);

        using var tb = new SolidBrush(text);
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        float textLeft = 0;
        if (Icon != null)
        {
            float isz = Height * 0.5f;
            float ix = string.IsNullOrEmpty(Text) ? (Width - isz) / 2f : Height * 0.3f;
            Icons.Draw(e.Graphics, Icon, new RectangleF(ix, (Height - isz) / 2f, isz, isz), text);
            if (!string.IsNullOrEmpty(Text)) textLeft = ix + isz + Height * 0.05f;
        }

        if (!string.IsNullOrEmpty(Text))
            e.Graphics.DrawString(Text, Font, tb, new RectangleF(textLeft, 0, Width - textLeft, Height), sf);
    }
}

/// <summary>On/off switch.</summary>
internal class Toggle : Control
{
    private bool isOn;
    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => isOn;
        set { if (isOn != value) { isOn = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
    }

    public Toggle()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Size = new Size(44, 24);
    }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.Round(r, Height / 2))
        using (var b = new SolidBrush(isOn ? Theme.Accent : Theme.Border))
            e.Graphics.FillPath(b, path);

        int k = Height - 8;
        int x = isOn ? Width - k - 4 : 4;
        using var kb = new SolidBrush(Color.White);
        e.Graphics.FillEllipse(kb, x, 4, k, k);
    }
}

/// <summary>Round check box.</summary>
internal class RoundCheck : Control
{
    private bool isOn;
    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => isOn;
        set { if (isOn != value) { isOn = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
    }

    public RoundCheck()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Size = new Size(20, 20);
    }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.Round(r, 5);
        if (isOn)
        {
            using var b = new SolidBrush(Theme.Accent);
            e.Graphics.FillPath(b, path);
            using var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float w = Width, h = Height;
            e.Graphics.DrawLines(pen, new[]
            {
                new PointF(w * 0.25f, h * 0.52f), new PointF(w * 0.44f, h * 0.70f), new PointF(w * 0.76f, h * 0.30f)
            });
        }
        else
        {
            using var pen = new Pen(Theme.Muted, 1.5f);
            e.Graphics.DrawPath(pen, path);
        }
    }
}

/// <summary>Thin progress bar.</summary>
internal class ThinBar : Control
{
    private int val;
    public int Value { get => val; set { val = Math.Clamp(value, 0, 100); Invalidate(); } }

    public ThinBar()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Size = new Size(200, 6);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        var track = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var p = Theme.Round(track, Height / 2))
        using (var b = new SolidBrush(Theme.Border))
            e.Graphics.FillPath(b, p);

        int w = (int)((Width - 1) * (val / 100d));
        if (w < Height) w = val > 0 ? Height : 0;
        if (w > 0)
        {
            using var p = Theme.Round(new Rectangle(0, 0, w, Height - 1), Height / 2);
            using var b = new SolidBrush(Theme.Accent);
            e.Graphics.FillPath(b, p);
        }
    }
}

/// <summary>Dashed ring with a bolt in the middle (Home page icon).</summary>
internal class RingIcon : Control
{
    public string Kind { get; set; } = "bolt";

    public RingIcon()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Size = new Size(110, 110);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        using var ring = new Pen(Theme.Accent, 2f) { DashStyle = DashStyle.Dash };
        e.Graphics.DrawEllipse(ring, 3, 3, Width - 7, Height - 7);
        int off = (int)(Width * 0.13f);
        using var inner = new Pen(Theme.Border, 1.5f);
        e.Graphics.DrawEllipse(inner, off, off, Width - 2 * off - 1, Height - 2 * off - 1);

        Icons.Draw(e.Graphics, Kind, new RectangleF(Width * 0.29f, Height * 0.29f, Width * 0.42f, Height * 0.42f), Theme.AccentHi);
    }
}


/// <summary>Round colour swatch used in Settings.</summary>
internal class Swatch : Control
{
    public Color Tint { get; set; } = Color.Gray;
    public bool Selected { get; set; }

    public Swatch()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Size = new Size(36, 36);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        using var b = new SolidBrush(Tint);
        e.Graphics.FillEllipse(b, 5, 5, Width - 11, Height - 11);
        if (Selected)
        {
            using var pen = new Pen(Color.White, 2f);
            e.Graphics.DrawEllipse(pen, 1, 1, Width - 3, Height - 3);
        }
    }
}

/// <summary>Circular progress ring with percentage text. Animate() eases to a new value.</summary>
internal class ProgressRing : Control
{
    private float val;
    private float goal;
    private bool done;
    private System.Windows.Forms.Timer? ease;

    public string Suffix { get; set; } = "%";
    public float Value { get => val; set { val = Math.Clamp(value, 0, 100); Invalidate(); } }
    public bool Done { get => done; set { done = value; Invalidate(); } }

    public ProgressRing()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Size = new Size(150, 150);
    }

    public void Animate(float to)
    {
        goal = Math.Clamp(to, 0, 100);
        if (ease == null)
        {
            ease = new System.Windows.Forms.Timer { Interval = 20 };
            ease.Tick += (_, _) =>
            {
                float d = goal - val;
                if (Math.Abs(d) < 0.4f) { Value = goal; ease!.Stop(); return; }
                Value = val + d * 0.25f;
            };
        }
        if (!ease.Enabled) ease.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { ease?.Stop(); ease?.Dispose(); }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        float th = Math.Max(5f, Height / 16f);
        int pad = (int)(th / 2) + 2;
        var r = new Rectangle(pad, pad, Width - 2 * pad - 1, Height - 2 * pad - 1);
        Color c = done ? Theme.Good : Theme.Accent;

        using (var glow = new Pen(Color.FromArgb(40, c), th + 5f))
            if (val > 0.5f)
                e.Graphics.DrawArc(glow, r, -90, Math.Min(359.9f, 360f * val / 100f));

        using (var track = new Pen(Theme.Border, th))
            e.Graphics.DrawEllipse(track, r);

        if (val > 0.5f)
        {
            using var arc = new Pen(c, th) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawArc(arc, r, -90, Math.Min(359.9f, 360f * val / 100f));
        }

        using var f = new Font("Segoe UI", Math.Max(8f, Height / 6.2f), FontStyle.Bold);
        using var tb = new SolidBrush(done ? Theme.Good : Theme.Text);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString($"{(int)Math.Round(val)}{Suffix}", f, tb, new RectangleF(0, 0, Width, Height), sf);
    }
}

/// <summary>Transparent page container (lets the gradient background show through).</summary>
internal class Page : Panel
{
    public Page()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
}

/// <summary>Small multi-series line chart.</summary>
internal class Sparkline : Control
{
    private readonly List<Queue<double>> data = new();
    public Color[] Colors { get; set; } = Array.Empty<Color>();
    public int MaxPoints { get; set; } = 60;
    public double Max { get; set; } = 100;

    public Sparkline(int seriesCount = 1)
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        for (int i = 0; i < seriesCount; i++) data.Add(new Queue<double>());
        Size = new Size(300, 60);
    }

    public void Add(int series, double value)
    {
        if (series < 0 || series >= data.Count) return;
        var q = data[series];
        q.Enqueue(value);
        while (q.Count > MaxPoints) q.Dequeue();
    }

    public void Redraw() => Invalidate();

    protected override void OnPaint(PaintEventArgs e)
    {
        Theme.Prepare(e.Graphics);
        var g = e.Graphics;

        using (var grid = new Pen(Color.FromArgb(70, Theme.Border), 1f))
        {
            for (int i = 0; i <= 2; i++)
            {
                float y = (Height - 1) * i / 2f;
                g.DrawLine(grid, 0, y, Width, y);
            }
        }

        float step = (Width - 1f) / Math.Max(1, MaxPoints - 1);

        for (int s = 0; s < data.Count; s++)
        {
            var vals = data[s].ToArray();
            if (vals.Length < 2) continue;

            Color col = s < Colors.Length ? Colors[s] : (s == 0 ? Theme.Accent : Theme.Muted);
            var pts = new PointF[vals.Length];
            for (int i = 0; i < vals.Length; i++)
            {
                float x = Width - 1 - (vals.Length - 1 - i) * step;
                float y = (float)((Height - 3) * (1 - Math.Clamp(vals[i] / Max, 0, 1))) + 1;
                pts[i] = new PointF(x, y);
            }

            if (s == 0)
            {
                var poly = new List<PointF>(pts) { new PointF(pts[^1].X, Height - 1), new PointF(pts[0].X, Height - 1) };
                using var fill = new SolidBrush(Color.FromArgb(40, col));
                g.FillPolygon(fill, poly.ToArray());
            }

            using var pen = new Pen(col, 2f) { LineJoin = LineJoin.Round };
            g.DrawLines(pen, pts);
        }
    }
}


/// <summary>Icons drawn with lines and shapes, so they show on every PC (no symbol font needed).</summary>
internal static class Icons
{
    public static void Draw(Graphics g, string name, RectangleF r, Color c)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float size = Math.Min(r.Width, r.Height);
        float u = size / 24f;
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        PointF P(float x, float y) => new(cx + (x - 12) * u, cy + (y - 12) * u);

        using var pen = new Pen(c, Math.Max(1.3f, u * 2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var br = new SolidBrush(c);

        switch (name)
        {
            case "close":
                g.DrawLine(pen, P(6, 6), P(18, 18));
                g.DrawLine(pen, P(18, 6), P(6, 18));
                break;

            case "min":
                g.DrawLine(pen, P(5, 17), P(19, 17));
                break;

            case "home":
                g.DrawLines(pen, new[] { P(3, 12), P(12, 4), P(21, 12) });
                g.DrawLines(pen, new[] { P(6, 10.5f), P(6, 20), P(18, 20), P(18, 10.5f) });
                g.DrawLines(pen, new[] { P(10, 20), P(10, 14), P(14, 14), P(14, 20) });
                break;

            case "hardware":
            {
                var a = P(7, 7); var b = P(17, 17);
                g.DrawRectangle(pen, a.X, a.Y, b.X - a.X, b.Y - a.Y);
                foreach (float t in new[] { 9.5f, 14.5f })
                {
                    g.DrawLine(pen, P(t, 3.5f), P(t, 7));
                    g.DrawLine(pen, P(t, 17), P(t, 20.5f));
                    g.DrawLine(pen, P(3.5f, t), P(7, t));
                    g.DrawLine(pen, P(17, t), P(20.5f, t));
                }
                break;
            }

            case "monitor":
            {
                var a = P(3, 6);
                g.DrawArc(pen, a.X, a.Y, 18 * u, 18 * u, 180, 180);
                g.DrawLine(pen, P(12, 15), P(17, 9));
                var d = P(12, 15);
                g.FillEllipse(br, d.X - 1.6f * u, d.Y - 1.6f * u, 3.2f * u, 3.2f * u);
                g.DrawLine(pen, P(4, 19), P(20, 19));
                break;
            }

            case "cleanup":
                g.FillPolygon(br, new[]
                {
                    P(12, 2.5f), P(14.2f, 9.8f), P(21.5f, 12), P(14.2f, 14.2f),
                    P(12, 21.5f), P(9.8f, 14.2f), P(2.5f, 12), P(9.8f, 9.8f)
                });
                break;

            case "startup":
            {
                var a = P(5.5f, 6.5f);
                g.DrawArc(pen, a.X, a.Y, 13 * u, 13 * u, -50, 280);
                g.DrawLine(pen, P(12, 3), P(12, 11));
                break;
            }

            case "report":
            {
                var a = P(6, 3); var b = P(18, 21);
                g.DrawRectangle(pen, a.X, a.Y, b.X - a.X, b.Y - a.Y);
                g.DrawLine(pen, P(9, 9), P(15, 9));
                g.DrawLine(pen, P(9, 13), P(15, 13));
                g.DrawLine(pen, P(9, 17), P(13, 17));
                break;
            }

            case "settings":
            {
                var a = P(5, 5);
                g.DrawEllipse(pen, a.X, a.Y, 14 * u, 14 * u);
                var b = P(9.5f, 9.5f);
                g.DrawEllipse(pen, b.X, b.Y, 5 * u, 5 * u);
                for (int i = 0; i < 8; i++)
                {
                    double ang = i * Math.PI / 4;
                    float cs = (float)Math.Cos(ang), sn = (float)Math.Sin(ang);
                    g.DrawLine(pen, P(12 + 7 * cs, 12 + 7 * sn), P(12 + 10 * cs, 12 + 10 * sn));
                }
                break;
            }

            case "bolt":
                g.FillPolygon(br, new[]
                {
                    P(13.5f, 2), P(5, 13.5f), P(11, 13.5f), P(9.5f, 22), P(19, 9.5f), P(13, 9.5f)
                });
                break;

            case "arrow":
                g.DrawLine(pen, P(4, 12), P(19, 12));
                g.DrawLines(pen, new[] { P(13, 6), P(19, 12), P(13, 18) });
                break;
        }

        g.SmoothingMode = old;
    }
}
