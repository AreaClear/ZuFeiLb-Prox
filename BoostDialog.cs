namespace ZufeiBooster;

/// <summary>Small panel with a progress ring that runs the boost steps and shows real results.</summary>
internal sealed class BoostDialog : Form
{
    private readonly string title;
    private readonly List<BoostStep> steps;

    private readonly ProgressRing ring = new();
    private readonly Label stepLabel;
    private readonly Label results;
    private readonly PillButton closeBtn;
    private readonly System.Windows.Forms.Timer anim = new() { Interval = 16 };
    private float shown, target;
    private bool finished;

    public BoostDialog(string title, List<BoostStep> steps)
    {
        this.title = title;
        this.steps = steps;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(470, 470);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        ShowInTaskbar = false;
        DoubleBuffered = true;

        var titleLabel = new Label
        {
            Text = title, Location = new Point(0, 16), Size = new Size(470, 30),
            Font = Theme.F(15, FontStyle.Bold), ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
        };
        Controls.Add(titleLabel);

        ring.Location = new Point(160, 58);
        Controls.Add(ring);

        stepLabel = new Label
        {
            Text = "Starting...", Location = new Point(20, 214), Size = new Size(430, 24),
            Font = Theme.F(10.5f, FontStyle.Bold), ForeColor = Theme.AccentHi,
            TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
        };
        Controls.Add(stepLabel);

        var card = new Card { Location = new Point(20, 248), Size = new Size(430, 148) };
        Controls.Add(card);
        results = new Label
        {
            Location = new Point(12, 8), Size = new Size(406, 132),
            Font = Theme.F(8.5f), ForeColor = Theme.Muted, BackColor = Color.Transparent
        };
        card.Controls.Add(results);

        closeBtn = new PillButton
        {
            Text = "Please wait...", Location = new Point(20, 412), Size = new Size(430, 40),
            Primary = true, Enabled = false
        };
        closeBtn.Click += (_, _) => Close();
        Controls.Add(closeBtn);

        UiScale.ApplyChildren(this, null);
        ClientSize = new Size(UiScale.S(470), UiScale.S(470));

        anim.Tick += (_, _) =>
        {
            if (shown < target) shown = Math.Min(target, shown + Math.Max(0.4f, (target - shown) * 0.1f));
            ring.Value = shown;
        };
        anim.Start();

        Shown += async (_, _) =>
        {
            try { await RunAsync(); }
            catch (Exception ex)
            {
                stepLabel.Text = "Something went wrong";
                results.Text = ex.Message;
                finished = true;
                closeBtn.Text = "Close";
                closeBtn.Enabled = true;
            }
        };
    }

    private async Task RunAsync()
    {
        var lines = new List<string>();
        int ok = 0;

        for (int i = 0; i < steps.Count; i++)
        {
            stepLabel.Text = steps[i].Title + "...";
            target = 100f * i / steps.Count;
            await Task.Delay(350);

            (bool ok, string detail) r;
            try { r = await Task.Run(steps[i].Run); }
            catch (Exception ex) { r = (false, ex.Message); }

            if (r.ok) ok++;
            lines.Add($"{(r.ok ? "OK" : "!!")}   {steps[i].Title}: {r.detail}");
            results.Text = string.Join("\n", lines.TakeLast(7));

            target = 100f * (i + 1) / steps.Count;
            await Task.Delay(250);
        }

        target = 100f;
        for (int w = 0; w < 100 && shown < 99.5f; w++) await Task.Delay(30);

        ring.Value = 100;
        ring.Done = ok == steps.Count;
        stepLabel.ForeColor = ok == steps.Count ? Theme.Good : Theme.AccentHi;
        stepLabel.Text = ok == steps.Count
            ? $"{title} complete"
            : $"{title} finished: {ok} of {steps.Count} steps applied";

        finished = true;
        closeBtn.Text = "Done";
        closeBtn.Enabled = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!finished) e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        anim.Stop();
        anim.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
