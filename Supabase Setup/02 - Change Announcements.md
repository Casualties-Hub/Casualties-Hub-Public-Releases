# Supabase announcement setup

1. Complete `00 - START HERE - v0.0.6-pre.2.md` first.
2. To publish a new announcement, use **Table Editor** -> `hub_status` -> edit the only row.
4. Change all three values together:
   - `announcement`: the message players see.
   - `announcement_id`: a new simple ID, for example `2026-07-26-pre-1`.
   - `updated_at`: set it to the current time.

The Hub stores only the newest announcement on each player's PC. If the service is offline, it shows that single saved announcement. A successful new request replaces the old saved one.

## Security

- The app uses the Supabase **publishable** key only.
- Never put a `service_role` or secret key in Casualties Hub, GitHub, Discord, screenshots, or chat.
- Keep the table public **read only**. There is no reason for a player to have database write access.
