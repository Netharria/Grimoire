// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace

namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class Message
    {
        [SlashCommand("Override",
            "Overrides the default message logging settings. Use this to control which channels are logged.")]
        public async Task Override(
            InteractionContext ctx,
            [Option("Option", "Override option to set the channel to.")]
            UpdateMessageLogOverride.MessageLogOverrideSetting overrideSetting,
            [Option("Channel", "The channel to override the message log settings of. Leave empty for current channel.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferAsync();
            channel ??= ctx.Channel;

            var response = await this._mediator.Send(new UpdateMessageLogOverride.Command
            {
                ChannelId = channel.Id, ChannelOverrideSetting = overrideSetting, GuildId = channel.Guild.Id
            });

            await ctx.EditReplyAsync(GrimoireColor.Purple, response.Message);
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                $"{ctx.User.GetUsernameWithDiscriminator()} updated the channel overrides", response.Message);
        }
    }
}

public sealed class UpdateMessageLogOverride
{
    public enum MessageLogOverrideSetting
    {
        Always,
        Inherit,
        Never
    }

    public sealed record Command : IRequest<BaseResponse>
    {
        public required ulong ChannelId { get; init; }
        public required ulong GuildId { get; init; }
        public required MessageLogOverrideSetting ChannelOverrideSetting { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory) : IRequestHandler<Command, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public Task<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            if (command.ChannelOverrideSetting == MessageLogOverrideSetting.Inherit)
                return this.DeleteOverride(command, cancellationToken);
            return this.AddOrUpdateOverride(command, cancellationToken);
        }

        private async Task<BaseResponse> DeleteOverride(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.MessagesLogChannelOverrides
                .Where(x => x.ChannelId == command.ChannelId)
                .Select(x => new { Override = x, x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);
            if (result?.Override is null)
                throw new AnticipatedException("That channel did not have an override.");
            dbContext.MessagesLogChannelOverrides.Remove(result.Override);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse
            {
                LogChannelId = result.ModChannelLog, Message = "Override was successfully removed from the channel."
            };
        }

        private async Task<BaseResponse> AddOrUpdateOverride(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Guilds
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
                await dbContext.MessagesLogChannelOverrides.AddAsync(new MessageLogChannelOverride
                {
                    ChannelId = command.ChannelId,
                    GuildId = command.GuildId,
                    ChannelOption = command.ChannelOverrideSetting switch
                    {
                        MessageLogOverrideSetting.Always => MessageLogOverrideOption.AlwaysLog,
                        MessageLogOverrideSetting.Never => MessageLogOverrideOption.NeverLog,
                        _ => throw new NotImplementedException(
                            "A Message log Override option was selected that has not been implemented.")
                    }
                }, cancellationToken);
            else
                result.Override.ChannelOption = command.ChannelOverrideSetting switch
                {
                    MessageLogOverrideSetting.Always => MessageLogOverrideOption.AlwaysLog,
                    MessageLogOverrideSetting.Never => MessageLogOverrideOption.NeverLog,
                    _ => throw new NotImplementedException(
                        "A Message log Override option was selected that has not been implemented.")
                };

            await dbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse
            {
                LogChannelId = result.ModChannelLog,
                Message = command.ChannelOverrideSetting switch
                {
                    MessageLogOverrideSetting.Always =>
                        $"Will now always log messages from {ChannelExtensions.Mention(command.ChannelId)} and its sub channels/threads.",
                    MessageLogOverrideSetting.Never =>
                        $"Will now never log messages from {ChannelExtensions.Mention(command.ChannelId)} and its sub channels/threads.",
                    _ => throw new NotImplementedException(
                        "A Message log Override option was selected that has not been implemented.")
                }
            };
        }
    }
}
