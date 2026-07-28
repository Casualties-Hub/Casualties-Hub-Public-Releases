-- Save this query in Supabase as: CH - View Request Totals

select
  count(*) filter (where request_at >= now() - interval '1 hour') as requests_last_hour,
  count(*) filter (where request_at >= now() - interval '24 hours') as requests_last_24_hours
from private.hub_status_rate_limits;
