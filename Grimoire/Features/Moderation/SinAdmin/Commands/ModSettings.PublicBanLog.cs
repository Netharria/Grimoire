// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{
    [SlashCommand("PublicBanLog", "Set public channel to publish ban and unbans to.")]
    public async Task BanLogAsync(
        InteractionContext ctx,
        [Option("Option", "Select whether to turn log off, use the current channel, or specify a channel")]
        ChannelOption option,
        [Option("Channel", "The channel to send to send the logs to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferAsync(true);
        channel = ctx.GetChannelOptionAsync(option, channel);
        if (channel is not null)
        {
            var permissions = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!permissions.HasPermission(DiscordPermission.SendMessages))
                throw new AnticipatedException(
                    $"{ctx.Guild.CurrentMember.Mention} does not have permissions to send messages in that channel.");
        }

        var response = await this._mediator.Send(new SetBanLogChannel.Command
        {
            GuildId = ctx.Guild.Id, ChannelId = channel?.Id
        });

        if (option is ChannelOption.Off)
        {
            await ctx.EditReplyAsync(message: "Disabled the public ban log.");
            await ctx.SendLogAsync(response, GrimoireColor.Purple, $"{ctx.User.Mention} disabled the public ban log.");
            return;
        }

        await ctx.EditReplyAsync(message: $"Updated the public ban log to {channel?.Mention}");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.User.Mention} updated the public ban log to {channel?.Mention}.");
    }
}

internal sealed class SetBanLogChannel
{
    public sealed record Command : IRequest<BaseResponse>
    {
        public ulong GuildId { get; init; }
        public ulong? ChannelId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var guildModerationSettings = await dbContext.GuildModerationSettings
                .Include(x => x.Guild)
                .FirstOrDefaultAsync(x => x.GuildId.Equals(command.GuildId), cancellationToken);
            if (guildModerationSettings is null) throw new AnticipatedException("Could not find the Servers settings.");

            guildModerationSettings.PublicBanLog = command.ChannelId;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse { LogChannelId = guildModerationSettings.Guild.ModChannelLog };
        }
    }
}
