# Casualties Hub v0.0.6-pre.8.5

**GitHub tag:** `v0.0.6-pre.8.5`  
**Release title:** `Casualties Hub v0.0.6-pre.8.5`  
**Mark as a pre-release:** Yes

## What changed

- RGB controls in Settings are now collapsed behind **Show RGB sliders**; hex input remains available at all times.
- Creating a Modlist Share Code now confirms when it has been copied to the clipboard.
- Hub Center can now install a locally downloaded Casualties Hub release ZIP for update or rollback use.
- Hub Center continues to show eligible GitHub updates and supports automatic GitHub update installation.

## Known issue

- Opera GX may not automatically open or come to the foreground when Casualties Hub opens a browser link.

## Release checklist

1. Publish with `dotnet publish "Casualties Hub\Casualties Hub.csproj" -c Release -o ".buildverify\publish"`.
2. Build a normal release folder with `Release Packaging\Build-CasualtiesHubRelease.ps1` and version `v0.0.6-pre.8.5`.
3. ZIP the resulting release folder yourself and attach it to the GitHub pre-release.
4. Use the tag and title above, then mark the release as a **pre-release**.
