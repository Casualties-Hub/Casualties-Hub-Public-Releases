# Casualties Hub Mod Manager

Casualties Hub is a launcher and mod manager for **Casualties Unknown**, running on Linux and Windows. It helps players browse community mod metadata, manage local BepInEx plugins, preserve custom assets, share mod setups, and identify common dependency or compatibility problems.

**Project status:** early public release; actively developed. The current version is always
on the [Releases page](../../releases): the release marked **Latest** is the current public
build, and releases marked **Pre-release** are testing builds.

## Download and install

1. Open the [Releases page](../../releases).
2. Download the version you want from **Assets**. Pick the release marked **Latest** unless you want to help test.
3. On Windows, download the `.exe` and run it from any folder outside `Program Files`.
4. On Linux, download the `.tar.gz`, extract it, and run `casualties-hub`. The included
   `README-linux.txt` covers the one-time `chmod` and optional desktop entry.
5. On first launch, choose your Casualties Unknown, BepInEx, or Plugins folder in **Settings** if the Hub cannot locate it automatically.

The Hub is a single file with the catalogs and release notes built in. To hand-edit the catalogs, create a `Data` folder beside the executable; the Hub prefers files found there.

Older releases remain available on GitHub for testing and rollback.

## What the Hub can do

- Browse Casualties Unknown community metadata in the **Nexus Dashboard**, as tiles or as a list.
- Search, filter (installed, not installed, updates available), sort, and show adult-tagged mods only when the **18+** option is ticked.
- Open a mod’s Nexus Files/download page, or use direct downloads with a player’s own Nexus Premium API key.
- Detect and install supported archives (`.zip`, `.7z`, `.rar`) from a watched downloads folder, or install an archive from disk.
- Manage local BepInEx plugins: enable, disable, delete, refresh, and check version information.
- Highlight known dependency, update, incompatibility, and known-bug information where metadata is available, with column views such as **Needs Attention** and **Missing Dependencies**.
- Create and import concise **Modlist Share Codes** for enabled local mods.
- Show missing share-code mods with an **Open Download** action for their specific Nexus Files page.
- Use **Skins & Backups** to:
  - preview installed CustomSprites character skins in **Skin Preview**,
  - protect custom sprites, skins, sounds, character folders, and other assets locally, then restore them after a mod reinstall,
  - take and restore full backups of the plugins folder.
- Launch Casualties Unknown through Steam.
- Create local diagnostic logs, retain recent crash reports, and copy a diagnostics report from **Settings**.
- Use **Hub Home** for announcements, previous announcements, what changed in the current build, links to the release history, Nexus page and Discord, and credits.
- Adjust text size, theme colours, and folder paths in **Settings**, save up to four custom looks, turn on Animated RGB, and enable optional easter eggs under **Extras**.

On Linux, see the `README-linux.txt` included in the download for requirements and troubleshooting.

## Nexus and mod installation

Casualties Hub is designed to send ordinary Nexus users to the original Nexus download page rather than bypassing Nexus downloads. Players who have their own Nexus Premium API key can save it in Settings to use direct-download actions where supported.

Some archives use custom layouts or have author-specific installation steps. Read the original mod page whenever the Hub marks an archive as requiring special instructions.

## Protected Assets

Protected Assets are saved **only on your PC**. Use them for things you do not want overwritten by a mod update, such as custom character `st#` folders, skins, sprite replacements, or sounds.

Choose either a file or the complete folder you want to preserve. **Restore All** puts the saved copies back into their remembered locations after a mod is installed or replaced.

## Network access

The Hub has no account and no online services to sign up for. Local mod management, Protected Assets, Backups, and Skin Preview work offline. It connects to:

- **The Casualties Hub Config site** (`casualties-hub.github.io`, published from the public [Casualties Hub Config](https://github.com/Casualties-Hub/Casualties-Hub-Config) repository) for the announcements and community links shown in **Hub Home**. Testing builds read a separate prerelease channel.
- **GitHub**, for the community mod metadata shown in the **Nexus Dashboard**.
- **Nexus Mods**, for mod images, and for direct downloads only when you have saved your own API key.

Steam is started through a local `steam://` link. The Hub does not update itself; new builds are downloaded from the Releases page.

### What is sent

Casualties Hub does not create or send an installation ID, does not collect activity metrics, and does not send your settings anywhere. Your Nexus API key is only ever sent to Nexus Mods. Like any internet request, network and hosting providers process normal connection information independently.

Announcements are checked when the Hub starts and when Hub Home opens, no more than once every 30 minutes unless you press **Check now**. Community metadata is reused for up to six hours. Responses are cached locally and use HTTP change validators, so unchanged files are not downloaded again, and the cached copies are used when you are offline.

## Known limitations

- Dependency and compatibility data is community-maintained and may be incomplete or temporarily out of date.
- Some mods use custom DLL names or archive structures that cannot be matched automatically.
- Certain mod archives require manual installation because their author has special instructions.
- Opera GX may not automatically open or come to the foreground when Casualties Hub opens a browser link.

## Feedback and support

For feature discussion, downloads, and support, join the 
[Casualties Hub Discord](https://discord.gg/th62vFHWTR)

For bug reports, include:

- Casualties Hub version.
- What you were doing.
- Expected result and actual result.
- Installed mods or a Modlist Share Code.
- A diagnostic log or BepInEx `LogOutput.log` when relevant.

## Development and project policy

- [`CONTRIBUTING.md`](CONTRIBUTING.md) explains how to build and test the project.
- [`ARCHITECTURE.md`](ARCHITECTURE.md) describes the repository and sensitive boundaries.
- [`PROJECT_RULES.md`](PROJECT_RULES.md) defines official project and Nexus compliance rules.
- [`SECURITY.md`](SECURITY.md) explains responsible vulnerability reporting.
- [`AGENTS.md`](AGENTS.md) provides safety instructions for AI-assisted development.

## Credits

Casualties Hub is a passion project created by **MarlyZ89**.
Currently maintained by **Chundelac**.

The application’s Credits page, reached from **Hub Home**, recognises community contributors, testers and metadata support.
JimmyKing has contributed ideas and granted permission for certain project resources.
Overall project direction are handled by MarlyZ89.

## License

Casualties Hub is licensed under the [GNU Affero General Public License v3.0](LICENSE).

## Disclaimer

Casualties Hub is an independent community project. It is not affiliated with or endorsed by the developers of Casualties Unknown, Steam, Nexus Mods, BepInEx, or GitHub.
