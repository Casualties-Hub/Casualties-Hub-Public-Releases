# Casualties Hub Supabase Guide - Pre.6

You do not install a program for Supabase. You run the setup SQL once in your Supabase project's SQL Editor.

## One-time setup

1. Go to your Supabase project.
2. Click **SQL Editor** in the left menu.
3. Click **New query**.
4. Open `01 - Complete Hub Setup.sql` on your PC.
5. Copy everything in that file, paste it into the query, and click **Run**.
6. If Supabase warns about destructive changes, choose **Run query**. The script is replacing the old status function so Pre.6 can use the new format. It does not delete your announcement or compatibility data.
7. Wait for `Success. No rows returned`.

## What Pre.6 does

- The Hub makes normal automatic checks every 30 minutes.
- A user can press Check now once every 15 minutes.
- The server refuses the same anonymous installation UUID for 7 minutes 30 seconds after a successful request.
- The server also allows at most eight public requests per IP address per rolling hour.
- The server counts anonymous installations seen in the last 2 hours, 24 hours, and 7 days.

The UUID is randomly created on the user's PC. It is not a login and is not tied to their name, email, game folder, Discord, or mods.

## Saved query names

After setup, save these queries in the Supabase SQL Editor. The names are only for your own organization; they do not affect the Hub.

1. `Casualties Hub Pre.6 Complete Setup` - the one-time full setup query.
2. `CH - Send or Change Announcement` - sends a new announcement.
3. `CH - Update Compatibility and Known Bugs` - changes server rules.
4. `CH - View Activity Counts` - shows 2-hour, 24-hour, and 7-day totals.
5. `CH - View Request Totals` - shows recent total request counts.

## Send an announcement

1. Open `02 - Send or Change Announcement.sql`.
2. Find **SEND OR CHANGE AN ANNOUNCEMENT**.
3. Replace the announcement text and the `announcement_id`.
4. Run only that command in Supabase.

Use a new `announcement_id` whenever you want the Hub to recognize a new announcement and save it in local history.

## Send compatibility or known-bug information

Use `03 - Update Compatibility and Known Bugs.sql`. The Hub saves the newest server data locally and continues to use it if the online service is later unavailable.

## View activity

Run `04 - View Activity Counts.sql` or `05 - View Request Totals.sql`. The results are aggregate counts only.
