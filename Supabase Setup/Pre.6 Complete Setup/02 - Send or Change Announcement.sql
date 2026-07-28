-- Save this query in Supabase as: CH - Send or Change Announcement
-- Replace BOTH quoted values before clicking Run.
-- Use a new announcement_id whenever you want clients to treat it as new.

update public.hub_status
set announcement = 'Replace this with your announcement text.',
    announcement_id = 'announcement-YYYY-MM-DD-01',
    updated_at = now()
where singleton = true;
