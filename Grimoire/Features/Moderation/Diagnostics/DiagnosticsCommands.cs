// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain.Documents;
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
        await ctx.DeferPrivateAsync();

        var guild = ctx.RequiredGuild;
        var muteRoleId = await this._settingsModule.GetConfiguredMuteRole(guild.GetGuildId())
            .OrNullDuringMigration();
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
            var guild = ctx.RequiredGuild;
            var (ignored, skipped) = ToTargets(items, guild);

            if (ignored.Count == 0)
            {
                await ctx.SendWarningResponseAsync("Only roles and members can be ignored. "
                                                   + "Administrator roles are always left out and @everyone cannot be.");
                return;
            }

            await (await this._settingsModule.AddDiagnosticsIgnoredItemsAsync(guild.GetGuildId(), ctx.GetModeratorId(), ignored))
                .MatchAsync(
                    write => write.Match(
                        saved: added => this.ReportAsync(ctx, guild, added, "now ignored by the diagnostics", skipped),
                        // Nothing new to ignore: a warning, as when it was a failure.
                        unchanged: message => ctx.SendWarningResponseAsync(message).AsTask()),
                    error => ctx.SendWarningResponseAsync(error.Message).AsTask());
        }

        [Command("Remove")]
        [Description("Include roles or members in the diagnostics again.")]
        public async Task RemoveAsync(CommandContext ctx,
            [Parameter("items")] [Description("The roles or members to stop ignoring.")]
            params SnowflakeObject[] items)
        {
            await ctx.DeferResponseAsync();
            var guild = ctx.RequiredGuild;
            var (ignored, skipped) = ToTargets(items, guild);

            if (ignored.Count == 0)
            {
                await ctx.SendWarningResponseAsync("Only roles and members can be on the ignore list.");
                return;
            }

            await (await this._settingsModule.RemoveDiagnosticsIgnoredItemsAsync(guild.GetGuildId(), ctx.GetModeratorId(), ignored))
                .MatchAsync(
                    removed => this.ReportAsync(ctx, guild, removed, "no longer ignored by the diagnostics", skipped),
                    error => ctx.SendWarningResponseAsync(error.Message).AsTask());
        }

        [Command("View")]
        [Description("Shows the roles and members the diagnostics leave out.")]
        public async Task ViewAsync(CommandContext ctx)
        {
            await ctx.DeferResponseAsync();

            var items = await this._settingsModule.GetDiagnosticsIgnoredItemsAsync(ctx.RequiredGuild.GetGuildId())
                .OrDefaultDuringMigration(FrozenSet<DiagnosticsTarget>.Empty);

            await ctx.ReplyPagesAsync(ViewPages(items));
        }

        private const int MaxFieldLength = 1024;
        private const int MaxFieldsPerPage = 25;

        /// <summary>Room for field names and values, under Discord's 6,000 for a whole embed with title and footer.</summary>
        private const int MaxFieldTextPerPage = 5500;

        /// <summary>
        ///     The ignore list as embed fields: Roles, then Members, each split between mentions into
        ///     "(continued)" fields, and the fields split into as many pages as they need. KI-62.
        /// </summary>
        internal static IReadOnlyList<DiscordEmbedBuilder> ViewPages(IReadOnlyCollection<DiagnosticsTarget> items)
            => [
                .. PackFields([
                    .. Section("Roles", items.OfType<DiagnosticsTarget.Role>().Select(r => $"<@&{r.Id.Value}>")),
                    .. Section("Members", items.OfType<DiagnosticsTarget.Member>().Select(m => $"<@{m.Id.Value}>"))
                ]).Select(fields => fields.Aggregate(
                    new DiscordEmbedBuilder().WithTitle("Ignored by the diagnostics"),
                    (embed, field) => embed.AddField(field.Name, field.Value)))
            ];

        // Mentions never contain a line break, so packing them as lines and joining with spaces keeps each whole.
        private static IEnumerable<(string Name, string Value)> Section(string name, IEnumerable<string> mentions)
            => PagedReplyExtensions.Paginate(mentions, MaxFieldLength)
                .DefaultIfEmpty("None")
                .Select((text, index) => (index == 0 ? name : $"{name} (continued)", text.Replace('\n', ' ')));

        private static IEnumerable<IReadOnlyList<(string Name, string Value)>> PackFields(
            IEnumerable<(string Name, string Value)> fields)
        {
            List<(string Name, string Value)> page = [];
            var length = 0;
            foreach (var field in fields)
            {
                var size = field.Name.Length + field.Value.Length;
                if (page.Count > 0 && (page.Count == MaxFieldsPerPage || length + size > MaxFieldTextPerPage))
                {
                    yield return page;
                    page = [];
                    length = 0;
                }

                page.Add(field);
                length += size;
            }

            if (page.Count > 0)
                yield return page;
        }

        private static (List<DiagnosticsTarget> Items, List<SnowflakeObject> Skipped) ToTargets(
            IEnumerable<SnowflakeObject> items, DiscordGuild guild)
        {
            List<DiagnosticsTarget> ignored = [];
            List<SnowflakeObject> skipped = [];

            foreach (var item in items)
                switch (item)
                {
                    case DiscordRole role when role.Id != guild.Id:
                        ignored.Add(new DiagnosticsTarget.Role(new RoleId(role.Id)));
                        break;
                    case DiscordUser user:
                        ignored.Add(new DiagnosticsTarget.Member(new UserId(user.Id)));
                        break;
                    default:
                        skipped.Add(item);
                        break;
                }

            return (ignored, skipped);
        }

        private async Task ReportAsync(CommandContext ctx, DiscordGuild guild,
            IReadOnlyList<DiagnosticsTarget> changed, string outcome, IReadOnlyList<SnowflakeObject> skipped)
        {
            var mentions = string.Join(' ', changed.Select(item => item switch
            {
                DiagnosticsTarget.Role role => $"<@&{role.Id.Value}>",
                DiagnosticsTarget.Member member => $"<@{member.Id.Value}>",
                _ => string.Empty
            }));
            var message = $"{mentions} {(changed.Count == 1 ? "is" : "are")} {outcome}."
                          + (skipped.Count > 0 ? $"\nSkipped {skipped.Count} item(s) that are not roles or members." : "");

            await ctx.ReplyAsync(GrimoireColor.Green, message);
            await this._guildLog.SendLogMessageAsync(new GuildLogTextMessage
            {
                GuildId = guild.GetGuildId(),
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.DarkPurple,
                Description = $"{ctx.User.Mention}: {message}"
            });
        }
    }
}
