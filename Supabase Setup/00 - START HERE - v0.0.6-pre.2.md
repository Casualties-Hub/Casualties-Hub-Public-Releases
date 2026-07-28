# Casualties Hub Supabase setup - v0.0.6-pre.2

## If you already ran the original setup

Run only this file in Supabase SQL Editor:

`04 - Pre.2 Rate Limited Announcement Migration.sql`

Then restart Casualties Hub. The status circle should go green after the first successful request.

## If this is a brand-new Supabase project

1. Run `01 - Run This In Supabase SQL Editor.sql`.
2. Run `04 - Pre.2 Rate Limited Announcement Migration.sql`.
3. Do not run the old manual `grant select on public.hub_status` command. The migration intentionally removes public table reads.

## What changed

- The Hub no longer reads `hub_status` directly.
- The Hub sends a POST request to `get_hub_status`.
- Supabase accepts a maximum of four requests from one IP address in a rolling hour.
- Supabase sends no more than 500 announcement characters per request.
- The app itself also waits 30 minutes between normal network checks and relies on its local saved announcement in between.

## Testing

Use the Developer Console menu options 10-12 to test a live status request, a local rate-limit fallback, and eligible GitHub update discovery.

## Do not expose secrets

The Hub contains a publishable key only. Never add a Supabase service-role/secret key to the Hub, GitHub, a release ZIP, or chat.
# Pre.5 add-on: activity counts and check limits

If you are deploying Casualties Hub `v0.0.6-pre.5` or later, run
`07 - Pre.5 Activity and Check Limits Migration.sql` **after** the earlier
Pre.2 and Pre.3 migrations. Then follow
`08 - Pre.5 Activity Setup Guide.md` to verify the aggregate user counts.
