# Casualties Hub Project Rules

These rules govern the official Casualties Hub repository, builds, services, and
contributions. They supplement the project license; they do not reduce rights granted
by the GNU Affero General Public License.

## Nexus Mods compliance

Official Casualties Hub development must not:

- Circumvent Nexus Mods authentication, Premium requirements, download pages, rate
  limits, advertising, or technical access controls.
- Scrape Nexus Mods pages or use undocumented/private endpoints.
- Fabricate download URLs, reuse captured download links, or bypass an API response.
- Impersonate Vortex or any other registered application.
- Store users' Nexus credentials on a Casualties Hub server or use a credential
  without a user-initiated action.
- Rehost mods, mod files, or protected Nexus content without permission.
- Automate unusually high-volume access or attempt to evade throttling.

Use documented APIs only when authorized. Ordinary users must be sent to the original
Nexus page whenever direct API downloading is unavailable or unauthorized. Nexus Mods'
current Terms of Service and API Acceptable Use Policy take precedence over assumptions
in this repository.

Changes involving Nexus authentication, API requests, direct downloads, metadata, or
rate limiting require maintainer approval. Public-facing API functionality should be
registered with or approved by Nexus Mods when their policies require it.

## User safety and privacy

- Collect the minimum data necessary and document it accurately.
- Never log secrets or include them in diagnostics.
- Require explicit confirmation for broad deletion or replacement operations.
- Validate archive paths and remote data before writing to disk or executing files.
- Treat update, installer, uninstaller, and remote-content code as security-sensitive.

## Official releases and branding

Only releases published by MarlyZ89 through the project's declared official channels
are official Casualties Hub builds. Modified forks must not claim endorsement or use
official update services in a way that confuses users about their origin.

Commits and branches are development history; they are not releases. Publishing a
release requires an intentional version tag, release notes, packaged artifacts, and
maintainer approval.

## Contributions

Contributions must follow `CONTRIBUTING.md`, preserve license notices, identify copied
or adapted material, and include only material the contributor is permitted to share.
The maintainer may decline changes that create legal, security, privacy, maintenance,
or platform-policy risk.
