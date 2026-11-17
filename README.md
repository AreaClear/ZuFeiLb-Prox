# Zufei Booster

Windows tool (WinForms, .NET 8): Home, Hardware (Boost system), Monitor, Cleanup, Startup, Report, Settings.

## Real FPS
Monitor page -> **Start FPS**. If PresentMon (free Intel tool) is missing, the app offers to download it
(official GitHub release). Windows requires administrator rights for frame tracing: Monitor -> **Admin**.

## GPU Boost (Hardware or Monitor page)
Sets Windows' per-app GPU preference to *High performance* for every emulator found, checks which GPU the emulator
really renders on, and shows what to change inside the emulator (renderer, FPS cap). Restart the emulator afterwards.

## Build the EXE on GitHub
1. Upload all files to a GitHub repo (keep `.github/workflows/build.yml`; delete any other workflow files).
2. **Actions** -> "Build Zufei Booster" -> **Run workflow**.
3. Download **ZufeiBooster-Windows** from *Artifacts*. Needs the .NET 8 Desktop Runtime.

## Build locally
Open the folder in Visual Studio 2022 and press F5, or run `dotnet run`.
