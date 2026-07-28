# Casualties Hub Supabase Setup - Pre.6

This folder replaces the older scattered Supabase setup files.

## For a new or existing Casualties Hub Supabase project

1. Open `01 - Complete Hub Setup.sql`.
2. In Supabase, open **SQL Editor** and click **New query**.
3. Paste the whole file and click **Run**.
4. Accept Supabase's warning. It is expected because the script replaces the public status function; it does not delete your announcement or compatibility data.
5. When it reports success, use the separate saved-query files below.

Read `03 - Plain English Guide.md` before sharing Pre.6 with testers.

## Save these Supabase queries after the setup succeeds

| Save this Supabase query name | Copy this file into it | What it does |
| --- | --- | --- |
| `Casualties Hub Pre.6 Complete Setup` | `01 - Complete Hub Setup.sql` | One-time server setup. Run once. |
| `CH - Send or Change Announcement` | `02 - Send or Change Announcement.sql` | Sends a new announcement after you replace its text and ID. |
| `CH - Update Compatibility and Known Bugs` | `03 - Update Compatibility and Known Bugs.sql` | Changes conflicts, known bugs, and special-install notes. |
| `CH - View Activity Counts` | `04 - View Activity Counts.sql` | Shows aggregate active installations. |
| `CH - View Request Totals` | `05 - View Request Totals.sql` | Shows aggregate request totals. |

Do not run the announcement or compatibility templates until you replace their example values.

## What this stores

- The current announcement and compatibility text you choose.
- A random anonymous installation UUID plus first-seen and last-seen timestamps.
- Short-lived IP rate-limit timestamps.

It does **not** store player names, emails, game folders, mod lists, protected assets, or Discord accounts.
