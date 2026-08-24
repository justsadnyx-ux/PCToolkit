# PC Toolkit

A lightweight Windows bootstrapper and PC utility. It installs itself like a classic game launcher, keeps an eye on your hardware in real time, ships maintenance tools, and updates automatically — no manual downloads required.

> **Proprietary software.** PC Toolkit was built independently and is **not** open source. The source code is private; this repository exists only to distribute official builds.

---

## Features

- **Bootstrapper installer** — first-run setup with a live summary of your CPU, RAM, cores, OS and free disk space before you click Install
- **System specs dashboard** — processor, graphics, motherboard, memory speed, uptime and a per-drive storage table
- **Live stats** — real-time CPU load, memory usage and per-drive activity, refreshed every second
- **Maintenance tools** — one-click temp-file cleanup, DNS flush, Explorer restart, Disk Cleanup, elevated SFC scan, Task Manager
- **Logs & tools** — download log bundles from any http(s) URL and generate a full system report (specs, top processes, recent event-log warnings/errors)
- **Automatic updates** — the app checks this repository's Releases feed on startup and self-applies new versions

## Download

Grab the latest `PCToolkit.exe` from the [**Releases**](https://github.com/justsadnyx-ux/PCToolkit/releases) page.

The EXE is fully standalone (self-contained) — no .NET runtime or other dependencies are required.

## Getting started

1. Run `PCToolkit.exe`
2. The bootstrapper shows your system summary and lets you pick an install folder (default: `%LocalAppData%\Programs\PCToolkit`)
3. Optional shortcuts are created on the Desktop and Start Menu
4. Done — the app launches and keeps itself up to date from here

## Updating

PC Toolkit checks for a newer release every time it starts (and any time from the **Updates** tab). When one is found it offers a one-click download-and-restart update, replacing the installed copy in place.

## Uninstalling

Run the installed copy with the `--uninstall` argument, or use *Settings → Apps → Installed apps → PC Toolkit* on Windows.

## System requirements

| | |
|---|---|
| OS | Windows 10 / 11 (x64) |
| Runtime | None needed (self-contained build) |
| Privileges | Standard user (some tools request admin individually) |

## Legal

Copyright © 2026 PCToolkit Project. **All Rights Reserved.**

This program and its binaries are licensed for personal use as-is, without warranty of any kind. Redistribution, reverse engineering, or resale is prohibited. See [`LICENSE`](LICENSE).
