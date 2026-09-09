# Casualties Hub Architecture

## Repository map

| Path | Responsibility |
| --- | --- |
| `Casualties Hub/` | The Hub: an Avalonia launcher and mod manager. |
| `Casualties Hub/Views/` | UI pages and their event-handling code. |
| `Casualties Hub/Models/` | Settings, metadata, mod, and installation models. |
| `Casualties Hub/Services/` | File, network, update, catalog, installation, and diagnostic logic. |
| `Casualties Hub.Tests/` | Tests for the destructive and silent-failure paths. |
| `Nexus Mod Package/` | Nexus package marker project. |
| `Release Packaging/` | Scripts and inputs used to assemble releases. |
| `Release Notes/` | In-application version history. |
| `GitHub Release Notes/` | Public release-page copy. |

`Casualties Hub.slnx` includes the Hub and its tests. One project targets Linux and Windows;
platform differences are handled inside the services, not by separate applications.

## Runtime data

User-specific settings, logs, protected assets, cached data, downloads, and credentials
must remain outside the repository. The application uses its local application-data
directory for persistent user data. A Nexus API key must never be committed, logged,
or sent to Casualties Hub services.

## External systems

- **Nexus Mods:** metadata/pages for mods and an opt-in authorized API flow. The normal
  fallback is the original Nexus browser page. See `PROJECT_RULES.md`.
- **GitHub:** source, release metadata, and approved application updates.
- **Hub configuration:** public announcements and community links from the
  [Casualties Hub Config](https://github.com/Casualties-Hub/Casualties-Hub-Config) repository,
  published to `casualties-hub.github.io`. A prerelease build reads the `prerelease` channel and
  every other build reads `stable`; both are cached locally with conditional requests.
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

Releases are built by the `Release` workflow, started by hand from the Actions tab with
a version and a choice of testing or full. It builds and tests both platforms, stamps the
version into the binary, packages the Linux tarball and the Windows executable, and leaves
them on a draft GitHub Release. The draft is private and announces nothing; a maintainer
reviews it and presses Publish, which is what creates the tag and makes the release real.

The workflow refuses a request before building when the version is unreadable, when the
embedded release notes for it are missing, when the tag already exists, or when the version
and the chosen release type disagree. It also checks the built binary reports the version it
was asked to build, because that string picks the update channel and the notes the Hub shows.

`Release Packaging/Build-CasualtiesHubRelease.ps1` builds the same assets locally. CI is the
route for anything published; the script remains useful for testing a package by hand.
