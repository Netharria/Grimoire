// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Grimoire.Features.Logging.Settings;

namespace Grimoire.Features.Logging.UserLogging.Events;
internal class GuildMemberAddedEvent(IMediator mediator, IInviteService inviteService) : IEventHandler<GuildMemberAddedEventArgs>
{
    private readonly IMediator _mediator = mediator;
    private readonly IInviteService _inviteService = inviteService;

    public async Task HandleEventAsync(DiscordClient sender, GuildMemberAddedEventArgs args)
    {
        var settings = await this._mediator.Send(new GetUserLogSettings.Query{ GuildId = args.Guild.Id });
        if (!settings.IsLoggingEnabled) return;
        if (settings.JoinChannelLog is null) return;
        var logChannel = args.Guild.Channels.GetValueOrDefault(settings.JoinChannelLog.Value);
        if (logChannel is null) return;
        var invites = await args.Guild.GetInvitesAsync();
        var inviteUsed = this._inviteService.CalculateInviteUsed(new GuildInviteDto
        {
            GuildId = args.Guild.Id,
            Invites = new ConcurrentDictionary<string, Invite>(
                invites.Select(x =>
                    new Invite
                    {
                        Code = x.Code,
                        Inviter = x.Inviter.GetUsernameWithDiscriminator(),
                        Url = x.ToString(),
                        Uses = x.Uses,
                        MaxUses = x.MaxUses
                    }).ToDictionary(x => x.Code))
        });
        var inviteUsedText = "";
        if (inviteUsed is not null)
            inviteUsedText = $"{inviteUsed.Url} ({inviteUsed.Uses} uses)\n**Created By:** {inviteUsed.Inviter}";
        else if (!string.IsNullOrWhiteSpace(args.Guild.VanityUrlCode))
            inviteUsedText = $"Vanity Invite";
        else
            inviteUsedText = $"Unknown Invite";

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("User Joined")
            .AddField("Name", args.Member.Mention, true)
            .AddField("Created", Formatter.Timestamp(args.Member.CreationTimestamp), true)
            .AddField("Invite Used", inviteUsedText)
            .WithColor(args.Member.CreationTimestamp > DateTimeOffset.UtcNow.AddDays(-7) ? GrimoireColor.Yellow : GrimoireColor.Green)
            .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
            .WithFooter($"Total Members: {args.Guild.MemberCount}")
            .WithTimestamp(DateTimeOffset.UtcNow);

        if (args.Member.CreationTimestamp > DateTimeOffset.UtcNow.AddDays(-7))
            embed.AddField("New Account", $"Created {Formatter.Timestamp(args.Member.CreationTimestamp)}");
        await logChannel.SendMessageAsync(embed);
    }
}
