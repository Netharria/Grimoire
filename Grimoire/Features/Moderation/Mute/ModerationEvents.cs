// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Mute;

public sealed class ModerationEvents(IMediator mediator)
{
    private readonly IMediator _mediator = mediator;

    public async Task DiscordOnGuildMemberAdded(DiscordClient sender, GuildMemberAddedEventArgs args)
    {
        var response = await this._mediator.Send(new GetUserMuteQuery
        {
            UserId = args.Member.Id, GuildId = args.Guild.Id
        });
        if (response is null) return;
        var role = args.Guild.Roles.GetValueOrDefault(response.Value);
        if (role is null) return;
        await args.Member.GrantRoleAsync(role, "Rejoined while muted");
    }
}
