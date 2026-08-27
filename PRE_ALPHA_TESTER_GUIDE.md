# Casualties Hub — Pre Alpha 0.0.1 Tester Guide

Thank you for testing Casualties Hub. This is an early pre-alpha build: it is meant to find broken installs, missing edge cases, confusing UI, and wrong mod metadata before wider testing.

Casualties Hub currently manages **Casualties Unknown / Scav Prototype** BepInEx mods. It does not change the game’s code or upload player data.

## Before You Start

1. Close Casualties Unknown before installing, deleting, enabling, or disabling mods.
2. Back up anything important in `BepInEx\Plugins`, especially custom sprites, sounds, or other personal edits.
3. In Casualties Hub, open **Settings** and set either the game folder, the `BepInEx` folder, or the `BepInEx\Plugins` folder. The Hub should resolve the correct Plugins folder automatically.
4. Wait for the Nexus Dashboard to finish loading its community metadata.

## Please Test These Areas

### 1. Nexus Dashboard

Expected behavior:

- Lists community mod metadata in pages of 50 mods.
- Search filters by mod name and author.
- Sort works for total downloads, unique downloads, endorsements, date, and name.
- The **Show 18+ mods** checkbox is off by default. The manually marked adult-content entries should only appear when it is enabled.
- Clicking a mod card opens its description inside that card. The description is manually scrollable with the mouse wheel.
- Installed cards receive a subtle green tint.
- Cards show installed, disabled, up-to-date, or out-of-date information where the Hub can identify a local mod.
- **Open Download** opens Nexus’s Files/download page in your browser.
- **View Modpage** opens the normal Nexus mod page.

Premium Nexus users may save their own API key in Settings. When a key is saved, **Open Download** becomes **Download** and attempts a direct Nexus download. Do not share your API key with anyone.

Things to look for:

- Wrong mod title, image, description, version, dependency, or update status.
- A card marked installed when it is not, or not installed when it is.
- The wrong Nexus page opening.
- A description that cannot be scrolled or a card that does not close.

### 2. Download Inbox and Automatic Import

The Hub watches the configured download inbox for `.zip`, `.7z`, and `.rar` files. When a new supported archive finishes downloading, it asks whether to install it.

Expected behavior:

- Selecting **Yes** installs the archive into the appropriate game location.
- Selecting **No** leaves it alone.
- Mod archives copy all included files except `.txt` files.
- Archives containing a `BepInEx` or `Plugins` layout preserve that layout.
- A DLL-only mod is installed beneath `BepInEx\Plugins`.
- The `experimentCrus.png` skin flow asks for an `st0`–`st9` CustomSprites slot.
- If an incoming mod matches existing files, the Hub warns before replacement.

In **Settings**, `DADIPF` means **Disable Auto Delete Imported Parent Files**:

- Off (default): after a successful automatic import, the archive is moved out of the download inbox into Hub storage.
- On: the original archive remains in the download inbox after import.

Things to look for:

- Files going to the wrong folder.
- Files missing after an install.
- `.txt` files being installed.
- The same archive being repeatedly imported.
- DADIPF not preserving/removing the archive as expected.

### 3. Local Mods

This page reads your resolved `BepInEx\Plugins` folder.

Expected behavior:

- Lists detected DLL mods and mod folders.
- **Refresh** reloads the local list.
- The search box filters local mods, GUIDs, and dependency labels.
- **Disable** renames a mod DLL to `.dll.disabled`.
- **Enable** changes it back to `.dll`.
- Disabled entries appear dark red.
- **Disable all** and **Enable all** apply to all detected DLLs.
- Out-of-date entries display a red **Out of date** button that opens the matching Nexus Files page.
- A local version of `0.0.0.0` is treated as a placeholder and should not be marked out of date.

### 4. Modlist Share Codes

Use **Modlist Share Code** to create and copy a readable `CUH1:` code containing installed mod GUIDs and metadata.

Expected behavior:

- The share code is copied to the clipboard.
- Paste a code into **Paste sharecode here** and use **Paste and Import**.
- Missing mods from the imported list appear in purple.
- Missing entries can open Nexus or be ignored.

Important limitation: importing a share code identifies mods that are missing; it does not silently download every required mod.

### 5. Protected Assets

Use this before deleting/reinstalling a mod that contains custom content.

Expected behavior:

- **Protect Files** and **Protect Folder** save a copy of files/folders inside `BepInEx\Plugins`.
- **Restore All** deletes the current version at each saved destination, then replaces it with the protected copy.
- **Remove selected** removes the saved backup only; it does not delete the live game file.
- **Open protected folder** opens the Hub’s local backup folder in File Explorer.

Recommended test:

1. Protect a test folder or custom sprite folder.
2. Change or delete the live copy in Plugins.
3. Use Restore All.
4. Verify the original protected version comes back exactly.

### 6. Delete All Mods

**Delete all mods** removes everything inside `BepInEx\Plugins`.

This is intentionally destructive. Confirm that anything you want to keep has first been added to Protected Assets or manually backed up. Testers should use a disposable test install whenever possible.

### 7. Settings and Debug Console

Settings includes the game path, download inbox, optional Premium Nexus key, and DADIPF option.

The **Debug Console** shows Hub activity and has a button above the log to open BepInEx crash logs (`LogOut.log` / `LogOutput.log`) in Notepad when available.

## Reporting a Bug

Please report bugs in the [Casualties Hub Discord](https://discord.gg/4srPZjkGSH). Include:

1. What you clicked or did, step by step.
2. What you expected to happen.
3. What happened instead.
4. The mod name, version, and Nexus link if relevant.
5. Whether the mod was installed, disabled, imported from a share code, or manually copied.
6. A screenshot or screen recording when possible.
7. Relevant BepInEx crash log text and the Hub Debug Console output.
8. Your game folder type: game root, BepInEx folder, or Plugins folder.

Please report **Hub/install/compatibility problems** to the Hub team. For an individual mod’s gameplay bug or feature request, use that mod author’s Nexus page or original support location.

## Known Pre-Alpha Limitations

- Dependency and incompatibility information is community-maintained and incomplete.
- Metadata matching can fail when mod authors rename DLLs or do not supply version information.
- The Hub does not guarantee that every Nexus archive uses the same folder layout.
- Direct download is optional and depends on the tester’s own Premium Nexus API key; normal users use browser downloads.
- This build has not been tested against every mod combination. Do not rely on it as the only backup of important files.

## Current Build

**Pre Alpha 0.0.1**
