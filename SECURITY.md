# Security Policy

## Reporting a vulnerability

Do not open a public issue for a vulnerability that could expose credentials, execute
untrusted code, overwrite/delete arbitrary files, compromise the update path, or reveal
private user data. Contact the maintainer through the Casualties Hub Discord listed in
`README.md` and request a private reporting channel. Do not include secrets or working
exploits in the initial public message.

Include the affected version, impact, reproduction conditions, and suggested mitigation
when possible. Test only against systems, accounts, and files you are authorized to use.

## Security-sensitive areas

- Nexus API-key storage and authorized download behavior.
- Archive inspection, extraction, and installation paths.
- File deletion, replacement, protected-asset restoration, and uninstallation.
- Update discovery, download, validation, and process launching.
- Supabase configuration, remote metadata, and other network responses.
- Diagnostic logs and settings that may contain local paths or user information.

## Contributor requirements

- Never commit secrets, personal data, game files, or third-party mod files.
- Reject absolute paths, traversal segments, and destinations outside the intended root
  when extracting or installing archives.
- Use HTTPS and documented service endpoints.
- Do not weaken Nexus access controls or attempt to evade rate limits.
- Do not log credentials, authorization headers, signed URLs, or sensitive payloads.
- Keep destructive actions narrowly scoped, validated, and clearly confirmed.
- Treat all remote content and user-selected archives as untrusted.

Supported security fixes target the latest public release and active development branch.
Older releases may be replaced rather than patched individually.
