-- Save this query in Supabase as: CH - View Activity Counts

select
  count(*) filter (where last_seen_at >= now() - interval '2 hours') as active_last_2_hours,
  count(*) filter (where last_seen_at >= now() - interval '24 hours') as active_last_24_hours,
  count(*) filter (where last_seen_at >= now() - interval '7 days') as active_last_7_days
from private.hub_installations;
