// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Exceptions;
using Grimoire.Core.Features.Moderation.Commands;
using Grimoire.Core.Features.SpamModule;

namespace Grimoire.Discord.SpamModule;

[DiscordMessageCreatedEventSubscriber]
internal class SpamEvents(IMediator mediator, SpamTrackerModule spamModule) : IDiscordMessageCreatedEventSubscriber
{
    private readonly IMediator _mediator = mediator;
    private readonly SpamTrackerModule _spamModule = spamModule;

    public async Task DiscordOnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
    {
        var checkSpamResult = _spamModule.CheckSpam(args.Message);

        if(checkSpamResult.IsSpam == false)
            return;

        if (args.Author is not DiscordMember member)
        {
            return;
        }

        var response = await this._mediator.Send(new AutoMuteUser.Command
        {
            UserId = args.Message.Author.Id,
            GuildId = args.Guild.Id,
            ModeratorId = sender.CurrentUser.Id,
            Reason = checkSpamResult.Reason,
        });
        var muteRole = args.Guild.Roles.GetValueOrDefault(response.MuteRole);
        if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
        await member.GrantRoleAsync(muteRole, checkSpamResult.Reason);

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("Mute")
            .AddField("User", member.Mention, true)
            .AddField("Sin Id", $"**{response.SinId}**", true)
            .AddField("Moderator", sender.CurrentUser.Mention, true)
            .AddField("Length", $"{response.Duration.TotalMinutes} minute(s)", true)
            .WithColor(GrimoireColor.Red)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField("Reason", checkSpamResult.Reason);


        await args.Channel.PurgeMessagesAsync(5, "Automod: Spam", x => x.Author.Id == args.Author.Id);

        var sentMessageToUser = true;
        try
        {
            await member.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor($"Mute Id {response.SinId}")
            .WithDescription($"You have been muted for {response.Duration.TotalMinutes} minute(s) by {sender.CurrentUser.Mention} for spamming.")
            .WithColor(GrimoireColor.Red));
        }
        catch (Exception)
        {
            sentMessageToUser= false;
        }

        if (response.LogChannelId is null) return;

        var logChannel = args.Guild.Channels.GetValueOrDefault(response.LogChannelId.Value);

        if (logChannel is null) return;

        await logChannel.SendMessageAsync(embed);

        if(sentMessageToUser == false)
        {
            await logChannel.SendMessageAsync(new DiscordEmbedBuilder()
            .WithDescription($"Was not able to send a direct message with the mute details to {member.Mention}")
            .WithColor(GrimoireColor.Red));
        }

        
    }
}
