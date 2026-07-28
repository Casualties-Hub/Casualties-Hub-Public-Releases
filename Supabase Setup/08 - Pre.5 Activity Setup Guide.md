# Casualties Hub Pre.5 — Activity Counts and Check Limits

Run `07 - Pre.5 Activity and Check Limits Migration.sql` once in the Supabase SQL Editor after the Pre.2 and Pre.3 migrations.

## What it changes

- The Hub still checks automatically every 30 minutes.
- The Hub's **Check now** button has a 15-minute local cooldown.
- Supabase allows up to 8 public status requests per IP in a rolling hour.
- A single anonymous installation UUID cannot request status more than once every 7.5 minutes.
- Pre.5 sends a locally generated UUID only when Online Services makes a normal status request.
- Supabase stores only that UUID plus `first_seen_at` and `last_seen_at`.
- Supabase returns aggregate activity counts for the previous 2 hours, 24 hours, and 7 days.

Older Hub versions continue to receive announcements because `get_hub_status` accepts an empty request. They simply do not add to the anonymous activity count.

## Run the migration

1. Open **Supabase Dashboard** → your Casualties Hub project → **SQL Editor**.
2. Click **New query**.
3. Open `07 - Pre.5 Activity and Check Limits Migration.sql` in Notepad.
4. Copy every line, paste it into Supabase, and click **Run**.
5. Accept Supabase's destructive-operation warning. It is expected because the migration replaces the old RPC function; it does not delete announcements or compatibility data.
6. Wait for **Success. No rows returned**.

## View the counts as the owner

Run this in the SQL Editor whenever you want the current aggregate totals:

```sql
select
  count(*) filter (where last_seen_at >= now() - interval '2 hours') as active_last_2_hours,
  count(*) filter (where last_seen_at >= now() - interval '24 hours') as active_last_24_hours,
  count(*) filter (where last_seen_at >= now() - interval '7 days') as active_last_7_days
from private.hub_installations;
```

The Hub only shows the aggregate numbers. It never shows an installation list to players.
