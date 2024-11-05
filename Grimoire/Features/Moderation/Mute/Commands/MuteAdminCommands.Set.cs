﻿// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Mute.Commands;

public partial class MuteAdminCommands
{
    [SlashCommand("Set", "Sets the role that is used for muting users.")]
    public async Task SetMuteRoleAsync(
        InteractionContext ctx,
        [Option("Role", "The role to use for muting")]
        DiscordRole role)
    {
        await ctx.DeferAsync();
        var response = await this._mediator.Send(new SetMuteRole.Request { Role = role.Id, GuildId = ctx.Guild.Id });

        await ctx.EditReplyAsync(message: $"Will now use role {role.Mention} for muting users.");
        await ctx.SendLogAsync(response, GrimoireColor.Purple,
            message: $"{ctx.Member.Mention} updated the mute role to {role.Mention}");
    }
}

public sealed class SetMuteRole
{
    public sealed record Request : IRequest<BaseResponse>
    {
        public ulong Role { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext)
        : IRequestHandler<Request, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            var guildModerationSettings = await this._grimoireDbContext.GuildModerationSettings
                .Include(x => x.Guild)
                .FirstOrDefaultAsync(x => x.GuildId == command.GuildId, cancellationToken);
            if (guildModerationSettings is null) throw new AnticipatedException("Could not find the Servers settings.");

            guildModerationSettings.MuteRole = command.Role;

            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

            return new BaseResponse { LogChannelId = guildModerationSettings.Guild.ModChannelLog };
        }
    }
}
