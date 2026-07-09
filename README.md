# Internet Speed Monitor

A lightweight, always-on-top floating widget for Windows 11 that shows your live network speeds — with a full dashboard, daily usage history, ping monitoring, and data-cap alerts.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square) ![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4?style=flat-square) ![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

---

## Features

### Floating widget
- **Live speed display** — download (↓) and upload (↑), refreshed every 1 / 2 / 5 s (configurable)
- **60-second sparkline graph** under the numbers, with per-direction show/hide
- **Layouts** — vertical (stacked), horizontal (side by side), or compact single-row; show ↓ and ↑ lines independently
- **Fully styleable** — background and text transparency controlled separately, custom download/upload colors, font size 8–28 pt
- **Units** — Binary (KiB/MiB), Decimal (KB/MB), or bits (Mbps) to compare against your ISP plan
- **Window behavior** — always on top, snap to screen edges, lock position, click-through mode
- **Never intrudes** — hidden from the taskbar and Alt+Tab, never steals focus, Windows 11 rounded corners, position remembered across restarts

### Dashboard (right-click → Dashboard…)
- Live speeds and **color-coded ping latency** side by side
- **Data usage** — this session, today, and this month
- **Monthly data cap** — progress bar plus tray notifications at 80% and 100%
- **30-day usage chart** with per-day hover tooltips
- Network info — local IP, Wi-Fi SSID, active adapter

### Usage history (right-click → Usage History…)
- Day-by-day table of download / upload / total, kept for **120 days**
- Period totals and daily average; today's row updates live
- **Export to CSV**

### Monitoring
- **Per-adapter selection** — monitor all adapters or a single one
- **Ping monitor** — configurable host (default 8.8.8.8), connection-lost tray alert
- **System tray icon** — live speed + session tooltip, double-click to show/hide the widget

Settings persist to `%APPDATA%\InternetSpeedApp\settings.json`; usage history to `usage.json` alongside it.

---

## Screenshots

```
Vertical (default)       Horizontal                          Compact
┌──────────────────┐     ┌───────────────────────────────┐   ┌─────────────────────────────┐
│ ↓  5.2 MiB/s    │     │ ↓  5.2 MiB/s   ↑  1.1 KiB/s │   │ ↓ 5.2 MiB/s  ↑ 1.1 KiB/s │
│ ↑  1.1 KiB/s    │     └───────────────────────────────┘   └─────────────────────────────┘
│ ▁▂▄▆▄▂▁▂▄▆█▆▄▂▁ │
└──────────────────┘
```

---

## Requirements

- Windows 10 / 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime) — only for the **lite** installer; the full installer is self-contained

---

## Installation

### Option A — Installer (recommended)

1. Go to the [Releases](../../releases) page
2. Download one of the two installers:
   - `InternetSpeedMonitor-Setup-<version>.exe` (~60 MB) — **full**, self-contained,
     works on any Windows 10/11 x64 PC with no prerequisites
   - `InternetSpeedMonitor-Setup-<version>-lite.exe` (~2 MB) — **lite**, requires the
     [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0/runtime);
     the installer checks for it and opens the download page if it's missing
3. Run it — installs per-user (no admin prompt), with optional desktop icon and
   "Start automatically with Windows" task. Uninstall from Windows Settings → Apps.

> **Note:** the binaries are unsigned, so Windows SmartScreen may warn on first run.
> Click **More info → Run anyway**.

### Option B — Build from source

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

```bash
git clone https://github.com/Shakhawat-dev/Internet_Speed_App.git
cd Internet_Speed_App
dotnet run
```

### Option C — Build the installers yourself

**Prerequisites:** .NET 8 SDK + [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`)

```bash
# Full (self-contained, ~60 MB)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
ISCC installer\installer.iss

# Lite (framework-dependent, ~2 MB)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o bin\Release\fdd-publish
ISCC /DLite installer\installer.iss
```

Output: `installer\Output\InternetSpeedMonitor-Setup-<version>.exe` and `...-lite.exe`

---

## Usage

| Action | Result |
|---|---|
| Launch the app | Widget appears where you last left it (bottom-right on first run) |
| Left-click + drag | Move the widget (snaps to screen edges) |
| Right-click | Context menu — Dashboard, Usage History, session counter, toggles, Settings |
| Right-click → Dashboard… | Live stats, usage totals, data cap, 30-day chart, network info |
| Right-click → Usage History… | Day-by-day usage table with CSV export |
| Double-click tray icon | Show / hide the widget |
| Hover tray icon | Current speeds + session usage tooltip |
| Right-click → Exit | Close the app |

### Start with Windows

Enable it in Settings (Window tab), from the right-click menu, or at install time. This adds the app to `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`.

### Click-through mode

When enabled, the widget ignores all mouse input — use the tray icon to interact with the app (double-click to show/hide, right-click for the menu).

---

## How it works

Network speed is measured by reading the total bytes sent/received across the selected network interfaces (`System.Net.NetworkInformation`) on each refresh, then dividing the delta by the elapsed time. Daily usage is accumulated from the same deltas and persisted to `%APPDATA%\InternetSpeedApp\usage.json`. The widget renders with per-pixel alpha (`UpdateLayeredWindow`), which is how background and text can have independent transparency. No drivers, no admin privileges, no background services required.

---

## License

MIT
