-- =============================================================================
-- seed_migration_test_data.sql
--
-- Purpose : Insert test rows into every public-schema table that the
--           Settings.Initial migration copies from. Run this BEFORE applying
--           the new migrations (ModelRedesign, DropOrphanedCustomCommandGuildFKs,
--           Settings.Initial) so you can verify that all data paths migrate
--           correctly.
--
-- Safe to run on a prod backup — uses ON CONFLICT DO NOTHING throughout and
-- a sentinel guild ID (111000111000111001) that is astronomically unlikely to
-- collide with real Discord snowflakes.
--
-- To clean up afterwards:
--   DELETE FROM public."Guilds" WHERE "Id" = 111000111000111001;
--   (cascades to all child rows via existing FKs)
-- =============================================================================

DO $$
DECLARE
    -- Sentinel Discord-like snowflakes used only for this test
    g_guild      CONSTANT numeric := 111000111000111001;

    -- Channels that require real FK entries in public."Channels"
    g_chan_ignored  CONSTANT numeric := 222000222000222001; -- for IgnoredChannels
    g_chan_lock     CONSTANT numeric := 222000222000222002; -- for Locks
    g_chan_msglog   CONSTANT numeric := 222000222000222003; -- for MessagesLogChannelOverrides
    g_chan_spam     CONSTANT numeric := 222000222000222004; -- for SpamFilterOverrides

    -- Channel IDs stored only as values (no FK in Settings schema)
    g_mod_log_chan    CONSTANT numeric := 200900200900200901;
    g_user_cmd_chan   CONSTANT numeric := 200800200800200801;
    g_lvl_log_chan    CONSTANT numeric := 200700200700200701;
    g_del_log_chan    CONSTANT numeric := 200600200600200601;
    g_bulk_log_chan   CONSTANT numeric := 200500200500200501;
    g_edit_log_chan   CONSTANT numeric := 200400200400200401;
    g_pub_ban_chan    CONSTANT numeric := 200300200300200301;
    g_join_chan       CONSTANT numeric := 200200200200200201;
    g_leave_chan      CONSTANT numeric := 200100200100200101;
    g_uname_chan      CONSTANT numeric := 200050200050200051;
    g_nick_chan       CONSTANT numeric := 200040200040200041;
    g_avatar_chan     CONSTANT numeric := 200030200030200031;

    g_user       CONSTANT numeric := 333000333000333001; -- member / moderator / mute target
    g_mute_role  CONSTANT numeric := 444000444000444001; -- mute role
    g_rwrd_role  CONSTANT numeric := 444000444000444002; -- level-10 reward role
