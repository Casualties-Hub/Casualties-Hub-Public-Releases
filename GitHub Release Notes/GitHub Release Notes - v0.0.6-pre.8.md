# GitHub release posting guide — Casualties Hub v0.0.6-pre.8

This is a local posting checklist. Do **not** upload this file to the GitHub release ZIP. It exists so the exact title, tag, assets, and notes are not lost after posting.

## 1. Create the release

1. Open `MarlyZ89/Casualties-Hub-Public-Release` on GitHub.
2. Open **Releases** and choose **Draft a new release**.
3. Use these exact release fields:

| GitHub field | Value |
| --- | --- |
| Choose a tag | `v0.0.6-pre.8` |
| Target | `main` |
| Release title | `Casualties Hub v0.0.6-pre.8` |
| Set as a pre-release | **Checked** |
| Set as latest release | **Unchecked** for this pre-release |

## 2. Upload only the release assets

Upload these two files from the local release package:

- `Casualties Hub v0.0.6-pre.8.zip`
- `Casualties Hub v0.0.6-pre.8.sha256.txt`

Do **not** upload the unzipped package folder, this guide, source code, `.pdb` files, or development folders.

## 3. Paste this release description

```md
# 📢 Casualties Hub Pre-Release

## Version
**v0.0.6-pre.8**

**Release Date:** July 27, 2026

---

# ⚠ Important

This is a **Pre-Release** build. Expect bugs, unfinished features, and occasional breaking changes. Back up custom assets and mods before testing.

---

# ✨ What Changed

- Simplified page layouts by removing duplicate headers and unnecessary text.
- Reworked the Credits page with square contributor cards, role labels, descriptions, and photo placeholders.
- Added saved RGB controls for the Hub's primary text colour.
- Expanded the text-size accessibility setting up to size 20.
- Moved share-code controls into a compact right-side Local Mods toolbar.
- Added a manual show/hide control for the third Local Mods column.
- Added clearer share-code download actions: **Open Download** for exact Nexus matches and **Search Nexus** when only a search is available.
- New share codes retain the metadata ID required to open the exact Nexus Files page; older share codes remain supported.
- Renamed the download preservation option to **Keep imported downloads**.
- Added an **Open logs folder** button in Settings.

---

# ⚠ Known Issues

- Opera GX may not automatically open or come to the foreground when Casualties Hub opens a browser link.

---

# 📥 Installation

1. Download the attached ZIP.
2. Extract it anywhere outside `Program Files`.
3. Run `Casualties Hub.exe`.
4. Configure your Casualties Unknown folder in **Settings** if it is not detected automatically.

---

# 📝 Reporting Bugs

Report issues through the Casualties Hub Discord. Include your launcher version, what you were doing, expected versus actual results, installed mods or Share Code, and a diagnostic log when possible.
```

## 4. Final check before publishing

- Confirm the tag is exactly `v0.0.6-pre.8`.
- Confirm **Pre-release** is checked.
- Confirm the uploaded ZIP opens and contains `Casualties Hub.exe`.
- Confirm the checksum file matches the uploaded ZIP name.
- Publish the release, then copy its URL into the Casualties Hub Discord post.
