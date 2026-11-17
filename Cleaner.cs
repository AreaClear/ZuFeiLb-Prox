using System.Runtime.InteropServices;

namespace ZufeiBooster;

internal static class Cleaner
{
    private static readonly EnumerationOptions Opt = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public static string Downloads => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public static string[] TempDirs => new[]
    {
        Path.GetTempPath(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
    };

    public static string[] BrowserCacheDirs
    {
        get
        {
            string l = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new[]
            {
                Path.Combine(l, @"Google\Chrome\User Data\Default\Cache"),
                Path.Combine(l, @"Google\Chrome\User Data\Default\Code Cache"),
                Path.Combine(l, @"Microsoft\Edge\User Data\Default\Cache"),
                Path.Combine(l, @"Microsoft\Edge\User Data\Default\Code Cache"),
                Path.Combine(l, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"),
                Path.Combine(l, @"BraveSoftware\Brave-Browser\User Data\Default\Code Cache")
            };
        }
    }

    public static long DirSize(string dir)
    {
        long total = 0;
        try
        {
            if (!Directory.Exists(dir)) return 0;
            foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", Opt))
            {
                try { total += f.Length; } catch { }
            }
        }
        catch { }
        return total;
    }

    public static long DirsSize(IEnumerable<string> dirs) => dirs.Sum(d => DirSize(d));

    /// <summary>Deletes everything inside dir (not dir itself). Files in use are skipped.</summary>
    public static (long freed, int failed) DeleteContents(string dir)
    {
        long freed = 0;
        int failed = 0;

        try
        {
            if (!Directory.Exists(dir)) return (0, 0);
            var root = new DirectoryInfo(dir);

            foreach (var f in root.EnumerateFiles("*", Opt))
            {
                try
                {
                    long len = f.Length;
                    f.Attributes = FileAttributes.Normal;
                    f.Delete();
                    freed += len;
                }
                catch { failed++; }
            }

            foreach (var d in root.EnumerateDirectories("*", Opt)
                         .OrderByDescending(x => x.FullName.Length).ToList())
            {
                try
                {
                    if (!d.EnumerateFileSystemInfos().Any()) d.Delete();
                }
                catch { }
            }
        }
        catch { }

        return (freed, failed);
    }

    public static (long freed, int failed) DeleteContents(IEnumerable<string> dirs)
    {
        long freed = 0; int failed = 0;
        foreach (var d in dirs)
        {
            var r = DeleteContents(d);
            freed += r.freed; failed += r.failed;
        }
        return (freed, failed);
    }

    // ---------- large files (>= 500 MB in Downloads) ----------
    public const long LargeLimit = 500L * 1024 * 1024;

    public static List<FileInfo> LargeFiles()
    {
        var list = new List<FileInfo>();
        try
        {
            if (!Directory.Exists(Downloads)) return list;
            foreach (var f in new DirectoryInfo(Downloads).EnumerateFiles("*", Opt))
            {
                try { if (f.Length >= LargeLimit) list.Add(f); } catch { }
            }
        }
        catch { }
        return list;
    }

    public static long LargeFilesSize() => LargeFiles().Sum(f => f.Length);

    public static (long freed, int failed) DeleteLargeFiles()
    {
        long freed = 0; int failed = 0;
        foreach (var f in LargeFiles())
        {
            try
            {
                long len = f.Length;
                f.Attributes = FileAttributes.Normal;
                f.Delete();
                freed += len;
            }
            catch { failed++; }
        }
        return (freed, failed);
    }

    // ---------- recycle bin ----------
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? root, ref SHQUERYRBINFO info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? root, uint flags);

    public static long RecycleBinSize()
    {
        try
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
        }
        catch { return 0; }
    }

    public static (long freed, int failed) EmptyRecycleBin()
    {
        long before = RecycleBinSize();
        try
        {
            // NOCONFIRMATION | NOPROGRESSUI | NOSOUND
            int hr = SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4);
            return hr == 0 ? (before, 0) : (0, 1);
        }
        catch { return (0, 1); }
    }
}
