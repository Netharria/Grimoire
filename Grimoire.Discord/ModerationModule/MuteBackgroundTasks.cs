// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Moderation.Commands.MuteCommands.UnmuteUserCommand;
using Grimoire.Core.Features.Moderation.Queries.GetExpiredMutes;

namespace Grimoire.Discord.ModerationModule;

public class MuteBackgroundTasks : INotificationHandler<TimedNotification>
{
    private readonly IMediator _mediator;

    private readonly IDiscordClientService _discordClientService;

    public MuteBackgroundTasks(IMediator mediator, IDiscordClientService discordClientService)
    {
        this._mediator = mediator;
        this._discordClientService = discordClientService;
    }

    public async ValueTask Handle(TimedNotification notification, CancellationToken cancellationToken)
    {
        if (notification.Time.Second % 5 != 0)
            return;
        var response = await this._mediator.Send(new GetExpiredMutesQuery(), cancellationToken);
        foreach (var expiredLock in response)
        {
            var guild = this._discordClientService.Client.Guilds.GetValueOrDefault(expiredLock.GuildId);
            if (guild is null) continue;

            var user = guild.Members.GetValueOrDefault(expiredLock.UserId);

            if (user is null) continue;
            var role = guild.Roles.GetValueOrDefault(expiredLock.MuteRole);
            if (role is null) continue;
            await user.RevokeRoleAsync(role);
            _ = await this._mediator.Send(new UnmuteUserCommand { UserId = user.Id, GuildId = guild.Id }, cancellationToken);

            var embed = new DiscordEmbedBuilder()
                .WithDescription($"Mute on {user.Mention} has expired.");

            await user.SendMessageAsync(embed);

            if (expiredLock.LogChannelId is not null)
            {
                var ModerationLogChannel = guild.Channels.GetValueOrDefault(expiredLock.LogChannelId.Value);
                if (ModerationLogChannel is not null)
                    await ModerationLogChannel.SendMessageAsync(embed);
            }
        }
    }
}
