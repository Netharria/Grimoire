// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

[RequireGuild]
[RequireModuleEnabled(Module.Moderation)]
[RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
internal sealed class UpdateSinReason(IDbContextFactory<GrimoireDbContext> dbContextFactory, GuildLog guildLog)
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;

    [Command("Reason")]
    [Description("Update the reason for a user's sin.")]
    public async Task ReasonAsync(CommandContext ctx,
        [MinMaxValue(0)] [Parameter("SinId")] [Description("The id of the sin to be updated.")]
        int sinId,
        [MinMaxLength(maxLength: 1000)] [Parameter("Reason")] [Description("The reason the sin will be updated to.")]
        string reason)
    {
        await ctx.DeferResponseAsync();


        var guild = ctx.Guild!;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var result = await dbContext.Sins
            .Where(sin => sin.Id == sinId)
            .Where(sin => sin.GuildId == guild.Id)
            .Select(sin => new
            {
                // ReSharper disable AccessToDisposedClosure
                Sin = sin,
                UserName = dbContext.UsernameHistory
                    .Where(usernameHistory => usernameHistory.UserId == sin.UserId)
                    .OrderByDescending(usernameHistory => usernameHistory.Timestamp)
                    .Select(usernameHistory => usernameHistory.Username)
                    .FirstOrDefault()
                // ReSharper restore AccessToDisposedClosure
            })
            .FirstOrDefaultAsync();

        if (result is null)
        {
            await ctx.SendErrorResponseAsync("Could not find a sin with that ID.");
            return;
        }

        result.Sin.Reason = reason;

        await dbContext.SaveChangesAsync();

        var message = $"**ID:** {sinId} **User:** {result.UserName}";

        await ctx.EditReplyAsync(embed: new DiscordEmbedBuilder()
            .WithAuthor("Reason Updated")
            .AddField("Id", sinId.ToString(), true)
            .AddField("User", result.UserName ?? "Unknown", true)
            .AddField("Reason", reason)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .WithColor(GrimoireColor.Green));

        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Green,
            Description = $"{ctx.User.Mention} updated reason to {reason} for {message}"
        });
    }
}
