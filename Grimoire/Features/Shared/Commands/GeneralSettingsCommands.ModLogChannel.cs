// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.Processors.SlashCommands;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Channels;
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class GeneralSettingsCommands
{
    [UsedImplicitly]
    [Command("ModLogChannel")]
    [Description("Set the moderation log channel.")]
    public async Task SetAsync(
        SlashCommandContext ctx,
        [Parameter("Option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel.")]
        ChannelOption option,
        [Parameter("Channel")]
        [Description("The channel to send the logs to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermission.SendMessages))
                throw new AnticipatedException(
                    $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }

        var response = await this._mediator.Send(new SetModLog.Request
        {
            GuildId = ctx.Guild.Id, ChannelId = channel?.Id
        });
        if (option is ChannelOption.Off)
        {
            await ctx.EditReplyAsync(message: "Disabled the moderation log.");
            await this._channel.Writer.WriteAsync(new PublishToGuildLog
            {
                LogChannelId = response.LogChannelId,
                Description = $"{ctx.User.Mention} disabled the level log.",
                Color = GrimoireColor.Purple

            });
            return;
        }

        await ctx.EditReplyAsync(message: $"Updated the moderation log to {channel?.Mention}");
        await this._channel.Writer.WriteAsync(new PublishToGuildLog
        {
            LogChannelId = response.LogChannelId,
            Description = $"{ctx.User.Mention} updated the moderation log to {channel?.Mention}.",
            Color = GrimoireColor.Purple

        });
    }
}

internal sealed class SetModLog
{
    internal sealed record Request : IRequest<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public ulong? ChannelId { get; init; }
    }

    [UsedImplicitly]
    internal sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var guild = await dbContext.Guilds
                .WhereIdIs(command.GuildId)
                .FirstOrDefaultAsync(cancellationToken);
            if (guild is null)
                throw new AnticipatedException("Could not find the settings for this server.");
            guild.ModChannelLog = command.ChannelId;

            await dbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { LogChannelId = command.ChannelId };
        }
    }
}
