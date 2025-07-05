// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading.Channels;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels;
using Channel = Grimoire.Domain.Channel;

namespace Grimoire.Features.Leveling.Rewards;


[Command("Rewards")]
[Description("Commands for updating and viewing the server rewards.")]
[RequireGuild]
[RequireModuleEnabled(Module.Leveling)]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
public sealed partial class RewardCommandGroup(IMediator mediator, Channel<PublishToGuildLog> channel)
{
    private readonly IMediator _mediator = mediator;
    private readonly Channel<PublishToGuildLog> _channel = channel;
}
