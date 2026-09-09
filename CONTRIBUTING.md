# Contributing to Casualties Hub

Notes for setting up a local build environment for Casualties Hub on a fresh machine.

Casualties Hub is licensed under AGPL-3.0. Contributions are accepted under the same
licence.

Before making changes, read [`PROJECT_RULES.md`](PROJECT_RULES.md),
[`ARCHITECTURE.md`](ARCHITECTURE.md), and [`SECURITY.md`](SECURITY.md). AI-assisted
changes must also follow [`AGENTS.md`](AGENTS.md).

## Prerequisites

| Requirement | Notes |
| --- | --- |
| .NET SDK 10 | The **SDK**, not just the runtime. See below. |
| Internet access | The first build restores packages from nuget.org. |

The Hub is an Avalonia desktop application targeting `net10.0`, and builds and runs on
both Linux and Windows.

Visual Studio is optional. Everything below works with the `dotnet` CLI alone. If you do
use an IDE, it must understand the `.slnx` solution format.

## .NET SDK 10

Download it from <https://dotnet.microsoft.com/download/dotnet/10.0>, or install it with
your package manager. On Windows:

```bash
winget install Microsoft.DotNet.SDK.10
```

> **Runtimes are not enough.** `dotnet --list-runtimes` may show
> `Microsoft.NETCore.App 10.0.0` while `dotnet --list-sdks` is empty. Runtimes only execute
> finished binaries. Compiling needs the SDK, which ships `.slnx` support.

## Build

```bash
dotnet build "Casualties Hub.slnx" -c Release
```

Run it:

```bash
dotnet run --project "Casualties Hub/Casualties Hub.csproj"
```

`--selftest` constructs every page and dialog headlessly and reports which ones survived.
It needs no display, so it works over SSH and in CI.

```bash
dotnet run --project "Casualties Hub/Casualties Hub.csproj" -- --selftest
```

## Build fails with MSB3027 or MSB3021

The Hub is still running and holding a lock on its executable. Close it and build again.

## Project layout

| Path | Purpose |
| --- | --- |
| `Casualties Hub/` | The application. `Services/` holds the logic worth reading first. |
| `Casualties Hub.Tests/` | Tests for the destructive and silent-failure paths. |
| `Release Packaging/` | PowerShell scripts that assemble the release archives. |
| `Release Notes/`, `GitHub Release Notes/` | Per-version notes. Some are embedded in the app. |

## Before opening a pull request

1. `dotnet build "Casualties Hub.slnx" -c Release` reports no
   warnings and no errors.
2. `dotnet test "Casualties Hub.Tests/Casualties Hub.Tests.csproj"` passes.
3. Run the Hub and exercise the areas you touched. The tests cover the destructive paths,
   not the UI.
4. Check the Hub log folder for new errors.
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
Delete All Mods, and Settings, and states the expected behaviour for each. The tests cover
the destructive and silent-failure paths; that guide is the reference for everything the
UI does.

Work through the sections covering anything your change touches, and the whole guide for
wider changes.

## Publishing a release

Releases are built by CI. Nothing is uploaded by hand.

Releases are built from `main` only, so merge the version you are releasing first. Starting
the workflow from any other branch is refused before it builds.

1. Add `Release Notes/Version <version>.txt`. The Hub embeds this file and reads it for its
   What changed panel, so a release without it ships a build that cannot describe itself.
2. Add `GitHub Release Notes/GitHub Release Notes - v<version>.md` for the release page body.
   This one is optional: without it the draft gets a placeholder to fill in before publishing.
3. Run the `Release` workflow from the Actions tab. Give it the version without a leading `v`
   and choose the release type:
   - `testing` needs a prerelease version such as `0.0.9-pre.1`. It is flagged as a prerelease
     and never marked latest, and the Hub reads its configuration from the prerelease channel.
   - `full` needs a plain version such as `0.0.9`, and is marked the latest release.
4. Review the draft on the Releases page: check the version, the notes, and that both the
   Linux tarball and the Windows executable are attached.
5. Press Publish. That creates the tag and makes the release public.

Up to step 5 nothing is public and no tag exists, so a draft that looks wrong can simply be
deleted and the workflow run again.

Building and testing happen first; the draft is only created after an approval on the
`release` environment, whose reviewers are configured in the repository settings. Testing
releases are gated the same way as full ones, because a testing build is still published
and still reaches everyone on the prerelease channel.
