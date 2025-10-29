// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

public enum SinQueryType
{
    Warn,
    Mute,
    Ban,
    All,
    Mod
}

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
internal sealed class SinLog(IDbContextFactory<GrimoireDbContext> dbContextFactory, SettingsModule settingsModule)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly SettingsModule _settingsModule = settingsModule;

    [Command("SinLog")]
    [Description("Looks up the sin logs for the provided user.")]
    public async Task SinLogAsync(
        CommandContext ctx,
        [Parameter("Type")] [Description("The type of logs to look up.")]
        SinQueryType sinQueryType,
        [Parameter("User")] [Description("The user to look up the logs for. Leave blank for self.")]
        DiscordUser? user = null)
    {

        var guild = ctx.Guild!;
        var member = ctx.Member!;

        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(!member.Permissions.HasPermission(DiscordPermission.ManageMessages));
        else if (!ctx.Member.Permissions.HasPermission(DiscordPermission.ManageMessages))
            return;
        else
            await ctx.DeferResponseAsync();
        user ??= ctx.User;


        if (!member.Permissions.HasPermission(DiscordPermission.ManageMessages) && ctx.User != user)
        {
            await ctx.EditReplyAsync(GrimoireColor.Red, "You do not have permission to view other users' logs.");
            return;
        }
        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        if (sinQueryType == SinQueryType.Mod)
        {
            var modResponse = await dbContext.Sins
                .AsNoTracking()
                .Where(sin => sin.ModeratorId == user.Id && sin.GuildId == guild.Id)
                .GroupBy(sin => sin.SinType)
                .ToDictionaryAsync(sinGroup => sinGroup.Key, sinGroup => sinGroup.Count());

            await ctx.EditReplyAsync(embed: new DiscordEmbedBuilder()
                .WithAuthor($"Moderation log for {user.Username}")
                .AddField("Bans", modResponse.GetValueOrDefault(SinType.Ban, 0).ToString(), true)
                .AddField("Mutes", modResponse.GetValueOrDefault(SinType.Mute, 0).ToString(), true)
                .AddField("Warns", modResponse.GetValueOrDefault(SinType.Warn, 0).ToString(), true)
                .WithColor(GrimoireColor.Purple));
            return;
        }

        var queryable = dbContext.Sins
            .AsNoTracking()
            .Where(sin => sin.ModeratorId == user.Id && sin.GuildId == guild.Id);

        queryable = sinQueryType switch
        {
            SinQueryType.Warn => queryable.Where(x => x.SinType == SinType.Warn),
            SinQueryType.Mute => queryable.Where(x => x.SinType == SinType.Mute),
            SinQueryType.Ban => queryable.Where(x => x.SinType == SinType.Ban),
            SinQueryType.All => queryable,
            SinQueryType.Mod => throw new UnreachableException("Handled above"),
            _ => throw new ArgumentOutOfRangeException(nameof(sinQueryType), sinQueryType, null)
        };

        var autoPardonAfter = await this._settingsModule.GetAutoPardonDuration(guild.Id);

        var result = await queryable
            .Where(x => x.SinOn > DateTimeOffset.UtcNow - autoPardonAfter)
            .Select(x => new
            {
                x.Id,
                x.SinType,
                x.SinOn,
                x.Reason,
                x.ModeratorId,
                Pardon = x.Pardon != null,
                PardonModeratorId = (ulong?)(x.Pardon != null ? x.Pardon.ModeratorId : null),
                PardonDate = x.Pardon != null ? x.Pardon.PardonDate : DateTimeOffset.MinValue
            }).ToListAsync();
        var stringBuilder = new StringBuilder(2048);
        var resultStrings = new List<string>();
        result.ForEach(x =>
        {
            var builder = $"**{x.Id} : {x.SinType}** : <t:{x.SinOn.ToUnixTimeSeconds()}:f>\n" +
                          $"\tReason: {x.Reason}\n" +
                          $"\tModerator: {UserExtensions.Mention(x.ModeratorId)}\n";
            if (x.Pardon)
                builder = $"~~{builder}~~" +
                          $"**Pardoned by: {UserExtensions.Mention(x.PardonModeratorId)} on <t:{x.PardonDate.ToUnixTimeSeconds()}:f>**\n";
            if (stringBuilder.Length + builder.Length > stringBuilder.Capacity)
            {
                resultStrings.Add(stringBuilder.ToString());
                stringBuilder.Clear();
            }

            stringBuilder.Append(builder);
        });
        if (stringBuilder.Length > 0)
            resultStrings.Add(stringBuilder.ToString());
        if (resultStrings.Count == 0)
            await ctx.EditReplyAsync(GrimoireColor.Green, "That user does not have any logs",
                $"Sin log for {user.Username}");
        foreach (var message in resultStrings)
            await ctx.EditReplyAsync(GrimoireColor.Green, message,
                $"Sin log for {user.Username}");
    }
}
