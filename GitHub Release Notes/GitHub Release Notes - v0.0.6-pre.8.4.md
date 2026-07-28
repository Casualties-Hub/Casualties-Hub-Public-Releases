# Casualties Hub v0.0.6-pre.8.4

**GitHub tag:** `v0.0.6-pre.8.4`  
**Release title:** `Casualties Hub v0.0.6-pre.8.4`  
**Mark as a pre-release:** Yes

## What changed

- Added a first-launch Online Services choice.
- Online services now start disabled for new installs.
- Players can enable announcements, maintenance status, and Hub update checks, or remain offline.
- Existing players keep their previously saved setting and are not prompted again.
- Nexus metadata remains available when players explicitly browse or refresh metadata.

## Known issue

- Opera GX may not automatically open or come to the foreground when Casualties Hub opens a browser link.

## Release checklist

1. Publish with `dotnet publish "Casualties Hub\Casualties Hub.csproj" -c Release -o ".buildverify\publish"`.
2. Build a normal release folder with `Release Packaging\Build-CasualtiesHubRelease.ps1` and version `v0.0.6-pre.8.4`.
3. ZIP the resulting release folder yourself and attach it to the GitHub pre-release.
4. Use the tag and title above, then mark the release as a **pre-release**.
