// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Diagnostics;
// ReSharper disable once CheckNamespace
using Grimoire.Features.Shared.Channels.GuildLog;
// ReSharper disable once CheckNamespace

namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class Message
    {
        [Command("Override")]
        [Description("Overrides the default message logging settings. Use this to control which channels are logged.")]
        public async Task Override(
            SlashCommandContext ctx,
            [Parameter("Option")] [Description("Override option to set the channel to")]
            UpdateMessageLogOverride.MessageLogOverrideSetting overrideSetting,
            [Parameter("Channel")]
            [Description("The channel to override the message log settings of. Leave empty for current channel.")]
            DiscordChannel? channel = null)
        {
            await ctx.DeferResponseAsync();
            channel ??= ctx.Channel;

            await this._mediator.Send(new UpdateMessageLogOverride.Command
            {
                ChannelId = channel.Id, ChannelOverrideSetting = overrideSetting, GuildId = channel.Guild.Id
            });


            var message = overrideSetting switch
            {
                UpdateMessageLogOverride.MessageLogOverrideSetting.Always =>
                    $"Will now always log messages from {channel.Mention} and its sub channels/threads.",
                UpdateMessageLogOverride.MessageLogOverrideSetting.Never =>
                    $"Will now never log messages from {channel.Mention} and its sub channels/threads.",
                UpdateMessageLogOverride.MessageLogOverrideSetting.Inherit =>
                    "Override was successfully removed from the channel.",
                _ => throw new NotImplementedException(
                    "A Message log Override option was selected that has not been implemented.")
            };

            await ctx.EditReplyAsync(GrimoireColor.Purple, message);
            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = channel.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Title = $"{ctx.User.Mention} updated the channel overrides",
                Description = message,
                Color = GrimoireColor.Purple
            });
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

    public sealed record Command : IRequest
    {
        public required ChannelId ChannelId { get; init; }
        public required GuildId GuildId { get; init; }
        public required MessageLogOverrideSetting ChannelOverrideSetting { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public Task Handle(Command command, CancellationToken cancellationToken)
            => command.ChannelOverrideSetting == MessageLogOverrideSetting.Inherit
                ? DeleteOverride(command, cancellationToken)
                : AddOrUpdateOverride(command, cancellationToken);

        private async Task DeleteOverride(Command command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.MessagesLogChannelOverrides
                .Where(x => x.ChannelId == command.ChannelId)
                .FirstOrDefaultAsync(cancellationToken);
            if (result is null)
                throw new AnticipatedException("That channel did not have an override.");
            dbContext.MessagesLogChannelOverrides.Remove(result);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task AddOrUpdateOverride(Command command, CancellationToken cancellationToken)
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
                        MessageLogOverrideSetting.Inherit => throw new UnreachableException(
                            "Should not be adding an inherit override."),
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
        }
    }
}
