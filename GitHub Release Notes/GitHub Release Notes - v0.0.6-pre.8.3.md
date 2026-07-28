# Casualties Hub v0.0.6-pre.8.3

**GitHub tag:** `v0.0.6-pre.8.3`  
**Release title:** `Casualties Hub v0.0.6-pre.8.3`  
**Mark as a pre-release:** Yes

## What changed

- Default page text now uses Casualties Hub crimson (`#C21F32`).
- The top-left Casualties Hub wordmark always remains white and crimson, even when a player changes page text colours.
- Release publishing is now structured around a small root folder with `Data\Catalogs` and `Data\Release Notes` for supporting files.
- The Release build is configured as a self-contained single-file Windows executable.
- Hub Center's **What Changed** panel includes this build's summary.

## Known issue

- Opera GX may not automatically open or come to the foreground when Casualties Hub opens a browser link.

## Release checklist

1. Publish with `dotnet publish "Casualties Hub\Casualties Hub.csproj" -c Release -o ".buildverify\publish"`.
2. Run `Release Packaging\Build-CasualtiesHubRelease.ps1` with version `v0.0.6-pre.8.3` to create the normal release folder.
3. ZIP the resulting `Casualties Hub v0.0.6-pre.8.3` folder yourself and attach that ZIP to the GitHub pre-release.
4. Use the tag and title above, then mark the release as a **pre-release**.
