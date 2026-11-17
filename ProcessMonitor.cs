using System.Diagnostics;

namespace ZufeiBooster;

/// <summary>Real per-process CPU / RAM numbers, split into "emulator" and "everything else".</summary>
internal sealed class ProcessMonitor
{
    public sealed class Result
    {
        public double EmuCpu, OtherCpu, EmuRamMb, OtherRamMb;
        public int EmuCount;
        public string EmuNames = "";
        public List<(string name, double cpu)> Top = new();
    }

    private readonly Dictionary<int, TimeSpan> prev = new();
    private DateTime prevTime = DateTime.MinValue;

    public void Reset()
    {
        prev.Clear();
        prevTime = DateTime.MinValue;
    }

    public Result Take()
    {
        var res = new Result();
        var now = DateTime.UtcNow;
        double elapsed = (now - prevTime).TotalSeconds;
        bool valid = prevTime != DateTime.MinValue && elapsed > 0.2;

        var cur = new Dictionary<int, TimeSpan>();
        var perName = new Dictionary<string, double>();
        var emuNames = new HashSet<string>(BoostEngine.EmulatorNames, StringComparer.OrdinalIgnoreCase);
        var foundEmu = new SortedSet<string>();
        int self = Environment.ProcessId;

        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                try
                {
                    int id = p.Id;
                    if (id == 0) continue;

                    string name = p.ProcessName;
                    TimeSpan t = p.TotalProcessorTime;
                    cur[id] = t;

                    double cpu = 0;
                    if (valid && prev.TryGetValue(id, out var old))
                        cpu = Math.Max(0, (t - old).TotalSeconds) / elapsed / Environment.ProcessorCount * 100d;

                    double ram = p.WorkingSet64 / 1048576d;

                    if (emuNames.Contains(name))
                    {
                        res.EmuCpu += cpu;
                        res.EmuRamMb += ram;
                        res.EmuCount++;
                        foundEmu.Add(name);
                    }
                    else if (id != self)
                    {
                        res.OtherCpu += cpu;
                        res.OtherRamMb += ram;
                        perName[name] = perName.GetValueOrDefault(name) + cpu;
                    }
                }
                catch { /* protected system process: skipped */ }
            }
        }

        prev.Clear();
        foreach (var kv in cur) prev[kv.Key] = kv.Value;
        prevTime = now;

        res.EmuNames = string.Join(", ", foundEmu);
        res.Top = perName.OrderByDescending(k => k.Value).Take(5)
                         .Select(k => (k.Key, k.Value)).ToList();
        return res;
    }
}
