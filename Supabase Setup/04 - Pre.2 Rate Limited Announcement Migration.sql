-- Casualties Hub v0.0.6-pre.2 migration.
-- Run this once AFTER the original hub_status setup query has completed.
-- It removes public table reads and exposes only a small rate-limited RPC response.

create schema if not exists private;

create table if not exists private.hub_status_rate_limits (
  ip inet not null,
  request_at timestamptz not null default now()
);

create index if not exists hub_status_rate_limits_ip_time_idx
on private.hub_status_rate_limits (ip, request_at desc);

-- Close the old direct public GET endpoint. You still retain full owner access
-- through the Supabase dashboard/Table Editor.
drop policy if exists "Everyone can read the public Hub status" on public.hub_status;
revoke select on table public.hub_status from anon, authenticated;

-- The only public announcement route: it returns one row and caps the response
-- at 500 characters. SECURITY DEFINER lets it read the protected table while
-- callers receive no direct table privileges.
create or replace function public.get_hub_status()
returns table (
  announcement text,
  announcement_id text,
  updated_at timestamptz
)
language sql
security definer
set search_path = public, pg_temp
as $$
  select left(h.announcement, 500), h.announcement_id, h.updated_at
  from public.hub_status h
  where h.singleton = true;
$$;

revoke all on function public.get_hub_status() from public;
grant execute on function public.get_hub_status() to anon, authenticated;

-- Supabase runs this before Data API requests. It only applies to the Hub's
-- POST /rpc/get_hub_status endpoint, so it does not interfere with other APIs.
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
  -- Dashboard/service-role calls keep working, and only the public Hub RPC is limited.
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

  -- Self-cleaning storage: only roughly the current hour is retained.
  delete from private.hub_status_rate_limits where request_at < now() - interval '2 hours';

  select count(*) into requests_in_last_hour
  from private.hub_status_rate_limits
  where ip = req_ip and request_at >= now() - interval '1 hour';

  if requests_in_last_hour >= 4 then
    raise sqlstate 'PGRST' using
      message = json_build_object('message', 'Announcement request limit reached. Please use the saved announcement and retry later.')::text,
      detail = json_build_object('status', 429, 'status_text', 'Too Many Requests')::text;
  end if;

  insert into private.hub_status_rate_limits (ip, request_at) values (req_ip, now());
end;
$$;

-- Register the request check with the Supabase Data API, then reload its config.
alter role authenticator set pgrst.db_pre_request = 'public.check_request';
notify pgrst, 'reload config';