BEGIN

    -- -------------------------------------------------------------------------
    -- 1. Base entities required by FK constraints
    -- -------------------------------------------------------------------------

    -- Users has no FKs, just an Id PK
    INSERT INTO public."Users" ("Id")
    VALUES (g_user)
    ON CONFLICT DO NOTHING;

    -- Guilds.ModChannelLog / UserCommandChannelId have FK constraints back to
    -- Channels, but Channels also FK to Guilds — circular dependency.
    -- Break the cycle: insert Guild with NULLs first, create the channels,
    -- then back-fill the channel references.
    INSERT INTO public."Guilds" ("Id", "ModChannelLog", "UserCommandChannelId")
    VALUES (g_guild, NULL, NULL)
    ON CONFLICT DO NOTHING;

    -- Channels that are FK-referenced by Locks, IgnoredChannels, overrides,
    -- and (after the UPDATE below) the Guild itself.
    -- Note: IsXpIgnored was dropped from Channels in Message-Log-Overrides migration.
    INSERT INTO public."Channels" ("Id", "GuildId")
    VALUES
        (g_chan_ignored,  g_guild),
        (g_chan_lock,     g_guild),
        (g_chan_msglog,   g_guild),
        (g_chan_spam,     g_guild),
        (g_mod_log_chan,  g_guild),
        (g_user_cmd_chan, g_guild)
    ON CONFLICT DO NOTHING;

    -- Now we can safely set the nullable channel references on the Guild row.
    UPDATE public."Guilds"
    SET "ModChannelLog"      = g_mod_log_chan,
        "UserCommandChannelId" = g_user_cmd_chan
    WHERE "Id" = g_guild
      AND "ModChannelLog" IS NULL;  -- skip if another run already set them

    -- Roles for MuteRole setting and Rewards
    -- Note: IsXpIgnored was dropped from Roles in Message-Log-Overrides migration.
    INSERT INTO public."Roles" ("Id", "GuildId")
    VALUES
        (g_mute_role, g_guild),
        (g_rwrd_role, g_guild)
    ON CONFLICT DO NOTHING;

    -- Members (FK to Users + Guilds); used by Locks, IgnoredMembers, Mutes
    -- Note: IsXpIgnored was dropped from Members in Message-Log-Overrides migration.
    INSERT INTO public."Members" ("UserId", "GuildId")
    VALUES (g_user, g_guild)
    ON CONFLICT DO NOTHING;

    -- -------------------------------------------------------------------------
    -- 2. Guild-settings source tables
    --    All modules enabled + all optional channels set so every conditional
    --    INSERT in the migration is exercised.
    -- -------------------------------------------------------------------------

    INSERT INTO public."GuildCommandsSettings" ("GuildId", "ModuleEnabled")
    VALUES (g_guild, true)
    ON CONFLICT DO NOTHING;

    -- Optional log-channel columns (LevelChannelLogId, Delete/BulkDelete/EditChannelLogId,
    -- PublicBanLog, JoinChannelLogId …) all FK back to public."Channels".
    -- Setting them NULL keeps the seed self-contained; the migration guards each
    -- with WHERE … IS NOT NULL so those paths are simply skipped — the important
    -- paths (module flags, interval conversions, scalar values) are still exercised.

    -- Use non-default values for Base (≠15), Modifier (≠50), Amount (≠5) so the
    -- migration's WHERE <> default guards are exercised and these rows ARE migrated.
    INSERT INTO public."GuildLevelSettings"
        ("GuildId", "ModuleEnabled", "TextTime", "Base", "Modifier", "Amount", "LevelChannelLogId")
    VALUES
        (g_guild, true, INTERVAL '5 minutes', 20, 60, 8, NULL)
    ON CONFLICT DO NOTHING;

    INSERT INTO public."GuildMessageLogSettings"
        ("GuildId", "ModuleEnabled", "DeleteChannelLogId", "BulkDeleteChannelLogId", "EditChannelLogId")
    VALUES
        (g_guild, true, NULL, NULL, NULL)
    ON CONFLICT DO NOTHING;

    -- AutoPardonAfter = 365 days → should migrate as "365.00:00:00"
    -- MuteRole references public."Roles" (already inserted above); PublicBanLog → NULL
    INSERT INTO public."GuildModerationSettings"
        ("GuildId", "ModuleEnabled", "AntiSpamEnabled", "PublicBanLog", "AutoPardonAfter", "MuteRole")
    VALUES
        (g_guild, true, true, NULL, INTERVAL '365 days', g_mute_role)
    ON CONFLICT DO NOTHING;

    INSERT INTO public."GuildUserLogSettings"
        ("GuildId", "ModuleEnabled", "JoinChannelLogId", "LeaveChannelLogId",
         "UsernameChannelLogId", "NicknameChannelLogId", "AvatarChannelLogId")
    VALUES
        (g_guild, true, NULL, NULL, NULL, NULL, NULL)
    ON CONFLICT DO NOTHING;

    -- -------------------------------------------------------------------------
    -- 3. XP-ignore source tables
    -- -------------------------------------------------------------------------

    INSERT INTO public."IgnoredChannels" ("ChannelId", "GuildId")
    VALUES (g_chan_ignored, g_guild)
    ON CONFLICT DO NOTHING;

    -- IgnoredMembers PK is UserId (not composite), so one row per user globally
    INSERT INTO public."IgnoredMembers" ("UserId", "GuildId")
    VALUES (g_user, g_guild)
    ON CONFLICT DO NOTHING;

    INSERT INTO public."IgnoredRoles" ("RoleId", "GuildId")
    VALUES (g_mute_role, g_guild)
    ON CONFLICT DO NOTHING;

    -- -------------------------------------------------------------------------
    -- 4. Locks
    --    ChannelId FK → Channels, (ModeratorId, GuildId) FK → Members
    -- -------------------------------------------------------------------------

    INSERT INTO public."Locks"
        ("ChannelId", "GuildId", "ModeratorId", "Reason",
         "PreviouslyAllowed", "PreviouslyDenied", "EndTime")
    VALUES
        (g_chan_lock, g_guild, g_user, 'Test lock for migration verification',
         0, 1024, NOW() + INTERVAL '1 hour')
    ON CONFLICT DO NOTHING;

    -- -------------------------------------------------------------------------
    -- 5. Mutes
    --    SinId is GENERATED ALWAYS AS IDENTITY so we INSERT a Sin first and
    --    capture its Id.  Guard prevents duplicate mutes for the same user.
    -- -------------------------------------------------------------------------

    IF NOT EXISTS (
        SELECT 1 FROM public."Mutes"
        WHERE "UserId" = g_user AND "GuildId" = g_guild
    ) THEN
        WITH new_sin AS (
            INSERT INTO public."Sins"
                ("UserId", "GuildId", "Reason", "SinOn", "SinType")
            VALUES
                (g_user, g_guild, 'Test mute sin for migration verification',
                 NOW(), 1 /* Mute */)
            RETURNING "Id"
        )
        INSERT INTO public."Mutes" ("SinId", "UserId", "GuildId", "EndTime")
        SELECT "Id", g_user, g_guild, NOW() + INTERVAL '2 hours'
        FROM new_sin;
    END IF;

    -- -------------------------------------------------------------------------
    -- 6. Rewards
    --    RoleId FK → Roles, GuildId FK → Guilds
    -- -------------------------------------------------------------------------

    INSERT INTO public."Rewards" ("RoleId", "GuildId", "RewardLevel", "RewardMessage")
    VALUES (g_rwrd_role, g_guild, 10, 'Congratulations on reaching level 10!')
    ON CONFLICT DO NOTHING;

    -- -------------------------------------------------------------------------
    -- 7. Channel overrides
    --    ChannelOption stored as int: 0 = AlwaysLog/AlwaysFilter, 1 = NeverLog/NeverFilter
    -- -------------------------------------------------------------------------

    -- AlwaysLog (0) → should migrate as 'AlwaysLog'
    INSERT INTO public."MessagesLogChannelOverrides" ("ChannelId", "GuildId", "ChannelOption")
    VALUES (g_chan_msglog, g_guild, 0)
    ON CONFLICT DO NOTHING;

    -- NeverFilter (1) → should migrate as 'NeverFilter'
    INSERT INTO public."SpamFilterOverrides" ("ChannelId", "GuildId", "ChannelOption")
    VALUES (g_chan_spam, g_guild, 1)
    ON CONFLICT DO NOTHING;

    RAISE NOTICE 'Seed data ready for test guild %', g_guild;
END $$;
