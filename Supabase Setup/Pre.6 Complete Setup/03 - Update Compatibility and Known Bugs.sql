-- Save this query in Supabase as: CH - Update Compatibility and Known Bugs
-- Replace compatibility_version and compatibility_rules before clicking Run.
-- Keep the first line as the header. Each later line is one rule.

update public.hub_status
set compatibility_version = 'YYYY-MM-DD-01',
    compatibility_rules = E'# type | mod A | mod B-or-dash | severity | player-facing message\nconflict | cucorelib | rshlib | critical | These libraries cannot be enabled together.\nbug | example-mod | - | warning | Example known bug message.',
    updated_at = now()
where singleton = true;
