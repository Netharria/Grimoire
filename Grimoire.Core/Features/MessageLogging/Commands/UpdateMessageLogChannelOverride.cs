// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Extensions;

namespace Grimoire.Core.Features.MessageLogging.Commands;
public sealed class UpdateMessageLogChannelOverride
{
    public enum MessageLogChannelOverrideSetting
    {
        Always,
        Inherit,
        Never
    }
    public sealed record Command : ICommand<BaseResponse>
    {
        public required ulong ChannelId { get; init; }
        public required ulong GuildId { get; init; }
        public required MessageLogChannelOverrideSetting ChannelOverrideSetting { get; set; }
    }

    public sealed class Handler(IGrimoireDbContext dbContext) : ICommandHandler<Command, BaseResponse>
    {
        private readonly IGrimoireDbContext _dbContext = dbContext;

        public ValueTask<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            if (command.ChannelOverrideSetting == MessageLogChannelOverrideSetting.Inherit)
                return this.DeleteOverride(command, cancellationToken);
            return this.AddOrUpdateOverride(command, cancellationToken);
        }

        private async ValueTask<BaseResponse> DeleteOverride(Command command, CancellationToken cancellationToken)
        {
            var result = await this._dbContext.MessagesLogChannelOverrides
                .Where(x => x.ChannelId == command.ChannelId)
                .Select(x => new
                {
                    Override = x,
                    x.Guild.ModChannelLog
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (result is null || result.Override is null)
                throw new AnticipatedException("That channel did not have an override.");
            this._dbContext.MessagesLogChannelOverrides.Remove(result.Override);
            await this._dbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse
            {
                LogChannelId = result.ModChannelLog,
                Message = "Override was successfully removed from the channel."
            };
        }

        private async ValueTask<BaseResponse> AddOrUpdateOverride(Command command, CancellationToken cancellationToken)
        {
            var result = await this._dbContext.Guilds
                .Where(x => x.Id == command.GuildId)
                .Select(x => new
                {
                    Override = x.MessageLogChannelOverrides.FirstOrDefault(x => x.ChannelId == command.ChannelId),
                    x.ModChannelLog
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                throw new AnticipatedException("Could not find guild settings.");
            if (result.Override is null)
            {
                var local = this._dbContext.MessagesLogChannelOverrides.Local
                .FirstOrDefault(x => x.ChannelId == command.ChannelId
                    && x.GuildId == command.GuildId);
                if (local is not null)
                    this._dbContext.Entry(local).State = EntityState.Detached;
                await this._dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
                {
                    ChannelId = command.ChannelId,
                    GuildId = command.GuildId,
                    ChannelOption = command.ChannelOverrideSetting switch
                    {
                        MessageLogChannelOverrideSetting.Always => MessageLogChannelOverrideOption.AlwaysLog,
                        MessageLogChannelOverrideSetting.Never => MessageLogChannelOverrideOption.NeverLog,
                        _ => throw new ArgumentOutOfRangeException(nameof(command.ChannelOverrideSetting)),
                    }
                });
            }
            else
            {
                result.Override.ChannelOption = command.ChannelOverrideSetting switch
                {
                    MessageLogChannelOverrideSetting.Always => MessageLogChannelOverrideOption.AlwaysLog,
                    MessageLogChannelOverrideSetting.Never => MessageLogChannelOverrideOption.NeverLog,
                    _ => throw new ArgumentOutOfRangeException(nameof(command.ChannelOverrideSetting)),
                };
            }

            await this._dbContext.SaveChangesAsync();
            return new BaseResponse
            {
                LogChannelId = result.ModChannelLog,
                Message = command.ChannelOverrideSetting switch
                {
                    MessageLogChannelOverrideSetting.Always => $"Will now always log messages from {ChannelExtensions.Mention(command.ChannelId)} and its sub channels/threads.",
                    MessageLogChannelOverrideSetting.Never => $"Will now never log messages from {ChannelExtensions.Mention(command.ChannelId)} and its sub channels/threads.",
                    _ => throw new ArgumentOutOfRangeException(nameof(command.ChannelOverrideSetting)),
                };
        }
        }
    }
}
