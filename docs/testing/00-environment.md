 # 00 - Test Environment

## Discord side

- **Two guilds**: `Grimoire-Test-A` (main) and `Grimoire-Test-B` (isolation checks: settings and data must not leak between guilds).
- **Second bot application** is not needed; run your dev bot token locally against both guilds.
- **Accounts** (real users or alts; bots cannot trigger most user events):

| Handle | Role | Purpose |
|--------|------|---------|
| `owner` | Guild owner | Owner-bypass cases |
| `admin` | Administrator | Full permission path |
| `mod` | Custom role with Ban/Kick/ManageMessages/ManageRoles only | Granular permission checks |
| `user1`, `user2` | No permissions | Targets, XP gain, spam |
| `higher` | Role above the bot's top role | "Bot cannot act on target" cases |
| `pk` | Account with a PluralKit system (optional) | Proxy handling |

- **Role hierarchy** (top to bottom): `higher`, Bot role, `admin`, `mod`, Muted role, everyone.
- **Channels**: `#general`, `#spam`, `#locktest`, `#modlog`, `#userlog`, `#messagelog`, `#alerts`, `#levelup`, `#ignored`, `#private` (hidden from everyone but admin), one forum/thread channel.
- Enable Developer Mode; keep a note of guild/channel/role/user IDs in `ids.local.md` (gitignored).

## Bot side

1. `docker-compose up -d db` (PostgreSQL only) and set `ConnectionStrings__Grimoire`.
2. `dotnet run --project Grimoire` with `guildId` set to Test-A so experimental commands register instantly.
3. Global command registration can take a while: for iteration, rely on guild-scoped registration where the code allows it.
4. Set `channelId` to `#alerts` so error output is visible.

## Reset between areas

```bash
# wipe data, keep schema (adjust names to your compose file)
docker compose exec db psql -U <user> -d <db> -c 'DROP SCHEMA public CASCADE; CREATE SCHEMA public;'
dotnet ef database update --project Grimoire.Data --startup-project Grimoire
```

Note: materialized views (leaderboards) must be refreshed manually after bulk data changes. Settings
are cached (`IHybridCache`): after editing rows directly in SQL, restart the bot.

## Observability while testing

- Bot console log at Debug level for `Grimoire.*`.
- `psql` open in a second terminal; each case's "Expected DB" column names the table to check.
- `#alerts` channel for the alert service.
- Discord audit log for confirming real kicks, bans, role and permission changes.

## Timing helpers

Background services tick at: Lock/Mute 5 s, Watchdog 30 s, Warning flush 30 s, Cleanup 1 min,
Daily report 1 min poll, Leaderboard refresh 15 min. Use short durations (1-2 min) for expiry
cases and note the tick interval when asserting.

## Standard preflight (run at the start of every area)

- [ ] Bot online, slash commands visible in Test-A and Test-B
- [ ] All modules enabled in Test-A (`/Modules View`)
- [ ] `/Diagnostics Check` reports no guild issues
- [ ] Log channels configured (or intentionally unset for the case)
