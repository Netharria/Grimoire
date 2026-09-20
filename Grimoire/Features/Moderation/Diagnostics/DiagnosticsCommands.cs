// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;
using JetBrains.Annotations;

namespace Grimoire.Features.Moderation.Diagnostics;

[UsedImplicitly]
[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
[Command("Diagnostics")]
[Description("Checks the server setup for problems that stop moderation from working.")]
public sealed class DiagnosticsCommands(SettingsModule settingsModule)
{
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("Check")]
    [Description("Checks whether mutes and locks will work in this server's channels.")]
    public async Task CheckAsync(CommandContext ctx)
    {
        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(true);
        else
            await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;
        var muteRoleId = await this._settingsModule.GetConfiguredMuteRole(guild.GetGuildId())
            .GetOrElse(() => null);
        var ignores = await this._settingsModule.GetDiagnosticsIgnores(guild.GetGuildId());

        await ctx.EditResponseAsync(DiagnosticsResponse.Build(guild, muteRoleId, ignores));
    }

    [Command("Ignore")]
    [Description("Manage the roles and members that the diagnostics leave out.")]
    public sealed class IgnoreCommands(SettingsModule settingsModule, GuildLog guildLog)
    {
        private readonly GuildLog _guildLog = guildLog;
        private readonly SettingsModule _settingsModule = settingsModule;

        [Command("Add")]
        [Description("Leave roles or members out of the diagnostics, e.g. moderators who may speak in locked channels.")]
        public async Task AddAsync(CommandContext ctx,
            [Parameter("items")] [Description("The roles or members to ignore.")]
            params SnowflakeObject[] items)
        {
            await ctx.DeferResponseAsync();
            var guild = ctx.Guild!;
            var (ignored, skipped) = ToIgnoredItems(items, guild, ctx.GetModeratorId());

            if (ignored.Count == 0)
            {
                await ctx.SendWarningResponseAsync("Only roles and members can be ignored. "
                                                   + "Administrator roles are always left out and @everyone cannot be.");
                return;
            }

            await (await this._settingsModule.AddDiagnosticsIgnoredItemsAsync(guild.GetGuildId(), ignored))
                .MatchAsync(
                    added => this.ReportAsync(ctx, guild, added, "now ignored by the diagnostics", skipped),
                    error => ctx.SendWarningResponseAsync(error.Message).AsTask());
        }

        [Command("Remove")]
        [Description("Include roles or members in the diagnostics again.")]
        public async Task RemoveAsync(CommandContext ctx,
            [Parameter("items")] [Description("The roles or members to stop ignoring.")]
            params SnowflakeObject[] items)
        {
            await ctx.DeferResponseAsync();
            var guild = ctx.Guild!;
            var (ignored, skipped) = ToIgnoredItems(items, guild, ctx.GetModeratorId());

            if (ignored.Count == 0)
            {
                await ctx.SendWarningResponseAsync("Only roles and members can be on the ignore list.");
                return;
            }

            await (await this._settingsModule.RemoveDiagnosticsIgnoredItemsAsync(guild.GetGuildId(), ignored))
                .MatchAsync(
                    removed => this.ReportAsync(ctx, guild, removed, "no longer ignored by the diagnostics", skipped),
                    error => ctx.SendWarningResponseAsync(error.Message).AsTask());
        }

        [Command("View")]
        [Description("Shows the roles and members the diagnostics leave out.")]
        public async Task ViewAsync(CommandContext ctx)
        {
            await ctx.DeferResponseAsync();

            var items = await this._settingsModule.GetDiagnosticsIgnoredItemsAsync(ctx.Guild!.GetGuildId())
                .GetOrElse(() => FrozenSet<DiagnosticsIgnoredItem>.Empty);

            await ctx.ReplyAsync(embed: new DiscordEmbedBuilder()
                .WithTitle("Ignored by the diagnostics")
                .AddField("Roles", Join(items.OfType<DiagnosticsIgnoredRole>().Select(r => $"<@&{r.RoleId.Value}>")))
                .AddField("Members",
                    Join(items.OfType<DiagnosticsIgnoredMember>().Select(m => $"<@{m.UserId.Value}>"))));
        }

        private static string Join(IEnumerable<string> mentions)
        {
            var text = string.Join(' ', mentions);
            return text.Length == 0 ? "None" : text.Length <= 1000 ? text : text[..997] + "...";
        }

        private static (List<DiagnosticsIgnoredItem> Items, List<SnowflakeObject> Skipped) ToIgnoredItems(
            IEnumerable<SnowflakeObject> items, DiscordGuild guild, ModeratorId moderator)
        {
            List<DiagnosticsIgnoredItem> ignored = [];
            List<SnowflakeObject> skipped = [];
            var now = DateTimeOffset.UtcNow;

            foreach (var item in items)
                switch (item)
                {
                    case DiscordRole role when role.Id != guild.Id:
                        if (DiagnosticsIgnoredRole.Create(new RoleId(role.Id), guild.GetGuildId(), moderator, now)
                            is Validation<DiagnosticsIgnoredRole>.Valid { Value: var ignoredRole })
                            ignored.Add(ignoredRole);
                        else
                            skipped.Add(item);
                        break;
                    case DiscordUser user:
                        if (DiagnosticsIgnoredMember.Create(new UserId(user.Id), guild.GetGuildId(), moderator, now)
                            is Validation<DiagnosticsIgnoredMember>.Valid { Value: var ignoredMember })
                            ignored.Add(ignoredMember);
                        else
                            skipped.Add(item);
                        break;
                    default:
                        skipped.Add(item);
                        break;
                }

            return (ignored, skipped);
        }

        private async Task ReportAsync(CommandContext ctx, DiscordGuild guild,
            IReadOnlyList<DiagnosticsIgnoredItem> changed, string outcome, IReadOnlyList<SnowflakeObject> skipped)
        {
            var mentions = string.Join(' ', changed.Select(item => item switch
            {
                DiagnosticsIgnoredRole role => $"<@&{role.RoleId.Value}>",
                DiagnosticsIgnoredMember member => $"<@{member.UserId.Value}>",
                _ => string.Empty
            }));
            var message = $"{mentions} {(changed.Count == 1 ? "is" : "are")} {outcome}."
                          + (skipped.Count > 0 ? $"\nSkipped {skipped.Count} item(s) that are not roles or members." : "");

            await ctx.ReplyAsync(GrimoireColor.Green, message);
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.DarkPurple,
                Description = $"{ctx.User.Mention}: {message}"
            });
        }
    }
}
