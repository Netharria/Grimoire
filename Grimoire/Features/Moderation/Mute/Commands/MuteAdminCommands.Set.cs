// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands.Processors.SlashCommands;

namespace Grimoire.Features.Moderation.Mute.Commands;

public partial class MuteAdminCommands
{
    [Command("Set")]
    [Description("Sets the role that is used for muting users.")]
    public async Task SetMuteRoleAsync(
        SlashCommandContext ctx,
        [Parameter("Role")]
        [Description("The role to use for muting users.")]
        DiscordRole role)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response = await this._mediator.Send(new SetMuteRole.Request { Role = role.Id, GuildId = ctx.Guild.Id });

        await ctx.EditReplyAsync(message: $"Will now use role {role.Mention} for muting users.");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.User.Mention} updated the mute role to {role.Mention}");
    }
}

public sealed class SetMuteRole
{
    public sealed record Request : IRequest<BaseResponse>
    {
        public ulong Role { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var guildModerationSettings = await dbContext.GuildModerationSettings
                .Include(x => x.Guild)
                .FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken);
            if (guildModerationSettings is null) throw new AnticipatedException("Could not find the Servers settings.");

            guildModerationSettings.MuteRole = command.Role;

            await dbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse { LogChannelId = guildModerationSettings.Guild.ModChannelLog };
        }
    }
}
