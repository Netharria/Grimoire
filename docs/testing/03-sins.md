# 03 - Sins

Scope: `/SinLog`, `/Pardon`, `/Forget`, `/Reason`, `/ModSettings` (View, PublicBanLog, AutoPardon), and `/Publish`
(Ban, Unban, Kick). This covers viewing, pardoning and editing the sin records that area 02's Ban, Kick, Warn and
Mute create.

Source: `Grimoire/Features/Moderation/SinAdmin/`, `Grimoire/Features/Moderation/PublishSins/`,
`Grimoire.Settings/Services/SettingsModule.Moderation.AutoPardon.cs`.

This doc was written from the source while automating it (backlog A-14 to A-17). Each section lists the automated
test class that covers it. Do the standard preflight from `00-environment.md` first, with the mod log set to `#modlog`.

## A. `/SinLog type [user]`

Gates: `RequireGuild`, `RequireModuleEnabled(Moderation)`. Everyone sees the command. A member without Manage Messages
can only view their own log, and their reply is ephemeral. Types: Warn, Mute, Ban, Kick, All, Mod. Sins older than the
auto-pardon window are left out. Pardoned sins are shown struck through, with who pardoned them and when. A log over
about 2048 characters is split into pages.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| SIN-01 | `user1` has a warn, a mute, a ban and a kick | `mod` runs `/SinLog type:All user:user1` | Public embed "Sin log for user1"; one entry per sin: `**id : Type** : <timestamp>`, `Reason:`, `Moderator:` | none |
| SIN-02 | As SIN-01 | `type:Warn` (and Mute, Ban, Kick) | Only that type is listed | none |
| SIN-03 | `mod` has sins | `mod` runs `/SinLog type:All` with no user | `mod`'s own log | none |
| SIN-04 | `user1` has sins | `user1` (no Manage Messages) runs `/SinLog type:All` | Own log, ephemeral | none |
| SIN-05 | none | `user1` runs `/SinLog type:All user:user2` | Ephemeral red "You do not have permission to view other users' logs." | none |
| SIN-06 | `user1` has no sins | `/SinLog type:All user:user1` | "That user does not have any logs" | none |
| SIN-07 | Auto-pardon 30 days; one sin 40 days old | `/SinLog type:All user:user1` | The old sin is not listed | none |
| SIN-08 | One sin pardoned | `/SinLog type:All user:user1` | The entry is struck through, then "**Pardoned by: @mod on <timestamp>**" | none |
| SIN-09 | A sin whose reason was changed with `/Reason` | `/SinLog` | The newest reason is shown | none |
| SIN-10 | A sin with no reason, and one from a client ban with no audit actor | `/SinLog` | `Reason:` is blank; the system sin shows `Moderator: System` | none |
| SIN-11 | `mod` has issued bans, mutes and warns | `mod` runs `/SinLog type:Mod` | "Moderation log for mod" with Bans, Mutes and Warns counts. Kicks are not counted | none |
| SIN-12 | `admin` has issued sins, `mod` has not | `mod` runs `/SinLog type:Mod user:admin` | **Expected:** `admin`'s counts. **Actual:** `mod`'s own counts under `admin`'s name (KI-29) | none |
| SIN-13 | `user1` has enough sins to need two pages | `/SinLog type:All user:user1` | **Expected:** every page visible. **Actual:** each page edits the same response, so only the last page is seen (KI-30) | none |
| SIN-14 | Moderation disabled | `/SinLog` | "This module is disabled in this server." | none |
| SIN-15 | Sins in Test-A and Test-B | `/SinLog` in Test-A | Only Test-A sins (A-35) | none |

Automated in `Grimoire.Tests/Integration/Moderation/SinLogTests.cs`.

## B. `/Pardon`, `/Forget`, `/Reason`

