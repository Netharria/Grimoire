// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Moderation.Commands.LockCommands.UnlockChannelCommand;
using Grimoire.Core.Features.Moderation.Queries.GetExpiredLocks;

namespace Grimoire.Discord.ModerationModule
{
    public class LockBackgroundTasks : INotificationHandler<TimedNotification>
    {
        private readonly IMediator _mediator;
        private readonly IDiscordClientService _discordClientService;

        public LockBackgroundTasks(IMediator mediator, IDiscordClientService discordClientService)
        {
            this._mediator = mediator;
            this._discordClientService = discordClientService;
        }

        public async ValueTask Handle(TimedNotification notification, CancellationToken cancellationToken)
        {
            if (notification.Time.Second % 5 != 0)
                return;
            var response = await this._mediator.Send(new GetExpiredLocksQuery(), cancellationToken);
            foreach(var expiredLock in response)
            {
                var guild = this._discordClientService.Client.Guilds.GetValueOrDefault(expiredLock.GuildId);
                if (guild is null) continue;

                var channel = guild.Channels.GetValueOrDefault(expiredLock.ChannelId);

                if (channel is null) continue;
                if(!channel.IsThread)
                    await channel.ModifyAsync(editModel => editModel.PermissionOverwrites = channel.PermissionOverwrites.ToAsyncEnumerable()
                    .SelectAwait(async x => {
                        if (x.Type == OverwriteType.Role)
                            return await new DiscordOverwriteBuilder(await x.GetRoleAsync()).FromAsync(x);
                        return await new DiscordOverwriteBuilder(await x.GetMemberAsync()).FromAsync(x);
                    })
                    .Select(x =>
                    {
                        if (x.Target.Id == guild.EveryoneRole.Id)
                        {
                            x.Allowed.RevertLockPermissions(expiredLock.PreviouslyAllowed);
                            x.Denied.RevertLockPermissions(expiredLock.PreviouslyDenied);
                        }
                        return x;
                    }).ToEnumerable());

                _ = await this._mediator.Send(new UnlockChannelCommand { ChannelId = channel.Id, GuildId = guild.Id }, cancellationToken);

                var embed = new DiscordEmbedBuilder()
                    .WithDescription($"Lock on {channel.Mention} has expired.");

                await channel.SendMessageAsync(embed);

                if (expiredLock.LogChannelId is not null)
                {
                    var ModerationLogChannel = guild.Channels.GetValueOrDefault(expiredLock.LogChannelId.Value);
                    if (ModerationLogChannel is not null)
                        await ModerationLogChannel.SendMessageAsync(embed);
                }
            }
        }
    }
}
