# Casualties Hub Tester Guide

Thank you for testing Casualties Hub. Testing builds are meant to find broken installs, missing edge cases, confusing UI, and wrong mod metadata before a wider release.

Casualties Hub manages **Casualties Unknown** BepInEx mods on Windows and Linux. It does not change the game’s code or upload player data.

## Which build to test

Builds are published on the [Releases page](https://github.com/Casualties-Hub/Casualties-Hub-Public-Releases/releases). Releases marked **Pre-release** are testing builds; the one marked **Latest** is the current public build. Always test the newest release unless you were asked to check a specific one.

To see which version you are running, open **Hub Home** and look at **What changed in this build**, or check the version shown at the bottom of the sidebar.

## Before You Start

1. Close Casualties Unknown before installing, deleting, enabling, or disabling mods.
2. Back up anything important in `BepInEx/plugins`, especially custom sprites, sounds, or other personal edits. **Skins & Backups > Backups > Back up now** does this for you.
3. In Casualties Hub, open **Settings** and set the **Game folder**, or press **Detect**. The game folder, the `BepInEx` folder, and the `BepInEx/plugins` folder are all accepted; the Hub should resolve the correct plugins folder automatically.
4. Wait for the Nexus Dashboard to finish loading its community metadata.

On first launch with no game folder, the Hub offers to open Settings, and the Nexus Dashboard shows **Detect automatically**, **Choose folder...**, and **Open Settings**. Check that each of these works.

## Please Test These Areas

### 1. Nexus Dashboard

Expected behavior:

- Lists community mod metadata in pages of 50 mods, with **Previous** and **Next**.
- Search filters by mod name, author, or description.
- The filter offers **All mods**, **Installed**, **Not installed**, and **Updates available**.
- Sort offers **Most downloaded**, **Most endorsed**, **Name A-Z**, and **Author A-Z**.
- The **18+** checkbox is off by default. Mods flagged as adult content only appear when it is ticked.
- The two buttons beside **18+** switch between **tiles** and a **list**. The choice is remembered between launches.
- Tiles show the mod artwork with the name, author, and download count along the bottom. Clicking a tile opens a detail panel over the grid with the full description, image, stats, and actions. Close it with the X, by clicking the same tile again, or by clicking outside the panel. The grid keeps its scroll position.
- List rows show a small thumbnail, the name, author, a one-line excerpt, downloads, and endorsements. Clicking a row folds the full description out beneath it with a larger image.
- Right-clicking a tile or row opens a menu with the mod's action, **Show details** or **Hide details**, **View mod page on Nexus**, and **Copy Nexus link**.
- Installed mods show an **Installed**, **Update available**, or **Disabled** badge. Installed mods get a green outline and out-of-date mods an amber one.
- Descriptions should read with single line spacing, with blank lines only where the author left a paragraph gap.
- The action button reads:
  - **Open Download** for mods you do not have. It opens Nexus’s Files page in your browser.
  - **Open Update** when the installed copy is out of date.
  - **Re-enable in Local Mods** when the installed copy is disabled.
- The external-link icon button and **View mod page on Nexus** open the normal Nexus mod page.

Premium Nexus users may save their own API key in **Settings > Nexus Mods**. When a key is saved, **Open Download** becomes **Download** and **Open Update** becomes **Update**, and the Hub attempts a direct Nexus download. Do not share your API key with anyone.

Things to look for:

- Wrong mod title, image, description, version, dependency, or update status.
- A mod marked installed when it is not, or not installed when it is.
- The wrong Nexus page opening.
- A description that cannot be scrolled, a detail panel that does not close, or a description showing doubled blank lines.
- More than one row expanded at a time, or a detail panel left open after changing page, filter, or view.
- The tiles/list choice not surviving a restart.

### 2. Downloads Folder and Automatic Import

The Hub watches the folder set in **Settings > Downloads folder (watched for mod archives)** for `.zip`, `.7z`, and `.rar` files. When a new supported archive finishes downloading, it asks **Install the download ...?**

Expected behavior:

- **Install** installs the archive into the appropriate game location.
- Cancelling leaves it alone.
- Partially downloaded files are ignored until the download finishes.
- Mod archives copy all included files except `.txt` files.
- Archives containing a `BepInEx` or `plugins` layout preserve that layout.
- A DLL-only mod is installed beneath `BepInEx/plugins`.
- A skin archive (containing `experimentCrus.png`) asks which CustomSprites slot to use. At least `st0` to `st9` are offered, and higher slots appear when those are in use. Choosing an occupied slot asks for a second confirmation before replacing it.
- If an incoming mod matches existing files, the prompt says how many files will be replaced.
- An archive with nothing installable in it says so instead of installing.

**Local Mods > Install mod** installs an archive you pick from disk using the same rules.

In **Settings**, **Keep imported archives in the downloads folder**:

- Off (default): after a successful automatic import, the archive is moved out of the downloads folder into Hub storage, so it is not offered again.
- On: the original archive stays in the downloads folder after import.

Things to look for:

- Files going to the wrong folder.
- Files missing after an install.
- `.txt` files being installed.
- The same archive being repeatedly offered or imported.
- The keep-archives setting not keeping or moving the archive as expected.

### 3. Local Mods

This page reads your resolved `BepInEx/plugins` folder.

Expected behavior:

- Lists detected DLL mods and mod folders.
- **Refresh** re-scans the folder and re-checks every mod against the community metadata.
- **Search Mods** filters by mod name, GUID, and dependency names.
- Disabling a mod renames its DLL to `.dll.disabled`. Enabling it changes it back to `.dll`.
- Disabled entries appear dark red.
- **Disable all** and **Enable all** apply to every detected DLL. Nothing is deleted.
- **Delete** on a single mod asks first, then permanently removes that mod's files.
- Out-of-date entries show an amber **Out of date** button that opens the matching Nexus Files page.
- A local version of `0.0.0.0` is treated as a placeholder and should not be marked out of date.
- Each column has a view picker: **Enabled Mods**, **Disabled Mods**, **Sharecode Requested Mods**, **Missing Dependencies**, **Update Available**, **Incompatibility**, **Known Bugs**, and **Needs Attention**. **Add column >** adds another column.
- Missing dependencies appear as their own entries with a download action and **Ignore**.

### 4. Modlist Share Codes

On Local Mods, **Share code** creates a `CUH1:` code listing your enabled mods and their versions, and copies it to the clipboard.

Expected behavior:

- The share code is copied to the clipboard.
- Paste a code into **Paste share code** and press **Import**. With the box empty, **Import** reads the clipboard.
- Missing mods from the imported list appear in purple.
- Missing entries offer **Open Download** (or **Search Nexus** when no download page is known) and **Ignore**.

Important limitation: importing a share code identifies mods that are missing; it does not download anything automatically.

### 5. Skins & Backups

This sidebar section has three tabs.

#### Skin Preview

Expected behavior:

- Lists the CustomSprites `st#` skins installed in your plugins folder.
- **Preview** draws the selected skin. **Head**, **Eyes**, and **Zoom** change the pose and size, **Turn around** mirrors it, and **Centre** resets zoom and scroll.
- **Open folder** opens the skins folder; **Refresh** re-reads it.

Things to look for: body parts, eyes, or limbs out of place on a skin that looks correct in game.

#### Protected Assets

Use this before deleting or reinstalling a mod that contains custom content.

Expected behavior:

- **Protect files...** and **Protect folder...** save a copy of files or folders inside `BepInEx/plugins`. Anything outside the plugins folder is refused.
- **Restore all** asks first, then replaces the current version at each saved destination with the protected copy.
- **Remove** on an entry removes the saved copy only; it does not delete the live game file.
- **Open folder** opens the Hub’s local protected-assets folder in your file manager.

Recommended test:

1. Protect a test folder or custom sprite folder.
2. Change or delete the live copy in plugins.
3. Use **Restore all**.
4. Verify the original protected version comes back exactly.

#### Backups

Expected behavior:

- **Back up now** copies everything in your plugins folder into a new timestamped backup. Your mods are left in place.
- Each backup offers **Open**, **Restore**, and **Delete**.
- **Restore** asks first, then copies the backup's files into your plugins folder, overwriting files with the same name. Mods added since the backup are left alone.
- **Delete** asks first, then permanently removes that backup only.
- **Open backups folder** opens the folder where backups are kept.

### 6. Delete all

**Local Mods > Delete all** clears out `BepInEx/plugins`. It asks twice before doing anything.

Mod folders and `.dll` files are moved into a new timestamped backup, which then appears under **Skins & Backups > Backups**. Disabled single-file mods (`.dll.disabled`) and other loose files directly inside plugins are currently left in place.

This is intentionally destructive. Confirm that anything you want to keep has first been protected or backed up. Testers should use a disposable test install whenever possible.

### 7. Hub Home

Expected behavior:

- Shows the current **Announcement**. **Check now** fetches the latest announcements immediately.
- **Previous announcements** lists earlier ones, or says there are none.
- **What changed in this build** shows the release notes for the version you are running, and **Release information** shows or hides the extra release details.
- **Release history** opens the GitHub Releases page. **Nexus page** and **Discord** open the community links.
- **Credits** opens the Credits page.
- With no internet connection, Hub Home still shows the last announcements it received.

### 8. Launching the Game

**Launch game** at the top of the window starts Casualties Unknown through Steam. Check it works with Steam already running and with Steam closed.

### 9. Settings and Debug Console

Settings includes:

- **Folders**: the game folder (with **Browse...** and **Detect**), the watched downloads folder, and the keep-archives option.
- **Appearance**: text size, four colours, a colour wheel, saved looks in **Slot 1** to **Slot 4** with **Save to slot**, **Default**, and **Animated RGB**. The Hub should refuse to make text unreadable against the background.
- **Nexus Mods**: your optional personal API key, with **Save** and **Clear**.
- **Extras**: **Enable easter eggs**, which takes effect on the next start.
- **Diagnostics**: **Copy report** and **Open logs folder**.

The **Debug Console** is hidden: click the version text at the bottom of the sidebar five times quickly to open it. It shows Hub activity and has buttons above the log to copy it, open the current log, open the BepInEx log (`LogOutput.log`, or `LogOut.log` if that is missing), create a crash report, and open the logs folder. Files open in your system's default app.

The bug icon at the bottom of the sidebar opens the place to report issues.

### 10. Linux

If you test on Linux, also follow `README-linux.txt` from the download and check:

- The Hub starts from a terminal and, after `install-desktop-entry.sh`, from the application menu.
- The game is detected with native Steam, Flatpak Steam, and extra Steam libraries on other drives.
- Links and folders open in your browser and file manager.
- `./casualties-hub --diagnostics` prints a readable report.

## Reporting a Bug

Please report bugs on the Casualties Hub Discord; the invite link is in the [README](README.md#feedback-and-support). Include:

1. What you clicked or did, step by step.
2. What you expected to happen.
3. What happened instead.
4. The Hub version, from Hub Home or the sidebar.
5. Your operating system (Windows or Linux, and which distribution).
6. The mod name, version, and Nexus link if relevant.
7. Whether the mod was installed, disabled, imported from a share code, or manually copied.
8. A screenshot or screen recording when possible.
9. The **Settings > Diagnostics > Copy report** output, relevant BepInEx log text, and the Debug Console output.

Please report **Hub/install/compatibility problems** to the Hub team. For an individual mod’s gameplay bug or feature request, use that mod author’s Nexus page or original support location.

## Known Limitations

- Dependency and incompatibility information is community-maintained and incomplete.
- Metadata matching can fail when mod authors rename DLLs or do not supply version information.
- The Hub does not guarantee that every Nexus archive uses the same folder layout.
- Direct download is optional and depends on the tester’s own Premium Nexus API key; normal users use browser downloads.
- Testing builds have not been tried against every mod combination. Do not rely on the Hub as the only backup of important files.
