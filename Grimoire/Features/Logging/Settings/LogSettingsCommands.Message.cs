// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.Settings;
public partial class LogSettingsCommands
{
    [SlashCommandGroup("Message", "View or change the Message Log Module Settings.")]
    [SlashRequireModuleEnabled(Module.MessageLog)]
    public partial class Message(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;
    }
}
