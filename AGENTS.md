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
- All credentials must stay local, never sent anywhere, not for any reason.
- Downloads and destructive operations must require clear user intent and must be
  secured to not cause any deletion out of scope of the operation.
- Ask the maintainer before changing authentication, Nexus integration, telemetry,
  updates, installers, uninstallers, archive extraction, or deletion behavior.
- Do not create Git tags, GitHub Releases, upload assets, or publish builds without
  explicit maintainer approval.

## Development expectations

- Make narrowly scoped changes and commit them to simple and understandable commits.
- Build the solution after code changes and report warnings as well as errors.
- Keep generated output, local builds, archives, and reference material out of Git.
- Update documentation when behavior, setup, privacy, or release steps change.
- Do not claim Nexus Mods, Casualties Unknown, Steam, GitHub, or BepInEx
  endorses this project or any association with them.

## Commit rules

- Commits should be atomic and simple.
- Commit messages should be human readable, not overly technical, containing just enough
  information to review and no fluff.
