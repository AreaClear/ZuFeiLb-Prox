using System.Diagnostics;

namespace ZufeiBooster.Overlay;

public enum FakeFpsMode { Off, Synthetic, Scale }

/// <summary>Realistic FPS value generator: ramp-in, jitter, micro-stutter, clamp.</summary>
public sealed class FakeFpsEngine
{
    private readonly Random rng = new();
    private readonly Stopwatch clock = new();
    private double value;
    private int target = 120;
    private double multiplier = 1.25;
    private Func<int>? realFps;

    public FakeFpsMode Mode { get; private set; } = FakeFpsMode.Off;
    public int Current => (int)Math.Round(value);

    public void Start(FakeFpsMode mode, int goal, Func<int>? realProvider = null, double mult = 1.25)
    {
        target = Math.Clamp(goal, 30, 480);
        multiplier = Math.Clamp(mult, 1.0, 3.0);
        realFps = realProvider;
        Mode = mode;
        value = target * 0.55;
        clock.Restart();
    }

    public void Stop()
    {
        Mode = FakeFpsMode.Off;
        realFps = null;
        value = 0;
        clock.Reset();
    }

    /// <summary>Only the overlay tick may call this (single driver).</summary>
    public int Next()
    {
        if (Mode == FakeFpsMode.Off) return 0;

        double goal = target;
        if (Mode == FakeFpsMode.Scale && realFps is not null)
        {
            int real = realFps();
            goal = real <= 0 ? target : Math.Clamp(real * multiplier, target - 12, target + 4);
        }

        double ramp = Math.Min(1.0, clock.Elapsed.TotalSeconds / 1.5);
        value += (goal - value) * (0.18 + rng.NextDouble() * 0.34) * ramp;

        if (rng.NextDouble() < 0.09) value -= 2 + target * 0.015 + rng.NextDouble() * 3;

        double floor = goal - Math.Max(6.0, goal * 0.05);
        value = Math.Clamp(value, floor, goal + 2);
        return Current;    }
}
