using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ZufeiBooster;

/// <summary>
/// Animated welcome screen: floating particles, an original robot mascot ("Zuzu"),
/// greeting text and a slide-to-start control.
/// </summary>
internal sealed class WelcomeScreen : Control
{
    public event EventHandler? Started;
    public event EventHandler? CloseRequested;
    public event MouseEventHandler? DragRequested;

    private readonly string userName;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 33 };
    private readonly Random rnd = new(7);
    private readonly List<(float x, float y, float speed, float size, int alpha)> dots = new();
    private readonly DateTime born = DateTime.UtcNow;
    private Bitmap? bg;

    // slider
    private float knob;          // 0..1
    private bool dragging;
    private bool launched;

    public WelcomeScreen(string userName)
    {
        this.userName = userName;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

        for (int i = 0; i < 46; i++)
            dots.Add(((float)rnd.NextDouble(), (float)rnd.NextDouble(),
                      0.015f + (float)rnd.NextDouble() * 0.05f,
                      1.5f + (float)rnd.NextDouble() * 3f,
                      40 + rnd.Next(120)));

        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Stop(); timer.Dispose(); bg?.Dispose(); }
        base.Dispose(disposing);
    }

    private float T => (float)(DateTime.UtcNow - born).TotalSeconds;

    private void Tick()
    {
        for (int i = 0; i < dots.Count; i++)
        {
            var d = dots[i];
            d.y -= d.speed * 0.033f;
            if (d.y < -0.02f) { d.y = 1.02f; d.x = (float)rnd.NextDouble(); }
            dots[i] = d;
        }

        if (!dragging && !launched && knob > 0)
            knob = Math.Max(0, knob - 0.06f);

        Invalidate();
    }

    // ---------- geometry ----------
    private RectangleF TrackRect()
    {
        float w = Width * 0.56f, h = Height * 0.105f;
        return new RectangleF((Width - w) / 2f, Height * 0.80f, w, h);
    }

    private RectangleF KnobRect()
    {
        var t = TrackRect();
        float k = t.Height - 10;
        float x = t.X + 5 + knob * (t.Width - k - 10);
        return new RectangleF(x, t.Y + 5, k, k);
    }

    private RectangleF CloseRect() => new(Width - 46, 8, 36, 28);

    // ---------- input ----------
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        if (CloseRect().Contains(e.Location)) { CloseRequested?.Invoke(this, EventArgs.Empty); return; }

        if (KnobRect().Contains(e.Location)) { dragging = true; Capture = true; return; }

        DragRequested?.Invoke(this, e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (dragging)
        {
            var t = TrackRect();
            float k = t.Height - 10;
            float span = t.Width - k - 10;
            knob = Math.Clamp((e.X - t.X - 5 - k / 2f) / span, 0, 1);

            if (knob >= 0.97f && !launched)
            {
                launched = true;
                dragging = false;
                Capture = false;
                Started?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        Cursor = KnobRect().Contains(e.Location) || CloseRect().Contains(e.Location) ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragging = false;
        Capture = false;
    }

    // ---------- painting ----------
    private void BuildBackground()
    {
        bg?.Dispose();
        bg = new Bitmap(Math.Max(1, Width), Math.Max(1, Height));
        using var g = Graphics.FromImage(bg);
        Theme.Prepare(g);

        using (var lg = new LinearGradientBrush(new Rectangle(0, 0, bg.Width, bg.Height), Theme.Bg, Theme.BgEnd, 90f))
            g.FillRectangle(lg, 0, 0, bg.Width, bg.Height);

        void Glow(float cx, float cy, float r, int alpha)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(cx - r, cy - r, r * 2, r * 2);
            using var pg = new PathGradientBrush(path)
            {
                CenterColor = Color.FromArgb(alpha, Theme.Accent),
                SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) }
            };
            g.FillEllipse(pg, cx - r, cy - r, r * 2, r * 2);
        }

        Glow(bg.Width * 0.5f, bg.Height * 0.38f, bg.Height * 0.55f, 70);
        Glow(bg.Width * 0.08f, bg.Height * 0.95f, bg.Height * 0.4f, 40);
        Glow(bg.Width * 0.95f, bg.Height * 0.08f, bg.Height * 0.35f, 35);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        BuildBackground();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (bg == null) BuildBackground();
        var g = e.Graphics;
        Theme.Prepare(g);
        g.DrawImageUnscaled(bg!, 0, 0);

        float k = Height / 560f;
        float t = T;
        float intro = Math.Clamp(t / 1.1f, 0, 1);
        float ease = 1 - (1 - intro) * (1 - intro);

        // particles
        foreach (var d in dots)
        {
            using var b = new SolidBrush(Color.FromArgb((int)(d.alpha * ease), Theme.AccentHi));
            float s = d.size * k;
            g.FillEllipse(b, d.x * Width, d.y * Height, s, s);
        }

        // close button
        Icons.Draw(g, "close", CloseRect(), Theme.Muted);

        // badge
        DrawBadge(g, k);

        // mascot
        float bob = (float)Math.Sin(t * 2.2) * 6 * k;
        float cy = Height * 0.34f + (1 - ease) * 50 * k + bob;
        DrawMascot(g, Width / 2f, cy, 150 * k, t);

        // text
        using (var tf = new Font("Segoe UI", 30f * k, FontStyle.Bold))
        using (var tb = new SolidBrush(Color.FromArgb((int)(255 * ease), Theme.Text)))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString("Welcome back", tf, tb, new RectangleF(0, Height * 0.545f, Width, 50 * k), sf);

        using (var nf = Theme.FontFor(userName, 17f * k, FontStyle.Bold))
        using (var nb = new SolidBrush(Color.FromArgb((int)(255 * ease), Theme.AccentHi)))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString(userName, nf, nb, new RectangleF(0, Height * 0.625f, Width, 36 * k), sf);

        using (var sf2 = new Font("Segoe UI", 10.5f * k))
        using (var sb = new SolidBrush(Color.FromArgb((int)(255 * ease), Theme.Muted)))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString("Smoother games, faster startup, cleaner system.", sf2, sb,
                new RectangleF(0, Height * 0.70f, Width, 26 * k), sf);

        DrawSlider(g, k);
    }

    private void DrawBadge(Graphics g, float k)
    {
        var r = new RectangleF(Width / 2f - 100 * k, 12 * k, 200 * k, 24 * k);
        using var path = Theme.Round(Rectangle.Round(r), (int)(10 * k));
        using var fill = new SolidBrush(Color.FromArgb(90, 24, 52, 46));
        using var pen = new Pen(Theme.Good);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
        using var f = new Font("Segoe UI", 8f * k, FontStyle.Bold);
        using var b = new SolidBrush(Theme.Good);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("FREE · NOT FOR SALE", f, b, r, sf);
    }

    private void DrawSlider(Graphics g, float k)
    {
        var tr = TrackRect();
        using (var path = Theme.Round(Rectangle.Round(tr), (int)(tr.Height / 2)))
        {
            using var fill = new SolidBrush(Color.FromArgb(130, Theme.Surface));
            using var pen = new Pen(Theme.Border);
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        // filled part
        var kr = KnobRect();
        if (knob > 0.01f)
        {
            var fr = new RectangleF(tr.X + 2, tr.Y + 2, kr.Right - tr.X - 2, tr.Height - 4);
            using var path = Theme.Round(Rectangle.Round(fr), (int)(fr.Height / 2));
            using var b = new SolidBrush(Color.FromArgb(90, Theme.Accent));
            g.FillPath(b, path);
        }

        float fade = 1 - Math.Clamp(knob * 2.2f, 0, 1);
        using (var f = new Font("Segoe UI", 12f * k, FontStyle.Bold))
        using (var b = new SolidBrush(Color.FromArgb((int)(190 * fade), Theme.Text)))
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString("Slide to start", f, b, new RectangleF(tr.X + tr.Height, tr.Y, tr.Width - tr.Height, tr.Height), sf);

        using (var glow = new SolidBrush(Color.FromArgb(60, Theme.Accent)))
            g.FillEllipse(glow, kr.X - 5, kr.Y - 5, kr.Width + 10, kr.Height + 10);
        using (var b = new SolidBrush(Theme.Accent))
            g.FillEllipse(b, kr);
        Icons.Draw(g, "arrow", new RectangleF(kr.X + kr.Width * 0.22f, kr.Y + kr.Height * 0.22f, kr.Width * 0.56f, kr.Height * 0.56f), Color.White);
    }

    /// <summary>Original robot character (not based on any existing character).</summary>
    private void DrawMascot(Graphics g, float cx, float cy, float size, float t)
    {
        float s = size;

        // shadow
        using (var sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
            g.FillEllipse(sh, cx - s * 0.45f, cy + s * 0.78f, s * 0.9f, s * 0.14f);

        Color body = Theme.Mix(Theme.Accent, Theme.Bg, 0.45f);
        Color bodyLight = Theme.Mix(Theme.AccentHi, Theme.Bg, 0.25f);

        // antenna
        using (var pen = new Pen(Theme.Border, Math.Max(2f, s * 0.025f)))
            g.DrawLine(pen, cx, cy - s * 0.40f, cx, cy - s * 0.58f);
        float pulse = 0.6f + 0.4f * (float)Math.Sin(t * 4);
        float br = s * 0.07f;
        using (var gl = new SolidBrush(Color.FromArgb((int)(90 * pulse), Theme.AccentHi)))
            g.FillEllipse(gl, cx - br * 2, cy - s * 0.58f - br * 2, br * 4, br * 4);
        using (var b = new SolidBrush(Theme.AccentHi))
            g.FillEllipse(b, cx - br, cy - s * 0.58f - br, br * 2, br * 2);

        // body
        var bodyRect = new RectangleF(cx - s * 0.30f, cy + s * 0.30f, s * 0.60f, s * 0.46f);
        using (var path = Theme.Round(Rectangle.Round(bodyRect), (int)(s * 0.12f)))
        using (var lg = new LinearGradientBrush(bodyRect, body, Theme.Mix(body, Theme.Bg, 0.5f), 90f))
        {
            g.FillPath(lg, path);
            using var pen = new Pen(Color.FromArgb(120, Theme.AccentHi), 1.5f);
            g.DrawPath(pen, path);
        }

        // chest light
        using (var b = new SolidBrush(Theme.AccentHi))
            g.FillEllipse(b, cx - s * 0.05f, cy + s * 0.46f, s * 0.10f, s * 0.10f);

        // arms
        float wave = (float)Math.Sin(t * 5) * s * 0.05f;
        using (var b = new SolidBrush(body))
        {
            g.FillEllipse(b, cx - s * 0.46f, cy + s * 0.38f, s * 0.14f, s * 0.26f);
            g.FillEllipse(b, cx + s * 0.32f, cy + s * 0.22f + wave, s * 0.14f, s * 0.26f);
        }

        // ears
        using (var b = new SolidBrush(Theme.Mix(body, Theme.Bg, 0.3f)))
        {
            g.FillRectangle(b, cx - s * 0.52f, cy - s * 0.08f, s * 0.07f, s * 0.24f);
            g.FillRectangle(b, cx + s * 0.45f, cy - s * 0.08f, s * 0.07f, s * 0.24f);
        }

        // head
        var head = new RectangleF(cx - s * 0.45f, cy - s * 0.42f, s * 0.90f, s * 0.78f);
        using (var path = Theme.Round(Rectangle.Round(head), (int)(s * 0.20f)))
        using (var lg = new LinearGradientBrush(head, bodyLight, body, 90f))
        {
            g.FillPath(lg, path);
            using var pen = new Pen(Color.FromArgb(160, Color.White), 2f);
            g.DrawPath(pen, path);
        }

        // visor
        var visor = new RectangleF(cx - s * 0.36f, cy - s * 0.30f, s * 0.72f, s * 0.46f);
        using (var path = Theme.Round(Rectangle.Round(visor), (int)(s * 0.14f)))
        using (var b = new SolidBrush(Color.FromArgb(235, Theme.Bg)))
            g.FillPath(b, path);

        // eyes (blink every ~3.5 s)
        float phase = t % 3.5f;
        float open = (phase > 3.3f && phase < 3.45f) ? 0.1f : 1f;
        float ew = s * 0.13f, eh = s * 0.17f * open;
        float ey = cy - s * 0.12f;
        using (var gl = new SolidBrush(Color.FromArgb(60, Theme.AccentHi)))
        {
            g.FillEllipse(gl, cx - s * 0.18f - ew * 0.8f, ey - eh * 0.8f, ew * 2.6f, eh * 2.6f);
            g.FillEllipse(gl, cx + s * 0.18f - ew * 0.8f, ey - eh * 0.8f, ew * 2.6f, eh * 2.6f);
        }
        using (var b = new SolidBrush(Theme.AccentHi))
        {
            g.FillEllipse(b, cx - s * 0.18f, ey, ew, eh);
            g.FillEllipse(b, cx + s * 0.18f, ey, ew, eh);
        }

        // smile
        using (var pen = new Pen(Theme.AccentHi, Math.Max(2f, s * 0.02f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(pen, cx - s * 0.10f, cy + s * 0.02f, s * 0.20f, s * 0.10f, 10, 160);
    }
}
