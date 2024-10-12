// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Features.Shared.Commands;

namespace Grimoire.DatabaseManagementModules;

internal sealed class MemberEventManagementModule(IMediator mediator)
{
    private readonly IMediator _mediator = mediator;

    public async Task DiscordOnGuildMemberAdded(DiscordClient sender, GuildMemberAddedEventArgs args)
        => await this._mediator.Send(
            new AddMemberCommand
            {
                Nickname = string.IsNullOrWhiteSpace(args.Member.Nickname) ? null : args.Member.Nickname,
                GuildId = args.Guild.Id,
                UserId = args.Member.Id,
                UserName = args.Member.GetUsernameWithDiscriminator(),
                AvatarUrl = args.Member.GetGuildAvatarUrl(ImageFormat.Auto, 128)
            });

}
