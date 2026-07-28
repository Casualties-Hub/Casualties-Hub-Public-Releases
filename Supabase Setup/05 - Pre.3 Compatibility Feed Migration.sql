-- Casualties Hub v0.0.6-pre.3 migration.
-- Run this once after the Pre.2 rate-limited announcement migration.
-- It extends the existing one-row RPC response; it does NOT add another public endpoint.

alter table public.hub_status
  add column if not exists compatibility_rules text not null default
  '# type | mod A | mod B-or-dash | severity | player-facing message\n'
  '# conflict | cucorelib | rshlib | critical | These libraries cannot be enabled together.\n'
  '# bug | example-mod | - | warning | Describe a confirmed known issue here.\n'
  '# manual | multiplayer | - | info | Install this mod into the game folder.';

alter table public.hub_status
  add column if not exists compatibility_version text not null default 'initial';

create or replace function public.get_hub_status()
returns table (
  announcement text,
  announcement_id text,
  updated_at timestamptz,
  compatibility_rules text,
  compatibility_version text
)
language sql
security definer
set search_path = public, pg_temp
as $$
  select left(h.announcement, 500), h.announcement_id, h.updated_at,
         left(h.compatibility_rules, 30000), h.compatibility_version
  from public.hub_status h
  where h.singleton = true;
$$;

revoke all on function public.get_hub_status() from public;
grant execute on function public.get_hub_status() to anon, authenticated;
notify pgrst, 'reload config';
