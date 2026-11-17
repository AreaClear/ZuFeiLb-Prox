using System.Diagnostics;
using ZufeiBooster.Overlay;

namespace ZufeiBooster;

public sealed partial class MainForm
{
    private readonly FakeFpsEngine fakeFps = new();
    private FpsOverlayForm? fakeOverlay;
    private Toggle? fakeToggle, fakeScaleToggle, fakeCoverToggle;
    private Label? fakeStatus;
    private readonly PillButton?[] fakeTargetButtons = new PillButton?[3];
    private static readonly int[] FakeTargets = { 90, 120, 144 };
    private int fakeTarget = 120;
    private bool fakeBusy;

    private void BuildFakeFps(Page p)
    {
        var card = C(p, 30, 434, 720, 96);
        L(card, "Fake FPS overlay", 14, 8, 300, 16, 8.5f, Theme.Text, true);

        fakeToggle = new Toggle { Location = new Point(14, 30), Size = new Size(44, 24) };
        fakeToggle.CheckedChanged += (_, _) => Safe(ToggleFakeFpsAsync);
        card.Controls.Add(fakeToggle);

        L(card, "Target", 70, 34, 46, 16, 8, Theme.Muted);
        for (int i = 0; i < FakeTargets.Length; i++)
        {
            int t = FakeTargets[i];
            var b = new PillButton { Text = t.ToString(), Location = new Point(118 + i * 52, 28), Size = new Size(46, 26) };
            b.Click += (_, _) => SetFakeTarget(t);
            fakeTargetButtons[i] = b;
            card.Controls.Add(b);
        }

        fakeScaleToggle = new Toggle { Location = new Point(288, 30), Size = new Size(44, 24) };
        fakeScaleToggle.CheckedChanged += (_, _) =>
        {
            settings.FakeFpsScale = fakeScaleToggle!.Checked;
            settings.Save();
            if (fakeFps.Mode != FakeFpsMode.Off) StartFakeFps();
        };
        card.Controls.Add(fakeScaleToggle);
        L(card, "Follow real FPS", 338, 34, 110, 16, 8, Theme.Muted);

        fakeCoverToggle = new Toggle { Location = new Point(456, 30), Size = new Size(44, 24) };
        fakeCoverToggle.CheckedChanged += (_, _) =>
        {
            settings.FakeFpsCover = fakeCoverToggle!.Checked;
            settings.Save();
            if (fakeFps.Mode != FakeFpsMode.Off) StartFakeFps();
        };
        card.Controls.Add(fakeCoverToggle);
        L(card, "Cover in-game counter", 506, 34, 150, 16, 8, Theme.Muted);

        fakeStatus = L(card, "Off", 664, 34, 44, 16, 8, Theme.Muted, true);

        L(card, "Rides on the emulator window as a click-through overlay. With PresentMon running the number follows your real FPS and stays believable.",
            14, 62, 700, 26, 7.5f, Theme.Muted);

        fakeTarget = Math.Clamp(settings.FakeFpsTarget, 60, 240);
        fakeScaleToggle.Checked = settings.FakeFpsScale;
        fakeCoverToggle.Checked = settings.FakeFpsCover;
        PaintFakeTargets();

        FormClosed += (_, _) => { fakeOverlay?.Close(); fakeFps.Stop(); };

        if (settings.FakeFps) fakeToggle.Checked = true;
    }

    private void PaintFakeTargets()
    {
        for (int i = 0; i < FakeTargets.Length; i++)
        {
            if (fakeTargetButtons[i] is not { } b) continue;
            b.Selected = FakeTargets[i] == fakeTarget;
            b.Invalidate();
        }
    }

    private void SetFakeTarget(int t)
    {
        fakeTarget = t;
        settings.FakeFpsTarget = t;
        settings.Save();
        PaintFakeTargets();
        if (fakeFps.Mode != FakeFpsMode.Off) StartFakeFps();
    }

    private Task ToggleFakeFpsAsync()
    {
        if (fakeBusy) return Task.CompletedTask;
        fakeBusy = true;
        try
        {
            if (fakeToggle?.Checked != true) { StopFakeFpsCore(); return Task.CompletedTask; }
            StartFakeFps();
            if (fakeFps.Mode == FakeFpsMode.Off) fakeToggle!.Checked = false;
        }
        finally { fakeBusy = false; }
        return Task.CompletedTask;
    }

    private void StartFakeFps()
    {
        using var proc = FindEmulatorProcess();
        if (proc is null) { if (fakeStatus is not null) fakeStatus.Text = "Open emulator"; return; }

        var hwnd = EmulatorWindows.FindMainWindow(proc);
        if (hwnd == IntPtr.Zero) { if (fakeStatus is not null) fakeStatus.Text = "No window"; return; }

        var mode = fps.Running && fakeScaleToggle?.Checked == true ? FakeFpsMode.Scale : FakeFpsMode.Synthetic;
        fakeFps.Start(mode, fakeTarget, () => (int)fps.Current().fps, 1.25);

        fakeOverlay?.Close();
        fakeOverlay = new FpsOverlayForm(hwnd, fakeFps, new Point(12, 10), 15f, fakeCoverToggle?.Checked == true);
        fakeOverlay.Show();

        settings.FakeFps = true;
        settings.Save();
        if (fakeStatus is not null) fakeStatus.Text = "On";
    }

    private void StopFakeFpsCore()
    {
        fakeOverlay?.Close();
        fakeOverlay?.Dispose();
        fakeOverlay = null;
        fakeFps.Stop();
        if (fakeStatus is not null) fakeStatus.Text = "Off";
        settings.FakeFps = false;
        settings.Save();
    }

    private void StopFakeFps()
    {
        if (fakeBusy) return;
        fakeBusy = true;
        try
        {
            if (fakeToggle is not null) fakeToggle.Checked = false;
            StopFakeFpsCore();
        }
        finally { fakeBusy = false; }
    }

    private void UpdateFakeFpsLabels()
    {
        if (fakeFps.Mode == FakeFpsMode.Off) return;
        int v = fakeFps.Current;
        if (v <= 0) return;

        monFps.Text = $"{v} FPS";
        monFrame.Text = "simulated";
        monFpsStatus.Text = fakeFps.Mode == FakeFpsMode.Scale
            ? "Fake FPS on · following PresentMon"
            : "Fake FPS on · simulated";
    }

    private static Process? FindEmulatorProcess()
    {
        foreach (string name in FpsTargets)
        {
            var list = Process.GetProcessesByName(name);
            if (list.Length == 0) continue;
            var keep = list[0];
            for (int i = 1; i < list.Length; i++) list[i].Dispose();
            return keep;
        }
        return null;
    }
}
