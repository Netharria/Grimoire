// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Logging.Settings;

namespace Grimoire.Features.Logging.UserLogging.Events;

public sealed class GuildMemberRemovedEvent(IMediator mediator) : IEventHandler<GuildMemberRemovedEventArgs>
{
    private readonly IMediator _mediator = mediator;

    public async Task HandleEventAsync(DiscordClient sender, GuildMemberRemovedEventArgs args)
    {
        var settings = await this._mediator.Send(new GetUserLogSettings.Query{ GuildId = args.Guild.Id });
        if (!settings.IsLoggingEnabled) return;
        if (settings.LeaveChannelLog is null) return;
        var logChannel = args.Guild.Channels.GetValueOrDefault(settings.LeaveChannelLog.Value);
        if (logChannel is null) return;

        var embed = new DiscordEmbedBuilder()
            .WithAuthor("User Left")
            .AddField("Name", args.Member.Mention, true)
            .AddField("Created", Formatter.Timestamp(args.Member.CreationTimestamp), true)
            .AddField("Joined", Formatter.Timestamp(args.Member.JoinedAt), true)
            .WithColor(GrimoireColor.Red)
            .WithThumbnail(args.Member.GetGuildAvatarUrl(ImageFormat.Auto))
            .WithFooter($"Total Members: {args.Guild.MemberCount}")
            .WithTimestamp(DateTimeOffset.UtcNow)
            .AddField($"Roles[{args.Member.Roles.Count()}]",
            args.Member.Roles.Any()
            ? string.Join(' ', args.Member.Roles.Where(x => x.Id != args.Guild.Id).Select(x => x.Mention))
            : "None");
        await logChannel.SendMessageAsync(embed);
    }
}