Gates for all three: `RequireGuild`, `RequireModuleEnabled(Moderation)`, and Manage Messages, checked by the bot
(everyone sees the commands). `sin_id` is a string option that must parse to a positive number. Pardon and Reason
find the sin through the user's newest username in `UsernameHistory`. Forget deletes the sin with its reasons and
pardons.

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| SIN-20 | `user1` has a warn | `mod` runs `/Pardon sin_id:n reason:"forgiven"` | "Pardoned": `**ID:** n **User:** user1` | `Pardons` row with mod and reason. `#modlog` "Pardon" embed: User, Sin Id, Moderator, Reason |
| SIN-21 | As SIN-20 | `/Pardon sin_id:n` with no reason | **Expected:** pardoned, Reason "None". **Actual:** the command throws, because an empty reason can't be created (KI-32) | none |
| SIN-22 | none | `/Pardon sin_id:999` | "Could not find a sin with that ID." | none |
| SIN-23 | A sin in Test-B | `/Pardon` its id in Test-A | "Could not find a sin with that ID." | none |
| SIN-24 | A sin for a user with no username history (banned without ever joining) | `/Pardon` it | **Expected:** pardoned. **Actual:** "Could not find a sin with that ID." (KI-33) | none |
| SIN-25 | A pardoned sin | `/Pardon` it again | Pardoned again; there is no "already pardoned" check | A second `Pardons` row |
| SIN-26 | none | `sin_id:abc` or `sin_id:0` | Argument error; nothing pardoned | none |
| SIN-27 | none | `user1` (no Manage Messages) runs `/Pardon`, `/Forget` or `/Reason` | "You do not have the required permissions to use this command." | none |
| SIN-28 | Moderation disabled | Run each | "This module is disabled in this server." | none |
| SIN-30 | A pardoned sin with two reasons | `mod` runs `/Forget sin_id:n` | "Forgot": `**ID:** n **User:** user1` | Sin, its reasons and pardons deleted. `#modlog` embed "<bot nickname> has been commanded to forget." with User, Sin Id, Moderator |
| SIN-31 | none | `/Forget sin_id:999` | "Could not find a sin with that ID." | none |
| SIN-32 | Bot has no nickname | Check SIN-30's log title | Starts with a space: " has been commanded to forget." (KI-34) | none |
| SIN-40 | `user1` has a warn | `mod` runs `/Reason sin_id:n reason:"updated"` | "Reason Updated": Id, User, Reason | New `SinReasonHistory` row; `/SinLog` shows "updated". `#modlog` "@mod updated reason to updated for **ID:** n **User:** user1" |
| SIN-41 | none | `/Reason sin_id:999 reason:"x"` | Error "Could not find a sin with that ID." | none |
| SIN-42 | As SIN-24 | `/Reason` that sin | **Expected:** updated. **Actual:** "Could not find a sin with that ID." (KI-33) | none |
| SIN-43 | none | Reason over 1000 characters on `/Pardon` or `/Reason` | Discord's client rejects it | none |

Automated in `Grimoire.Tests/Integration/Moderation/SinAdminTests.cs`.

## C. `/ModSettings`

Gates: `RequireGuild`, `RequireModuleEnabled(Moderation)`, and Manage Guild, checked by the bot. View and PublicBanLog
reply ephemerally. AutoPardon durations are Days, Months (30 days) or Years (365 days). Zero means "use the default",
which is 10950 days (30 years).

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| SIN-50 | Fresh guild | `admin` runs `/ModSettings View` | Ephemeral "Current moderation System Settings": Module Enabled True, Auto Pardon Duration "30 years", Ban Log None | none |
| SIN-51 | Auto-pardon 60 days, ban log `#general` | View | "2 months" and `#general` | none |
| SIN-52 | none | `/ModSettings AutoPardon duration_type:Months duration_amount:3` | "Will now auto pardon sins after 3 Months" | Setting 90 days; `#modlog` "@admin updated auto pardon to 3 Months" |
| SIN-53 | none | AutoPardon with amount 0 | Reply says "after 0 Days"; View still shows "30 years" (zero falls back to the default) | Setting 0 |
| SIN-54 | none | AutoPardon Years with a very large amount | **Expected:** a clear refusal. **Actual:** the duration overflows `TimeSpan` and the command errors (KI-35) | none |
| SIN-55 | none | `/ModSettings PublicBanLog option:Select Channel channel:#general` | Ephemeral "Updated the public ban log to #general" | Setting; `#modlog` line |
| SIN-56 | Ban log set | `option:Off` | "Disabled the public ban log." | Setting cleared; `#modlog` line |
| SIN-57 | Bot can't send in `#private` | Select `#private` | Red "@Grimoire don't have permission to send messages in that channel." (sic) | Setting unchanged |
| SIN-58 | none | `option:Select Channel` with no channel | Error "Select a channel when using the Select Channel option." | Setting unchanged |
| SIN-59 | none | `mod` (no Manage Guild) runs any subcommand | Permission error | none |
| SIN-60 | Moderation disabled | Run View | "This module is disabled in this server.", so View's "Module Enabled" line can only ever say True | none |

