# SteamSwitcher 2.0

SteamSwitcher is a high-performance Windows utility that puts your Steam accounts and installations one click away with **Desktop and Start Menu shortcuts**.

The current version is **2.0.0**.

---

## What it does

- **1-Click Account Switching:** Switch between multiple Steam accounts on a single installation in under 2 seconds without re-entering credentials or Steam Guard.
- **Dynamic Multi-Account Support:** Automatically detects all configured accounts on your PC with avatars, persona names, and login status.
- **Multi-Installation Support (Point 5):** For power users maintaining separate physical Steam directories (e.g. Steam Beta, isolated sandboxes), SteamSwitcher performs **atomic Windows registry & `steam://` protocol swapping** to guarantee Big Picture mode, external links, and anti-cheat (VAC / EAC) work seamlessly without UAC service repair errors.
- **Native Windows Shortcuts:** Creates Desktop and Start Menu shortcuts (Normal and Big Picture) using the native Win32 `IShellLinkW` interface (no Windows Script Host dependency).
- **Graceful Shutdown & Data Safety:** Sends clean `-shutdown` signals to Steam and waits for active Cloud Save synchronizations to complete before switching.
- **No Background Bloat:** Fully standalone shortcuts. SteamSwitcher does not need to stay running in the background.

---

## Architecture

The project is built on **.NET 9 (Windows)**:

```text
src/
├── SteamSwitcher.Core    Shared domain logic, robust VDF KeyValues parser, process lifecycle & registry engine
├── SteamSwitcher.Cli     Lightweight, instant WinExe runner invoked by desktop shortcuts
└── SteamSwitcher.Setup   Modern WPF management UI
tests/
└── SteamSwitcher.Core.Tests  Automated unit test suite (xUnit)
```

---

## Requirements

- 64-bit Windows 10 or Windows 11.
- .NET 9 Desktop Runtime (or use the self-contained portable release).

---

## Build from Source

```powershell
# Build entire solution and run unit tests
.\build.ps1

# Publish a clean portable release to dist/SteamSwitcher
.\build.ps1 -Publish
```

---

## License and Trademarks

Distributed under the **MIT License**. See `LICENSE.txt`.

Steam and the Steam logo are trademarks and/or registered trademarks of Valve Corporation in the U.S. and/or other countries. SteamSwitcher is an independent open-source utility and is not affiliated with or endorsed by Valve Corporation.
