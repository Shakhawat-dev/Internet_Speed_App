# Internet Speed Monitor

A lightweight, always-on-top floating widget for Windows 11 that shows your live network upload and download speeds.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square) ![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D4?style=flat-square) ![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

---

## Features

- **Live speed display** — refreshes every second, shows download (↓) and upload (↑) separately
- **Always on top** — floats over all other windows without stealing keyboard focus
- **Draggable** — left-click and drag to position anywhere on screen
- **Never intrudes** — hidden from the taskbar and Alt+Tab switcher
- **Windows 11 rounded corners** via DWM
- **Configurable settings** (right-click → Settings):
  - Opacity (20–100%)
  - Font size (8–28 pt)
  - Orientation — vertical (stacked) or horizontal (side by side)
  - Units — Binary (KiB/MiB, base 1024) or Decimal (KB/MB, base 1000)
- **Start with Windows** toggle in settings
- Settings are persisted to `%APPDATA%\InternetSpeedApp\settings.json`

---

## Screenshots

```
Vertical (default)       Horizontal
┌──────────────────┐     ┌───────────────────────────────┐
│ ↓  5.2 MiB/s    │     │ ↓  5.2 MiB/s   ↑  1.1 KiB/s │
│ ↑  1.1 KiB/s    │     └───────────────────────────────┘
└──────────────────┘
```

---

## Requirements

- Windows 10 / 11
- [.NET 8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (if running the framework-dependent build)

---

## Installation

### Option A — Download the release (recommended)

1. Go to the [Releases](../../releases) page
2. Download `InternetSpeedApp.exe`
3. Run it — no installer needed

### Option B — Build from source

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

```bash
git clone https://github.com/shakhawat-dev/Internet_Speed_App.git
cd Internet_Speed_App
dotnet run
```

### Option C — Publish as a self-contained single `.exe`

No .NET runtime required on the target machine:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `bin\Release\net8.0-windows\win-x64\publish\InternetSpeedApp.exe`

---

## Usage

| Action | Result |
|---|---|
| Launch the app | Widget appears near the bottom-right corner |
| Left-click + drag | Move the widget anywhere |
| Right-click | Open context menu |
| Right-click → Settings | Open the settings dialog |
| Right-click → Exit | Close the app |

### Start with Windows

Open Settings (right-click → Settings) and enable **Start with Windows**. This adds the app to `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`.

---

## How it works

Network speed is measured by reading the total bytes sent/received across all active non-loopback network interfaces (`System.Net.NetworkInformation`) once per second, then dividing the delta by the elapsed time. No drivers, no admin privileges, no background services required.

---

## License

MIT
