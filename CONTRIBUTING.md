# Contributing to Casualties Hub

Notes for setting up a local build environment for Casualties Hub on a fresh Windows
machine.

Casualties Hub is licensed under AGPL-3.0. Contributions are accepted under the same
licence.

Before making changes, read [`PROJECT_RULES.md`](PROJECT_RULES.md),
[`ARCHITECTURE.md`](ARCHITECTURE.md), and [`SECURITY.md`](SECURITY.md). AI-assisted
changes must also follow [`AGENTS.md`](AGENTS.md).

## Prerequisites

| Requirement | Notes |
| --- | --- |
| Windows | The Hub is a WPF desktop application and targets `net10.0-windows`. Linux and macOS are not supported. |
| .NET SDK 10 | The **SDK**, not just the runtime. See below. |
| Internet access | The first build restores two packages from nuget.org. |

Visual Studio is optional. Everything below works with the `dotnet` CLI alone. If you do
use an IDE, it must understand the `.slnx` solution format and `net10.0-windows`.

## .NET SDK 10

Install it with winget:

```bash
winget install Microsoft.DotNet.SDK.10
```

Or download the installer from <https://dotnet.microsoft.com/download/dotnet/10.0>.

> **Runtimes are not enough.** `dotnet --list-runtimes` may show
> `Microsoft.NETCore.App 10.0.0` and `Microsoft.WindowsDesktop.App 10.0.0` while
> `dotnet --list-sdks` is empty. Runtimes only execute finished binaries. Compiling needs
> the SDK, which ships the WPF build targets and `.slnx` support.

## Build

```bash
dotnet build "Casualties Hub.slnx" -c Release
```

This produces two projects:

- `Casualties Hub/bin/Release/net10.0-windows/Casualties Hub.exe`: the Hub itself
- `Casualties Hub Installer/bin/Release/net10.0-windows/`: the standalone Setup Wizard

Run the Hub directly from its build output:

```bash
"Casualties Hub/bin/Release/net10.0-windows/Casualties Hub.exe"
```

## Build fails with MSB3027 or MSB3021

The Hub is still running and holding a lock on `Casualties Hub.exe`. Close it and build
again.

## Project layout

| Path | Purpose |
| --- | --- |
| `Casualties Hub/` | The WPF application. `Services/` holds the logic worth reading first. |
| `Casualties Hub Installer/` | Standalone Setup Wizard, published separately from the Hub ZIP. |
| `Release Packaging/` | PowerShell scripts that assemble the release ZIPs. |
| `HubContent.json` | Public announcement and release-information feed cached by the Hub. |
| `Release Notes/`, `GitHub Release Notes/` | Per-version notes. Some are embedded in the app. |

## Before opening a pull request

1. `dotnet build "Casualties Hub.slnx" -c Release` reports no warnings and no errors.
2. Run the Hub and exercise the areas you touched. There is no automated test suite, so
   manual verification is the only safety net.
3. Check `%LOCALAPPDATA%\CasualtiesHub\Logs` for new errors.
4. Test mod install, enable, disable, and delete against a disposable copy of the game
   folder if you changed anything under `Services/`.
5. Confirm that no API keys, credentials, personal data, game files, decompiled game
   code, third-party mods, local builds, or release archives are included.
6. Nexus, credentials, downloads, updates, installers, archive extraction, telemetry,
   process launching, and deletion changes require focused maintainer review.

## Testing your changes

**Read [`PRE_ALPHA_TESTER_GUIDE.md`](PRE_ALPHA_TESTER_GUIDE.md).**

The guide walks through every major area of the Hub, including the Nexus Dashboard, the
Download Inbox and automatic import, Local Mods, Modlist Share Codes, Protected Assets,
Delete All Mods, and Settings, and states the expected behaviour for each. Because there
is no automated test suite, that guide is the reference for whether a change broke
something.

Work through the sections covering anything your change touches, and the whole guide for
wider changes.
