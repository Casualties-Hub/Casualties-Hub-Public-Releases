# GitHub automatic-update checklist

Repository: `MarlyZ89/Casualties-Hub-Public-Release`

1. Create a **GitHub Release**, not merely a repository commit.
2. Use a tag exactly like `v0.0.6-pre.1` or `v0.0.6`.
3. Mark `v0.0.6-pre.1` as **This is a pre-release**. Do not mark final releases as pre-releases.
4. Upload one ZIP containing the published Hub folder. Somewhere inside the ZIP, it must contain `Casualties Hub.exe`.
5. Do not upload a source-code ZIP as the update package. Upload the published build ZIP.
6. GitHub displays a SHA-256 digest for uploaded release assets. The Hub requires that digest before it will replace files.

Channel rules:

- A stable installation such as `v0.0.6` accepts only a later stable release.
- A prerelease installation such as `v0.0.6-pre.1` accepts a later prerelease or a final stable release.
- The Hub selects the newest eligible release automatically.
