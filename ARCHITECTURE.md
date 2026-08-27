# Casualties Hub Architecture

## Repository map

| Path | Responsibility |
| --- | --- |
| `Casualties Hub Linux/` | The Hub: an Avalonia launcher and mod manager, running on Linux and Windows. |
| `Casualties Hub Linux/Views/` | UI pages and their event-handling code. |
| `Casualties Hub Linux/Models/` | Settings, metadata, mod, and installation models. |
| `Casualties Hub Linux/Services/` | File, network, update, catalog, installation, and diagnostic logic. |
| `Casualties Hub Linux.Tests/` | Tests for the destructive and silent-failure paths. |
| `Tools/LinuxProbe/` | Standalone probe used when investigating Linux installs. |
| `Nexus Mod Package/` | Nexus package marker project. |
| `Release Packaging/` | Scripts and inputs used to assemble releases. |
| `Release Notes/` | In-application version history. |
| `GitHub Release Notes/` | Public release-page copy. |

`Casualties Hub Linux/Casualties Hub Linux.slnx` includes the Hub and its tests. The
folder name is historical: the project targets Linux and Windows from one codebase.

## Runtime data

User-specific settings, logs, protected assets, cached data, downloads, and credentials
must remain outside the repository. The application uses its local application-data
directory for persistent user data. A Nexus API key must never be committed, logged,
or sent to Casualties Hub services.

## External systems

- **Nexus Mods:** metadata/pages for mods and an opt-in authorized API flow. The normal
  fallback is the original Nexus browser page. See `PROJECT_RULES.md`.
- **GitHub:** source, release metadata, and approved application updates.
- **GitHub content:** public announcements and release information from `HubContent.json`,
  cached locally with conditional requests.
- **Steam:** launches Casualties Unknown through its registered Steam application ID.

All remote responses are untrusted input. Network failures must leave local mod
management usable wherever practical.

## Sensitive boundaries

The highest-risk code is archive extraction, filesystem deletion/replacement, Nexus
credentials and downloads, remote metadata parsing, updates, installer/uninstaller
operations, and process launching. Changes to these areas require focused review and
testing with disposable data.

## Build and release flow

Development builds are produced with the command in `CONTRIBUTING.md`. Build outputs
under `bin/` and `obj/`, published directories, and ZIP archives are intentionally
ignored by Git. Release packaging is an explicit maintainer action; ordinary commits
and pushes do not publish a release.

Version numbers, embedded release notes, public release notes, and packaged artifacts
must agree before a version tag or GitHub Release is created.