Automated in `Grimoire.Tests/Integration/Moderation/ModSettingsTests.cs`.

## D. `/Publish Ban|Unban|Kick sin_id`

Gates: `RequireGuild`, `RequireModuleEnabled(Moderation)`, and Manage Messages, checked by the bot. Each posts an
embed to the public ban log channel (`/ModSettings PublicBanLog`) with the date, the user's newest username and id,
and the reason. Publishing again edits the earlier message. Unban publishes the ban's latest pardon, so the ban must
be pardoned first (`/Unban` records nothing itself, KI-09).

| ID | Precondition | Action | Expected Discord result | Expected DB / log |
|----|--------------|--------|-------------------------|-------------------|
| SIN-70 | A ban sin with reason "spam"; ban log `#general` | `mod` runs `/Publish Ban sin_id:n` | "Successfully published ban : n". `#general` gets a "Ban" embed: Date, "**User:** user1 (id)", "**Reason:** spam" | `PublishedMessages` row (Ban). `#modlog` "@mod published ban reason of sin n" |
| SIN-71 | SIN-70 done, reason since changed | Publish again | The same message is edited with the new reason | No second row |
| SIN-72 | SIN-70 done; the published message deleted by hand | Publish twice more | **Expected:** a new message whose id is stored, then edited. **Actual:** each publish posts another message, because the stored id is never updated (KI-36) | Row still holds the deleted id |
| SIN-73 | A warn sin | `/Publish Ban sin_id:<warn>` | "Could not find a ban with that Sin Id." | none |
| SIN-74 | No ban log configured | `/Publish Ban` | Red "Failed to publish ban reason. Verify @Grimoire has access to send messages in the public ban log channel." (it isn't configured at all) | none |
| SIN-75 | Ban log `#private`, which the bot can't send in | `/Publish Ban` | Discord refuses, so the bot-missing-permissions help is shown, not the SIN-74 text | none |
| SIN-76 | A ban sin, not pardoned | `/Publish Unban sin_id:n` | "The ban must be pardoned first before the unban can be published." | none |
| SIN-77 | The ban pardoned with "appealed" | `/Publish Unban sin_id:n` | "Unban" embed with the pardon's reason and date | `PublishedMessages` row (Unban) |
| SIN-78 | A kick sin | `/Publish Kick sin_id:n` | "Kick" embed | `PublishedMessages` row (Kick) |
| SIN-79 | A ban of a user with no username history | `/Publish Ban` | **Expected:** published with the user's current name. **Actual:** the command errors (KI-33) | none |
| SIN-80 | none | `user1` runs `/Publish Ban`; Moderation disabled | Permission error; module disabled | none |

Automated in `Grimoire.Tests/Integration/Moderation/PublishTests.cs`.

## Code observations

1. **`/SinLog type:Mod` ignores the user option** (KI-29). It counts sins by the invoker, under the given user's name,
   and doesn't count kicks.
2. **Multi-page sin logs show only the last page** (KI-30). After the defer, every `ReplyAsync` edits the original
   response.
3. **Pardon details mix two pardons**: "Pardoned by" takes the newest pardon's moderator but the oldest pardon's date.
   This only matters if a sin can be pardoned twice (see A-15).

4. **`/Pardon` without a reason throws** (KI-32). The reason defaults to `""`, which `ModerationReason.Create` rejects.
5. **Pardon and Reason can't find sins of users with no username history** (KI-33). The username lookup doubles as
   the existence check.
6. **Forget's log title uses the bot's nickname**, which is usually unset, so it reads " has been commanded to forget."
   (KI-34).

## Findings

Record failures from manual runs here.
