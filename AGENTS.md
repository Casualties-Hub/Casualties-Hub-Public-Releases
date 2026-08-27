# Instructions for AI Coding Agents

Read this file, `PROJECT_RULES.md`, `ARCHITECTURE.md`, `SECURITY.md`, and
`CONTRIBUTING.md` before changing the project.

## Safety and scope

- Never implement a way to bypass Nexus Mods authentication, Premium requirements,
  download pages, rate limits, advertising, or other access controls.
- Use only documented, authorized Nexus Mods APIs and ordinary browser links.
- Never scrape Nexus Mods, fabricate download URLs, reuse captured download links,
  impersonate another application, or rehost third-party mods.
- Never commit API keys, passwords, tokens, signing material, private configuration,
  user data, game files, decompiled game code, or third-party mod files.
- Nexus credentials must remain local to the Windows user. Do not transmit them to
  Casualties Hub services or log them.
- Downloads and destructive operations must require clear user intent.
- Ask the maintainer before changing authentication, Nexus integration, telemetry,
  updates, installers, uninstallers, archive extraction, or deletion behavior.
- Do not create Git tags, GitHub Releases, upload assets, or publish builds without
  explicit maintainer approval.

## Development expectations

- Make narrowly scoped changes and preserve unrelated user work.
- Build the solution after code changes and report warnings as well as errors.
- Add or update tests when changing testable logic.
- Use disposable directories and game copies for installation/deletion testing.
- Keep generated output, local builds, archives, and reference material out of Git.
- Update documentation when behavior, setup, privacy, or release steps change.
- Do not claim Nexus Mods, Casualties Unknown, Steam, GitHub, or BepInEx
  endorses this project.

## Nexus-related changes

Any Nexus-related contribution must preserve the non-Premium browser flow, keep
Premium/API behavior opt-in, respect API responses and rate limits, and fail closed
when authorization cannot be verified. Such changes require maintainer review.
