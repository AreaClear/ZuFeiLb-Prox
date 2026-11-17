using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ZufeiBooster;

public sealed partial class MainForm : Form
{
    private const string AppName = "Zufei Booster";
    private const string Creator = "祖霏宝贝 ツ";
    private const string Version = "v1.0.0";

    // page indexes
    private const int PgHome = 0, PgHardware = 1, PgCleanup = 2, PgSettings = 3, PgMonitor = 4, PgReport = 5, PgStartup = 6;
    private const int PageCount = 7;

    private readonly AppSettings settings = AppSettings.Load();
    private readonly System.Windows.Forms.Timer monitor = new() { Interval = 1500 };
    private readonly System.Windows.Forms.Timer autoTimer = new() { Interval = 3000 };
    private readonly BoostEngine engine;
    private readonly ProcessMonitor procMonitor = new();
    private readonly FpsMonitor fps = new();
    private readonly ToolTip tip = new();

    private readonly Panel[] pages = new Panel[PageCount];
    private readonly PillButton[] navButtons = new PillButton[PageCount];
    private Card navCard = null!;
    private int currentPage;

    private WelcomeScreen? welcome;
    private NotifyIcon? tray;
    private Bitmap? bgCache;
    private bool autoBoosted, autoBusy;

    // hardware info
    private string gpuName = "", gpuVram = "", gpuExtra = "", cpuName = "", osName = "", osBuild = "", board = "";
    private bool dedicatedGpu;

    // cleanup page state
    private sealed class CleanItem
    {
        public string Title = "", Sub = "";
        public Func<long> Measure = () => 0;
        public Func<(long freed, int failed)> Clean = () => (0, 0);
        public bool NeedsConfirm;
        public bool DefaultOn;
        public RoundCheck Check = null!;
        public Label SizeLabel = null!;
        public long Size;
    }

    private readonly List<CleanItem> items = new();
    private Label tileCache = null!, tileLast = null!, tileSelected = null!, activityText = null!;
    private PillButton runCleanup = null!;
    private readonly List<(string msg, DateTime time)> log = new();
    private bool busy;

    public MainForm()
    {
        Theme.Apply(settings.ThemeName);
        UiScale.Factor = UiScale.Clamp(settings.UiScale);
        settings.UiScale = UiScale.Factor;
        engine = new BoostEngine(settings);

        Text = AppName;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.F(9.5f);
        DoubleBuffered = true;
        ClientSize = ScaledBase();

        SetupTray();
        BuildAll();
        Navigate(PgHome);

        monitor.Tick += (_, _) => UpdateLive();
        autoTimer.Tick += async (_, _) => await AutoTickAsync();

        Shown += (_, _) =>
        {
            LoadHardware();
            UpdateLive();
            monitor.Start();
            autoTimer.Start();
        };

        if (settings.ShowWelcome) ShowWelcome();
    }

    private static Size ScaledBase() => new(UiScale.S(780), UiScale.S(560));

