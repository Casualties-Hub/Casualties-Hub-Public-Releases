-- Casualties Hub v0.0.6-pre.5 migration.
-- Run this once AFTER migrations 04 and 05.
-- It keeps the existing anonymous IP-side rate limit, raises it to eight
-- requests per rolling hour, and adds anonymous UUID-based activity counts.

create schema if not exists private;

-- One row per anonymous Hub installation. This is NOT an account system and
-- does not store usernames, game paths, mod lists, email addresses, or IPs.
create table if not exists private.hub_installations (
  installation_id uuid primary key,
  first_seen_at timestamptz not null default now(),
  last_seen_at timestamptz not null default now()
);

create index if not exists hub_installations_last_seen_idx
on private.hub_installations (last_seen_at desc);

revoke all on table private.hub_installations from public, anon, authenticated;

-- Replace the previous public status request guard. A single IP may make up
-- to eight status requests in any rolling hour. This is the server-side
-- authority; app-side cooldowns are only a friendly extra safeguard.
create or replace function public.check_request()
returns void
language plpgsql
security definer
set search_path = public, private, pg_temp
as $$
declare
  req_path text := current_setting('request.path', true);
  req_method text := current_setting('request.method', true);
  req_role text := coalesce(current_setting('request.jwt', true)::json->>'role', 'anon');
  req_ip_text text := split_part(coalesce(current_setting('request.headers', true)::json->>'x-forwarded-for', ''), ',', 1);
  req_ip inet;
  requests_in_last_hour integer;
begin
  if req_role = 'service_role' or req_path <> 'rpc/get_hub_status' or req_method <> 'POST' then
    return;
  end if;

  begin
    req_ip := req_ip_text::inet;
  exception when others then
    raise sqlstate 'PGRST' using
      message = json_build_object('message', 'Unable to determine request IP address.')::text,
      detail = json_build_object('status', 400, 'status_text', 'Bad Request')::text;
  end;

  delete from private.hub_status_rate_limits where request_at < now() - interval '2 hours';

  select count(*) into requests_in_last_hour
  from private.hub_status_rate_limits
  where ip = req_ip and request_at >= now() - interval '1 hour';

  if requests_in_last_hour >= 8 then
    raise sqlstate 'PGRST' using
      message = json_build_object('message', 'Hub status request limit reached. Please use saved data and retry later.')::text,
      detail = json_build_object('status', 429, 'status_text', 'Too Many Requests')::text;
  end if;

  insert into private.hub_status_rate_limits (ip, request_at) values (req_ip, now());
end;
$$;

-- Replace the existing RPC so old Hub builds can still call it with no
-- arguments, while Pre.5 sends p_installation_id to update aggregate activity.
drop function if exists public.get_hub_status();

create function public.get_hub_status(p_installation_id uuid default null)
returns table (
  announcement text,
  announcement_id text,
  updated_at timestamptz,
  compatibility_rules text,
  compatibility_version text,
  active_users_last_two_hours integer,
  active_users_last_day integer,
  active_users_last_week integer
)
language plpgsql
security definer
set search_path = public, private, pg_temp
as $$
declare
  last_seen timestamptz;
begin
  if p_installation_id is not null then
    select last_seen_at into last_seen
    from private.hub_installations
    where installation_id = p_installation_id;

    -- An installation may be counted/request status no more often than every
    -- 7.5 minutes. The IP guard above stops an attacker cycling random IDs.
    if last_seen is not null and last_seen > now() - interval '7 minutes 30 seconds' then
      raise sqlstate 'PGRST' using
        message = json_build_object('message', 'This Hub installation may check again in 7.5 minutes.')::text,
        detail = json_build_object('status', 429, 'status_text', 'Too Many Requests')::text;
    end if;

    insert into private.hub_installations (installation_id, first_seen_at, last_seen_at)
    values (p_installation_id, now(), now())
    on conflict (installation_id) do update set last_seen_at = excluded.last_seen_at;

    -- Keep the activity table bounded. Thirty days is more than enough for
    -- the Hub's 2-hour, 24-hour, and 7-day aggregate counters.
    delete from private.hub_installations where last_seen_at < now() - interval '30 days';
  end if;

  return query
  select
    left(h.announcement, 500),
    h.announcement_id,
    h.updated_at,
    left(h.compatibility_rules, 30000),
    h.compatibility_version,
    (select count(*)::integer from private.hub_installations where last_seen_at >= now() - interval '2 hours'),
    (select count(*)::integer from private.hub_installations where last_seen_at >= now() - interval '24 hours'),
    (select count(*)::integer from private.hub_installations where last_seen_at >= now() - interval '7 days')
  from public.hub_status h
  where h.singleton = true;
end;
$$;

revoke all on function public.get_hub_status(uuid) from public;
grant execute on function public.get_hub_status(uuid) to anon, authenticated;

alter role authenticator set pgrst.db_pre_request = 'public.check_request';
notify pgrst, 'reload config';
