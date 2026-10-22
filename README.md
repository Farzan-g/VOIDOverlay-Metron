# VOIDOverlay Metron

VOIDOverlay Metron is a compact Windows performance overlay for gaming and desktop use. It displays CPU utilization, a Windows GPU-engine activity reading, physical RAM usage, and an FPS slot that remains unavailable unless a real frame-measurement provider is added.

The project is an independent VOID Overlay sibling. It does not depend on or modify VOIDOverlay Termo and does not monitor temperatures.

## Features

- One compact, transparent, always-on-top metrics row with click-through behavior.
- CPU utilization from Windows `GetSystemTimes`.
- Physical memory from `GlobalMemoryStatusEx`, displayed in binary GiB with a one-decimal presentation.
- GPU engine activity from the documented Windows GPU Engine performance counter through PDH.
- Six position anchors, offsets, monitor selection, font size, opacity, metric visibility, and refresh interval settings.
- User-level startup registration and a tray menu for overlay, settings, lock, About, and exit controls.
- User-writable settings and diagnostic logs under `%LOCALAPPDATA%\VOIDOverlay\Metron`.
- No telemetry, analytics, network calls, drivers, game injection, or administrator requirement.

## Platform and installation

The application targets Windows 10 and Windows 11 x64 and .NET 10 WPF. The publish script produces a framework-dependent, single-file x64 build. The .NET 10 Desktop Runtime (x64) must be installed on the target PC. A self-contained build was not selected because downloading the Windows Desktop runtime pack failed in the current build environment.

To build a local package:

1. Install the .NET 10 SDK on Windows.
2. From the repository root, run `.\scripts\publish.ps1`.
3. Optionally run `.\scripts\package.ps1` to create `artifacts\VOIDOverlay-Metron-win-x64.zip`.
4. Extract the ZIP and launch `VOIDOverlay.Metron.exe`.

No release version, GitHub release, or download URL is configured yet, so the ZIP name is intentionally unversioned.

## First launch and tray

Metron starts with the overlay visible by default. The tray icon remains available when the overlay is hidden. Its menu provides:

- **Show/Hide Overlay**
- **Settings**
- **Lock Position**
- **Start with Windows**
- **About**
- **Exit**

Closing the settings window hides it rather than exiting the application. Use **Exit** in the tray menu to stop monitoring and shut down cleanly. A second launch asks the existing process to reveal its overlay instead of starting another instance.

The normal overlay is non-activating and click-through. Use Settings or the tray to change its position; no click or drag on the overlay itself is required. Position changes are clamped to the selected monitor's working area. A disconnected monitor falls back to the primary monitor.

## Readings and limitations

### CPU

The CPU percentage is calculated from the change in idle and total kernel/user time returned by `GetSystemTimes`. The first sample establishes a baseline and is shown as unavailable; subsequent readings represent the interval between samples. It is system-wide and is not CPU temperature or clock speed.

### GPU

Metron reads `\GPU Engine(*)\Utilization Percentage` using PDH's English counter API. It reports the maximum valid percentage among the reported GPU engine instances across installed adapters. This is the **busiest individual engine**, not a mathematically combined adapter-wide total; simultaneous engines are intentionally not summed because that can exceed 100% and misrepresent capacity.

The metric may be unavailable if the Windows GPU performance counters are absent, disabled, or not exposed by the driver. The overlay does not select an NVIDIA GPU by assumption. It currently does not offer per-adapter selection.

### RAM

RAM is physical memory used (`total physical - available physical`) over total physical memory. Values use binary GiB (1 GiB = 1024³ bytes), formatted with one decimal place. The underlying percentage is calculated from the same byte counts.

### FPS

FPS is always shown as `—` in this initial release. Windows does not expose a reliable universal in-game FPS value for arbitrary games through a safe general-purpose API. Metron does not count desktop refreshes or its own update loop, inject code into games, inspect game memory, or bypass anti-cheat systems.

`IFpsProvider` is an extension point, but no external provider or integration is shipped. A future provider must supply actual measured frames and define its compatibility and setup requirements before the displayed value can become active.

### Overlay compatibility

The overlay is click-through, borderless, transparent, and topmost when enabled. Windows, a game, protected content, exclusive fullscreen, or anti-cheat policy may prevent overlays from appearing or may limit performance counters. Metron does not attempt to circumvent such restrictions. Borderless-windowed or windowed mode is generally the most compatible.

## Settings and startup

Settings are persisted as JSON under the current user's Local AppData directory, not beside the executable. Invalid values are reset to safe defaults; malformed JSON is logged and defaults are used. Diagnostic warnings and errors go to a per-day file in the same application data directory.

The startup toggle writes only the current user's `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry. It does not require elevation, alter machine-wide settings, or make startup mandatory. Removing the app does not uninstall that entry automatically; turn it off in the tray/settings before deleting the executable.

Settings apply immediately. Supported update intervals are 250, 500, 1000, and 2000 ms. Ordinary unavailable metrics display `—`, which is different from a measured zero.

## Troubleshooting

- **GPU shows `—`:** Check that Windows GPU engine performance counters are present and available to your GPU driver. CPU and RAM readings continue independently.
- **FPS shows `—`:** This is expected until a supported real-frame provider is implemented; it is not a zero-FPS measurement.
- **Overlay is missing in a game:** Try borderless-windowed mode. Exclusive fullscreen, protected content, game settings, or anti-cheat policy can block overlays.
- **Startup does not work:** Verify that Start with Windows is enabled in the tray and that the executable remains at the same path. The setting is per-user.
- **Position looks wrong after display changes:** Reopen Settings and select the current monitor. Metron recalculates placement after display-change notifications and falls back to the primary display if the selected monitor is disconnected.
- **Settings appear reset:** Check that the current user can write to `%LOCALAPPDATA%\VOIDOverlay\Metron`; the log directory may contain an explanation.

## Developer workflow

Requirements: Windows, .NET 10 SDK, and network access for restoring the test packages on a fresh machine.

`assets\Metron.ico` is regenerated from the unchanged `assets\Metron.png` source by `.\scripts\convert-icon.ps1` before the build and publish scripts run.

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\publish.ps1
.\scripts\package.ps1
```

The app itself has no third-party runtime package references. The test project uses xUnit, Microsoft.NET.Test.Sdk, and coverlet.collector. Hardware-specific GPU integration was not exercised by the unit tests; the tests cover portable logic such as sampling calculations, settings validation, positioning, and FPS state expiration.

## License

Metron is licensed under the MIT License; see [LICENSE](./LICENSE). Test dependencies retain their respective upstream licenses.

## Contributing and security

See [CONTRIBUTING.md](./CONTRIBUTING.md) for development expectations and [SECURITY.md](./SECURITY.md) for vulnerability reporting guidance. No verified GitHub organization or repository URL has been supplied, so none is advertised here.
