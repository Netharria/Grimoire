// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.Processors.SlashCommands;
using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class GeneralSettingsCommands
{
    [Command("UserCommands")]
    [Description("Set the channel where some commands are visible for non moderators.")]
    public async Task SetUserCommandChannelAsync(
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
        var response = await this._mediator.Send(new SetUserCommandChannel.Request
        {
            GuildId = ctx.Guild.Id, ChannelId = channel?.Id
        });
        if (option is ChannelOption.Off)
        {
            await ctx.EditReplyAsync(message: "Disabled the User Command Channel.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple,
                $"{ctx.User.Mention} disabled the User Command Channel.");
            return;
        }

        await ctx.EditReplyAsync(message: $"Updated the User Command Channel to {channel?.Mention}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.User.Mention} updated the User Command Channel to {channel?.Mention}.");
    }
}

public sealed class SetUserCommandChannel
{
    public sealed record Request : IRequest<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public ulong? ChannelId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Request request, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var guild = await dbContext.Guilds
                .WhereIdIs(request.GuildId)
                .FirstOrDefaultAsync(cancellationToken);
            if (guild is null)
                throw new AnticipatedException("Could not find the settings for this server.");
            guild.UserCommandChannelId = request.ChannelId;

            await dbContext.SaveChangesAsync(cancellationToken);
            return new BaseResponse { LogChannelId = guild.ModChannelLog };
        }
    }
}
