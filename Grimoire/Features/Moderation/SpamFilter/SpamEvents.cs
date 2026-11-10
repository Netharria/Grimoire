// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Domain;
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
    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs args)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (args.Author.IsBot || args.Guild is null)
            return;

        if (!await settingsModule.IsModuleEnabled(Module.AntiSpam, args.Guild.GetGuildId())
                .GetOrElse(() => false))
            return;

        var checkSpamResult = await spamModule.CheckSpam(args.Message);

        if (!checkSpamResult.IsSpam)
            return;

        if (args.Author is not DiscordMember member)
            return;

        var muteRoleId = await settingsModule.GetEffectiveMuteRole(args.Guild.GetGuildId())
            .GetOrElse(() => null);
        if (muteRoleId is null)
            return;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var muteCount = await dbContext.Sins
            .Where(member1 => member1.UserId == member.GetUserId() && member1.GuildId == args.Guild.GetGuildId())
            .Where(x => x.SinType == SinType.Mute)
            .CountAsync(x => x.SinOn > DateTimeOffset.UtcNow.AddDays(-1));
        var duration = TimeSpan.FromMinutes(Math.Pow(2, muteCount));
        var muteEndTime = DateTimeOffset.UtcNow.Add(duration);
        var now = DateTimeOffset.UtcNow;
        var sin = Sin.ForMute(new ModerationActor.System(), member.GetUserId(), args.Guild.GetGuildId(), now,
                ModerationReason.CreateIfNotBlank(checkSpamResult.Reason).Match(r => r, _ => null))
            .Match(s => s, _ => throw new InvariantViolationException("Discord ids are never zero."));
        dbContext.Sins.Add(sin);
        await dbContext.SaveChangesAsync();

        // Nothing would ever lift a mute role given without a record, so don't give it.
        if (await settingsModule.AddMute(member.GetUserId(), args.Guild.GetGuildId(),
                new ModeratorId(args.Guild.CurrentMember.Id), sin.Id, muteEndTime, muteRoleId)
            is not Result<MuteAdded>.Success)
            return;

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


        await guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
        {
            GuildId = args.Guild.GetGuildId(), GuildLogType = GuildLogType.Moderation, Embed = embed
        });

        await guildLog.DirectMessageAsync(member, new DiscordEmbedBuilder()
            .WithAuthor($"Mute Id {sin.Id}")
            .WithDescription(
                $"You have been muted for {duration.TotalMinutes} minute(s) by {sender.CurrentUser.Mention} for spamming.")
            .WithColor(GrimoireColor.Red), "mute");

        // Last, so a refusal can't cost the log or the DM.
        try
        {
            await args.Message.DeleteAsync();
        }
        catch (DiscordException)
        {
            // Without Manage Messages Discord refuses, and PluralKit may have deleted it already. The member is muted
            // and everyone told either way.
        }
    }
}
