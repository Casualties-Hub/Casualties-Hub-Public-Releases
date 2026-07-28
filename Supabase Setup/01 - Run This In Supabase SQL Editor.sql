-- Casualties Hub public status / announcement row.
-- Run this once in Supabase: SQL Editor -> New query -> paste -> Run.
-- This table is deliberately PUBLIC READ ONLY. The Windows app cannot change it.

create table if not exists public.hub_status (
  singleton boolean primary key default true check (singleton = true),
  announcement text not null default 'Welcome to Casualties Hub.',
  announcement_id text not null default 'initial',
  updated_at timestamptz not null default now()
);

alter table public.hub_status enable row level security;

drop policy if exists "Everyone can read the public Hub status" on public.hub_status;
create policy "Everyone can read the public Hub status"
on public.hub_status
for select
to anon, authenticated
using (true);

-- There is intentionally no INSERT, UPDATE, or DELETE policy for anon/authenticated.
-- Only you, in the Supabase dashboard, can change the announcement.

insert into public.hub_status (singleton, announcement, announcement_id)
values (true, 'Welcome to Casualties Hub.', 'initial')
on conflict (singleton) do nothing;
