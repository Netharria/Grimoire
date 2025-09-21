// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed partial class ModSettings
{
    [Command("PublicBanLog")]
    [Description("Set the public channel to publish ban and unbans to.")]
    public async Task BanLogAsync(
        SlashCommandContext ctx,
        [Parameter("Option")]
        [Description("Select whether to turn log off, use the current channel, or specify a channel.")]
        ChannelOption option,
        [Parameter("Channel")] [Description("The channel to send the logs to.")]
        DiscordChannel? channel = null)
    {
        await ctx.DeferResponseAsync(true);

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

        await this._mediator.Send(new SetBanLogChannel.Command { GuildId = ctx.Guild.Id, ChannelId = channel?.Id });

        await ctx.EditReplyAsync(message: option is ChannelOption.Off
            ? "Disabled the public ban log."
            : $"Updated the public ban log to {channel?.Mention}");
        await this._guildLog.SendLogMessageAsync(new GuildLogMessage
        {
            GuildId = ctx.Guild.Id,
            GuildLogType = GuildLogType.Moderation,
            Color = GrimoireColor.Purple,
            Description = option is ChannelOption.Off
                ? $"{ctx.User.Mention} disabled the public ban log."
                : $"{ctx.User.Mention} updated the public ban log to {channel?.Mention}."
        });
    }
}

internal sealed class SetBanLogChannel
{
    public sealed record Command : IRequest
    {
        public GuildId GuildId { get; init; }
        public ulong? ChannelId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Command>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task Handle(Command command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var guildModerationSettings = await dbContext.GuildModerationSettings
                .FirstOrDefaultAsync(x => x.GuildId.Equals(command.GuildId), cancellationToken);
            if (guildModerationSettings is null) throw new AnticipatedException("Could not find the Servers settings.");

            guildModerationSettings.PublicBanLog = command.ChannelId;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
