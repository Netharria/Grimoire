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
using Channel = System.Threading.Channels.Channel;

namespace Grimoire.Features.Shared.Commands;

[Command("GeneralSettings")]
[Description("Change the settings of the General Module.")]
[RequireGuild]
[RequireUserGuildPermissions(DiscordPermission.ManageGuild)]
internal sealed partial class GeneralSettingsCommands(IMediator mediator, Channel<PublishToGuildLog> channel)
{
    private readonly IMediator _mediator = mediator;
    private readonly Channel<PublishToGuildLog> _channel = channel;
}
