// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Core.Features.Shared.Commands.MemberCommands.AddMember;
using Cybermancy.Core.Features.Shared.Commands.MemberCommands.UpdateMember;
using Cybermancy.Core.Features.Shared.Commands.MemberCommands.UpdateUser;
using Cybermancy.Discord.Extensions;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Mediator;
using Nefarius.DSharpPlus.Extensions.Hosting.Events;

namespace Cybermancy.Discord.DatabaseManagementModules
{
    [DiscordGuildMemberAddedEventSubscriber]
    [DiscordGuildMemberUpdatedEventSubscriber]
    [DiscordUserUpdatedEventSubscriber]
    internal class MemberEventManagementModule :
        IDiscordGuildMemberAddedEventSubscriber,
        IDiscordGuildMemberUpdatedEventSubscriber,
        IDiscordUserUpdatedEventSubscriber
    {
        private readonly IMediator _mediator;

        public MemberEventManagementModule(IMediator mediator)
        {
            this._mediator = mediator;
        }

        public async Task DiscordOnGuildMemberAdded(DiscordClient sender, GuildMemberAddEventArgs args)
            => await this._mediator.Send(
                new AddMemberCommand
                {
                    Nickname = string.IsNullOrWhiteSpace(args.Member.DisplayName) ? null : args.Member.DisplayName,
                    GuildId = args.Guild.Id,
                    UserId = args.Member.Id,
                    UserName = args.Member.GetUsernameWithDiscriminator()
                });

        public async Task DiscordOnGuildMemberUpdated(DiscordClient sender, GuildMemberUpdateEventArgs args)
            => await this._mediator.Send(
                new UpdateMemberCommand
                {
                    Nickname = string.IsNullOrWhiteSpace(args.NicknameAfter) ? null : args.NicknameAfter,
                    GuildId = args.Guild.Id,
                    UserId = args.Member.Id,
                });

        public async Task DiscordOnUserUpdated(DiscordClient sender, UserUpdateEventArgs args)
            => await this._mediator.Send(
                new UpdateUserCommand
                {
                    UserId = args.UserAfter.Id,
                    UserName = args.UserAfter.GetUsernameWithDiscriminator()
                });
    }
}