    // keeps the window smooth when many transparent controls repaint
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
            return cp;
        }
    }

    // =====================================================================
    //  Build / rebuild everything (used for first start, theme and size change)
    // =====================================================================

    private void BuildAll()
    {
        SuspendLayout();

        foreach (var c in Controls.Cast<Control>().ToList())
        {
            if (c == welcome) continue;
            Controls.Remove(c);
            c.Dispose();
        }

        items.Clear();
        BackColor = Theme.Bg;
        ClientSize = ScaledBase();

        BuildChrome();
        BuildHome();
        BuildHardware();
        BuildCleanup();
        BuildMonitor();
        BuildReport();
        BuildStartup();
        BuildSettings();
        BuildNav();

        UiScale.ApplyChildren(this, welcome);
        RebuildBackground();
        LayoutNav();

        if (welcome != null)
        {
            welcome.Bounds = ClientRectangle;
            welcome.BringToFront();
        }

        ResumeLayout();
        Invalidate(true);
    }

    private void ApplyTheme(string name)
    {
        settings.ThemeName = name;
        settings.Save();
        Theme.Apply(name);
        Rebuild(PgSettings);
    }

    private void ApplyScale(float factor)
    {
        settings.UiScale = factor;
        settings.Save();
        UiScale.Factor = factor;

        var center = new Point(Left + Width / 2, Top + Height / 2);
        Rebuild(PgSettings);
        Location = new Point(center.X - Width / 2, center.Y - Height / 2);
    }

    private void Rebuild(int page)
    {
        BuildAll();
        LoadHardware();
        UpdateLive();
        Navigate(page);
    }

    // =====================================================================
    //  Background (gradient + soft glows, cached)
    // =====================================================================

    private void RebuildBackground()
    {
        bgCache?.Dispose();
        int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
        bgCache = new Bitmap(w, h);

        using var g = Graphics.FromImage(bgCache);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Theme.Bg, Theme.BgEnd, 70f))
            g.FillRectangle(lg, 0, 0, w, h);

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

        Glow(w * 0.12f, h * 0.10f, h * 0.55f, 45);
        Glow(w * 0.92f, h * 0.92f, h * 0.50f, 38);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (bgCache == null) RebuildBackground();
        e.Graphics.DrawImageUnscaled(bgCache!, 0, 0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    // =====================================================================
    //  Helpers used by every page (base 100% coordinates)
    // =====================================================================

    private static Label L(Control parent, string text, int x, int y, int w, int h,
        float size = 9.5f, Color? color = null, bool bold = false,
        ContentAlignment align = ContentAlignment.TopLeft)
    {
        var l = new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            ForeColor = color ?? Theme.Text,
            BackColor = Color.Transparent,
            Font = Theme.FontFor(text, size, bold ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = align,
            AutoEllipsis = true
        };
        parent.Controls.Add(l);
        return l;
    }

    private static Card C(Control parent, int x, int y, int w, int h)
    {
        var c = new Card { Location = new Point(x, y), Size = new Size(w, h) };
        parent.Controls.Add(c);
        return c;
    }

    private static PillButton B(Control parent, string text, int x, int y, int w, int h,
        Action onClick, bool primary = false, bool danger = false)
    {
        var b = new PillButton
        {
            Text = text, Location = new Point(x, y), Size = new Size(w, h),
            Primary = primary, Danger = danger
        };
        b.Click += (_, _) =>
        {
            try { onClick(); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        parent.Controls.Add(b);
        return b;
    }

    private static ThinBar Bar(Control parent, int x, int y, int w)
    {
        var b = new ThinBar { Location = new Point(x, y), Size = new Size(w, 6) };
        parent.Controls.Add(b);
        return b;
    }

    private Page NewPage(int index, string title, string subtitle)
    {
        var p = new Page { Location = new Point(0, 40), Size = new Size(780, 440), Visible = false };
        Controls.Add(p);
        pages[index] = p;

        L(p, title, 0, 4, 780, 34, 17, Theme.Text, true, ContentAlignment.MiddleCenter);
        L(p, subtitle, 0, 38, 780, 22, 9, Theme.Muted, false, ContentAlignment.MiddleCenter);
        return p;
    }

    private async void Safe(Func<Task> work)
    {
        try { await work(); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // =====================================================================
    //  Title bar + navigation
    // =====================================================================

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, int w, int l);

    private void DragWindow(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 2, 0); // WM_NCLBUTTONDOWN, HTCAPTION
    }

    private void BuildChrome()
    {
        var bar = new Page { Location = new Point(0, 0), Size = new Size(780, 40) };
        Controls.Add(bar);
        bar.MouseDown += DragWindow;

        var badge = new Card
        {
            Location = new Point(12, 9), Size = new Size(110, 22),
            Fill = Color.FromArgb(24, 52, 46), Stroke = Theme.Good, Radius = 8
        };
        bar.Controls.Add(badge);
        var bl = L(badge, "FREE · NOT FOR SALE", 0, 0, 110, 22, 7, Theme.Good, true, ContentAlignment.MiddleCenter);
        bl.MouseDown += DragWindow;

        var title = L(bar, AppName, 0, 8, 780, 24, 9.5f, Theme.Muted, true, ContentAlignment.MiddleCenter);
        title.MouseDown += DragWindow;

        var min = B(bar, "", 696, 5, 36, 30, () => WindowState = FormWindowState.Minimized);
        min.Icon = "min";
        var close = B(bar, "", 736, 5, 36, 30, Close);
        close.Icon = "close";
        close.Danger = false;
    }

    private static readonly string[] NavIcons = { "home", "hardware", "cleanup", "settings", "monitor", "report", "startup" };
    private static readonly string[] NavNames = { "Home", "Hardware", "Cleanup", "Settings", "Monitor", "Report", "Startup" };
    private static readonly int[] NavOrder = { PgHome, PgHardware, PgMonitor, PgCleanup, PgStartup, PgReport, PgSettings };

    private void BuildNav()
    {
        navCard = C(this, 100, 496, 580, 46);
        navCard.Radius = 23;
        navCard.Glow = true;

        foreach (int idx in NavOrder)
        {
            int page = idx;
            var b = new PillButton
            {
                Icon = NavIcons[idx],
                Font = Theme.F(9f, FontStyle.Bold),
                Size = new Size(44, 34)
            };
            b.Click += (_, _) => Navigate(page);
            tip.SetToolTip(b, NavNames[idx]);
            navCard.Controls.Add(b);
            navButtons[idx] = b;
        }
    }

    /// <summary>Selected tab expands with its name, others show only the icon. Works at any size.</summary>
    private void LayoutNav()
    {
        if (navCard == null || navButtons[0] == null) return;

        int small = UiScale.S(44), wide = UiScale.S(120), gap = UiScale.S(6), pad = UiScale.S(8);
        int h = UiScale.S(34);
        int x = pad;

        foreach (int idx in NavOrder)
        {
            bool sel = idx == currentPage;
            var b = navButtons[idx];
            b.Text = sel ? NavNames[idx] : "";
            b.Selected = sel;
            int w = sel ? wide : small;
            b.SetBounds(x, UiScale.S(6), w, h);
            x += w + gap;
        }

        int total = x - gap + pad;
        navCard.SetBounds((ClientSize.Width - total) / 2, UiScale.S(496), total, UiScale.S(46));
    }

    private void Navigate(int index)
    {
        currentPage = index;
        if (index == PgMonitor) procMonitor.Reset();

        for (int i = 0; i < pages.Length; i++)
            pages[i].Visible = i == index;

        LayoutNav();
        Invalidate(true);

        if (index == PgCleanup && settings.ScanOnOpening && !busy) Safe(ScanAsync);
        if (index == PgReport) RefreshReport();
        if (index == PgStartup) RefreshStartupList();
    }

    // =====================================================================
    //  Welcome screen
    // =====================================================================

    private void ShowWelcome()
    {
        welcome = new WelcomeScreen(Creator) { Bounds = ClientRectangle };
        welcome.DragRequested += (_, e) => DragWindow(null, e);
        welcome.CloseRequested += (_, _) => Close();
        welcome.Started += (_, _) => BeginInvoke(new Action(() =>
        {
            if (welcome == null) return;
            Controls.Remove(welcome);
            welcome.Dispose();
            welcome = null;
        }));

        Controls.Add(welcome);
        welcome.BringToFront();
    }

    // =====================================================================
    //  Tray icon + auto boost
    // =====================================================================

    private void SetupTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => RestoreFromTray());
        menu.Items.Add("Boost now", null, (_, _) =>
        {
            RestoreFromTray();
            Navigate(PgHardware);
            Safe(BoostSystemAsync);
        });
        menu.Items.Add("Exit", null, (_, _) => Close());

        tray = new NotifyIcon { Icon = MakeIcon(), Text = AppName, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => RestoreFromTray();
    }

    private static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(Theme.Accent);
            g.FillEllipse(b, 1, 1, 30, 30);
            using var f = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Z", f, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized && settings.MinimizeToTray && tray != null)
            Hide();
    }

    private void Notify(string message)
    {
        try { tray?.ShowBalloonTip(3000, AppName, message, ToolTipIcon.Info); } catch { }
    }

    private async Task AutoTickAsync()
    {
        if (!settings.AutoBoost || autoBusy || IsDisposed) return;
        autoBusy = true;

        try
        {
            bool running = BoostEngine.AnyEmulatorRunning();

            if (running && !autoBoosted)
            {
                autoBoosted = true;
                var steps = engine.BuildSteps(HardwareProfile(), dedicatedGpu);
                await Task.Run(() =>
                {
                    foreach (var st in steps)
                    {
                        try { st.Run(); } catch { }
                    }
                });
                settings.AddLog("Auto boost applied (emulator started)");
                settings.Save();
                Notify("Emulator detected - boost applied.");
            }
            else if (!running && autoBoosted)
            {
                autoBoosted = false;
                await Task.Run(engine.RestoreAll);
                Notify("Emulator closed - settings restored.");
            }
        }
        catch { }
        finally { autoBusy = false; }
    }

    // =====================================================================
    //  Live updates
    // =====================================================================

    private bool gpuBusy;

    private void UpdateLive()
    {
        if (IsDisposed || !IsHandleCreated || !Visible) return;

        try
        {
            int cpu = (int)Math.Round(SysInfo.CpuUsage());
            var mem = SysInfo.Memory();
            int ram = (int)Math.Clamp(mem.load, 0, 100);
            var drive = SysInfo.SystemDrive();

            // home
            homeCpuRing.Animate(cpu);
            homeRamRing.Animate(ram);
            homeRamText.Text = $"RAM · {mem.available / 1073741824d:0.0} GB free";
            homeSpark.Add(0, cpu);
            homeSpark.Add(1, ram);
            if (currentPage == PgHome) homeSpark.Redraw();

            // hardware
            hwCpuLoad.Text = $"{cpu}%";
            hwCpuBar.Value = cpu;
            hwRamBar.Value = ram;
            hwRamUse.Text = $"{ram}% in use";

            if (drive.total > 0)
            {
                hwStorageBar.Value = (int)(100d * (drive.total - drive.free) / drive.total);
                hwStorageFree.Text = $"{drive.free / 1073741824d:0.0} GB free";
            }

            if (currentPage == PgMonitor) UpdateMonitor();
        }
        catch { }
    }

    // =====================================================================

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        monitor.Stop();
        autoTimer.Stop();
        fps.Dispose();

        if (!settings.KeepBoostOnClose) engine.RestoreAll();
        settings.Save();

        if (tray != null)
        {
            tray.Visible = false;
            tray.Dispose();
        }

        base.OnFormClosed(e);
    }
}
