// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;
using Grimoire.Settings.Services;

namespace Grimoire.Features.Moderation.SpamFilter;

internal sealed class SpamEvents(
    IDbContextFactory<GrimoireDbContext> dbContextFactory,
    SettingsModule settingsModule,
    SpamTrackerModule spamModule,
    GuildLog guildLog)
    : IEventHandler<MessageCreatedEventArgs>
{
    private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;
    private readonly GuildLog _guildLog = guildLog;
    private readonly SettingsModule _settingsModule = settingsModule;
    private readonly SpamTrackerModule _spamModule = spamModule;

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
    {
        if (args.Author.IsBot)
            return;

        var checkSpamResult = await this._spamModule.CheckSpam(args.Message);

        if (!checkSpamResult.IsSpam)
            return;

        if (args.Author is not DiscordMember member)
            return;

        var muteRoleId = await this._settingsModule.GetMuteRole(args.Guild.Id);
        if (muteRoleId is null)
            return;

        await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();
        var muteCount = await dbContext.Sins
            .Where(member1 => member1.UserId == member.Id && member1.GuildId == args.Guild.Id)
            .Where(x => x.SinType == SinType.Mute)
            .CountAsync(x => x.SinOn > DateTimeOffset.UtcNow.AddDays(-1));
        var duration = TimeSpan.FromMinutes(Math.Pow(2, muteCount));
        var muteEndTime = DateTimeOffset.UtcNow.Add(duration);
        var sin = new Sin
        {
            UserId = member.Id,
            GuildId = args.Guild.Id,
            ModeratorId = args.Guild.CurrentMember.Id,
            Reason = checkSpamResult.Reason,
            SinType = SinType.Mute
        };
        await dbContext.Sins.AddAsync(sin);
        await dbContext.SaveChangesAsync();

        await this._settingsModule.AddMute(member.Id, args.Guild.Id, sin.Id, muteEndTime);

        var muteRole = await args.Guild.GetRoleOrDefaultAsync(muteRoleId.Value);

        if (muteRole is null) return;

        await member.GrantRoleAsync(muteRole, checkSpamResult.Reason);

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Mute")
            .AddField("User", member.Mention, true)
            .AddField("Sin Id", $"**{sin.Id}**", true)
            .AddField("Moderator", sender.CurrentUser.Mention, true)
            .AddField("Length", $"{duration.TotalMinutes} minute(s)", true)
            .WithColor(GrimoireColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("Reason", checkSpamResult.Reason);


        await args.Message.DeleteAsync();

        var sentMessageToUser = true;
        try
        {
            await member.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor($"Mute Id {sin.Id}")
                .WithDescription(
                    $"You have been muted for {duration.TotalMinutes} minute(s) by {sender.CurrentUser.Mention} for spamming.")
                .WithColor(GrimoireColor.Red));
        }
        catch (Exception)
        {
            sentMessageToUser = false;
        }

        await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.Id, GuildLogType = GuildLogType.Moderation, Embed = embed
        });

        if (!sentMessageToUser)
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = args.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Red,
                Description =
                    $"Was not able to send a direct message with the mute details to {member.Mention}"
            });
    }
}
