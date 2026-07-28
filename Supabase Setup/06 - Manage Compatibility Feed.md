# Pre.3 compatibility and known-bug feed

After running `05 - Pre.3 Compatibility Feed Migration.sql`, edit the one `hub_status` row in Supabase Table Editor.

- `compatibility_version`: change this every time you change the rules, for example `2026-07-26-1`.
- `compatibility_rules`: paste lines in this format:

```text
# Lines beginning with # are notes and are ignored by the Hub.
# type | mod A | mod B-or-dash | severity | message
conflict | cucorelib | rshlib | critical | These libraries cannot be enabled together.
conflict | mod-a | mod-b | warning | These mods are known to conflict.
bug | exp-sneeze | - | warning | May fail to load on the newest game version.
manual | multiplayer | - | info | Install into the Casualties Unknown game folder.
```

Use metadata IDs or the mod’s displayed name where possible. `conflict` entries appear in the **Incompatibility** Local Mods view and show the other mod. `bug` entries appear in **Known Bugs**. `manual` is reserved for later install guidance.

The Hub saves the last valid rule text locally. It replaces that cache only after a successful later server response, so users retain the last rules while offline.
