# Casualties Hub Mod Manager

Casualties Hub is a Windows launcher and mod manager for **Casualties Unknown**. It helps players browse community mod metadata, manage local BepInEx plugins, preserve custom assets, share mod setups, and identify common dependency or compatibility problems.

**Current public build:** `v0.0.7`
**Project status:** early public release; actively developed.

## Download and install

1. Open the [Releases page](../../releases).
2. Download the version you want from **Assets**.
3. Extract the downloaded ZIP somewhere outside `Program Files`.
4. Run `Casualties Hub.exe`.
5. On first launch, choose your Casualties Unknown, BepInEx, or Plugins folder in **Settings** if the Hub cannot locate it automatically.

Keep the included `Data` folder beside the executable. It contains editable dependency/compatibility catalog data and release notes used by the Hub.

Older releases remain available on GitHub for testing and rollback.

## What the Hub can do

- Browse Casualties Unknown community metadata in the **Nexus Dashboard**.
- Filter, search, sort, hide installed mods, and show adult-tagged mods only when the 18+ option is enabled.
- Open a mod’s Nexus Files/download page, or use direct downloads with a player’s own Nexus Premium API key.
- Detect and install supported archives from a chosen download inbox.
- Manage local BepInEx plugins: enable, disable, delete, refresh, and check version information.
- Highlight known dependency, update, incompatibility, and known-bug information where metadata is available.
- Create and import concise **Modlist Share Codes** for enabled local mods.
- Show missing share-code mods with an **Open Download** action for their specific Nexus Files page.
- Protect custom sprites, skins, sounds, character folders, and other assets locally, then restore them after a mod reinstall.
- Launch Casualties Unknown through Steam.
- Create local diagnostic logs and retain recent crash reports for troubleshooting.
- Use the **Hub Center** for announcements, Community Activity, compatibility notices, and eligible GitHub update information.
- Adjust text size, theme colours, file paths, and other local preferences in **Settings**.

## Nexus and mod installation

Casualties Hub is designed to send ordinary Nexus users to the original Nexus download page rather than bypassing Nexus downloads. Players who have their own Nexus Premium API key can save it in Settings to use direct-download actions where supported.

Some archives use custom layouts or have author-specific installation steps. Read the original mod page whenever the Hub marks an archive as requiring special instructions.

## Protected Assets

Protected Assets are saved **only on your PC**. Use them for things you do not want overwritten by a mod update, such as custom character `st#` folders, skins, sprite replacements, or sounds.

Choose either a file or the complete folder you want to preserve. **Restore All** puts the saved copies back into their remembered locations after a mod is installed or replaced.

## Online Services and Community Activity

Hub Online Services are **optional** and are disabled by default on a fresh install. They are controlled from **Hub Center**; local mod management, Protected Assets, and most launcher functions still work when they are off.

When enabled, the Hub may retrieve:

- Announcements and the last three locally saved announcements.
- Community compatibility and known-bug notices.
- Eligible GitHub update information.
- Aggregate Community Activity counts shown in Hub Center.

### What is sent

On the first successful Online Services check, Casualties Hub creates a random UUID (a long anonymous installation ID) and saves it locally in the Hub’s settings. The Hub sends that UUID to the status service when it checks for Hub updates and announcements. It is used only to count unique recent installations for Community Activity, such as active installations in the last two hours, day, or week.

**No IP address is used for Community Activity metrics.** Casualties Hub does not collect, store, display, or send your IP address as part of its activity-count data. The application sends the anonymous UUID and requests only the public Hub status data. Normal checks occur at most once every 30 minutes; manual checks are limited to once every 15 minutes, with additional local and server-side safeguards to prevent abuse.

The service response is cached locally. A new announcement, compatibility list, or status update replaces the stored current data while the announcement history retains only the latest three messages.

> Like any internet request, your network and hosting providers may process normal connection traffic independently. Casualties Hub’s own Community Activity system does not use IP addresses as its identifier or metric data.

## Relevant build history

| Version range | Notable additions |
| --- | --- |
| `v0.0.7` | Official public release; compact Accessibility-based Easter Eggs preference, refreshed documentation, and current packaging. |
| `v0.0.6-pre` series | Hub Center, optional Online Services, announcements, Community Activity, compatibility feed, local-mod column views, UI colour controls, credits, and diagnostic improvements. |
| `v0.0.5` and earlier | Core Nexus metadata browser, archive/import workflows, Protected Assets, local BepInEx scanning, dependency checks, version checks, and Modlist Share Codes. |

## Known limitations

- Dependency and compatibility data is community-maintained and may be incomplete or temporarily out of date.
- Some mods use custom DLL names or archive structures that cannot be matched automatically.
- Certain mod archives require manual installation because their author has special instructions.
- Opera GX may not automatically open or come to the foreground when Casualties Hub opens a browser link.

## Feedback and support

For feature discussion, downloads, and support, join the Casualties Hub Discord:

https://discord.gg/386M6zZEK

For bug reports, include:

- Casualties Hub version.
- What you were doing.
- Expected result and actual result.
- Installed mods or a Modlist Share Code.
- A diagnostic log or BepInEx `LogOutput.log` when relevant.

## Credits

Casualties Hub is a passion project created and maintained by **MarlyZ89**.

The application’s Credits page recognises community contributors, testers, metadata support, and resource permissions. JimmyKing has contributed ideas and granted permission for certain project resources; the current coding, implementation, maintenance, and overall project direction are handled by MarlyZ89.

## License

Casualties Hub is licensed under the [GNU Affero General Public License v3.0](LICENSE).

## Disclaimer

Casualties Hub is an independent community project. It is not affiliated with or endorsed by the developers of Casualties Unknown, Steam, Nexus Mods, BepInEx, GitHub, or Supabase.
