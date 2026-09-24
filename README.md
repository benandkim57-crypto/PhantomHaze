# PhantomHaze v1.1 beta - Phase 1 Proof of Concept

Smallest possible Windows 11 app proving that a window can stay visible to
the person at the PC while being excluded from supported capture
mechanisms, via `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`.

Built as a .NET 10 WinForms app rather than Electron/Tauri: it needs no
native Node addon or separate Rust binary, `SetWindowDisplayAffinity` is a
two-line P/Invoke call, and `dotnet build` alone produces a runnable .exe.
Worth revisiting for Phase 2 if you want a different UI framework, but for
proving the Win32 call this is the fastest path.

## Architecture

```
PhantomHaze
├── Program.cs         entry point
├── MainForm.cs         UI layer: fake content, toggle, status, log
└── NativeMethods.cs    native layer: SetWindowDisplayAffinity /
                         GetWindowDisplayAffinity, isolated from the UI
```

The native layer is deliberately the only file that calls into `user32.dll`.
Everything security-relevant (applying the affinity, reading it back,
translating Win32 error codes) lives in `NativeMethods.cs`; the form just
calls it and displays the result.

## Project structure

```
PhantomHaze/
├── PhantomHaze.csproj
├── Program.cs
├── MainForm.cs
├── NativeMethods.cs
└── README.md
```

## Build

Requires the .NET 10 SDK on Windows 11 (`dotnet --version` to check — it
should print 10.x). .NET 9 or 8 also work fine here; just change
`<TargetFramework>` in the .csproj to match (`net9.0-windows` /
`net8.0-windows`) if you'd rather use an SDK you already have installed.

```powershell
cd PhantomHaze
dotnet build
```

Or open `PhantomHaze.csproj` in Visual Studio 2022+ and build there.

## Run

```powershell
dotnet run
```

Protection is applied automatically on load (matches the "always enabled,
not opt-in" requirement). The window shows:

- The fake sensitive text in red.
- A status bar: green "PROTECTION ACTIVE" or salmon "PROTECTION DISABLED".
- A **Disable Protection (testing only)** button to toggle it off/on so you
  can compare captures with and without exclusion.
- A scrolling log of every `SetWindowDisplayAffinity` /
  `GetWindowDisplayAffinity` call, its result, and the Win32 error code on
  failure.

## Phase 1 test checklist

For each row: note what the **physical display** shows, what the
**capture** receives, whether the log still reports the window as
excluded, your Windows 11 build number, and the capture app's version.

| Test | Physical display | Capture result | Notes |
|---|---|---|---|
| Print Screen | | | |
| Snipping Tool | | | |
| Win + Shift + S | | | |
| Xbox Game Bar capture | | | |
| OBS — Display Capture source | | | |
| OBS — Window Capture source | | | |
| Zoom screen share | | | |
| Microsoft Teams screen share | | | |
| Multiple monitors | | | |
| Window resizing | | | |
| Minimize/restore | | | |
| Moving window between monitors | | | |

Milestone for Phase 1: the physical view stays normal in every row while
capture shows excluded/blank for the rows using supported mechanisms.

## Troubleshooting on genuine Windows 11

All Windows 11 releases ship as build 22000 or higher, well above the
19041 minimum — so if `SetWindowDisplayAffinity` is failing on a real
Windows 11 machine, it isn't actually the build-number caveat below. The
app now logs the *true* build number at startup (read via `RtlGetVersion`
in `ntdll.dll`, which — unlike `Environment.OSVersion` — isn't affected by
Windows' app-compatibility version-lying shim), plus a decoded explanation
of any failure code, not just the raw number.

Run it and check the log panel for two lines:

1. `Detected Windows build: ...` — confirms what OS build the app is
   actually seeing.
2. `SetWindowDisplayAffinity failed. Win32 error 87: ...` (if it fails) —
   error 87 (`ERROR_INVALID_PARAMETER`) on a genuine build-22000+ machine
   usually means the desktop isn't running hardware-accelerated
   composition — common inside a VM without GPU passthrough, over Remote
   Desktop, or with a Basic/Microsoft Basic Display driver rather than a
   real GPU driver. Check Device Manager → Display adapters for that.

If you're seeing a different error code or the app doesn't build at all,
paste the exact message and I'll fix it directly.

## Windows-version dependency

`WDA_EXCLUDEFROMCAPTURE` requires **Windows 10 version 2004 (build 19041)
or later**. On earlier builds, `SetWindowDisplayAffinity` will fail and the
log will show a non-zero Win32 error code rather than silently doing
nothing — that failure is expected and matches the spec's requirement to
log success/failure rather than assume it worked. There's no automatic
fallback to `WDA_MONITOR` in this proof of concept, since that mode also
blanks the physical monitor, which the spec explicitly rules out.

## Known limits (by design, not bugs)

Matches the spec's stated boundary: this protects content rendered inside
this window against capture paths that honor Windows display affinity. It
does **not** protect against a phone photographing the screen, capture
tools that ignore display affinity, or privileged/malicious software that
can bypass it. OBS/Zoom/Teams detection and alerts are Phase 3 — Phase 1
only proves the exclusion mechanism itself.
