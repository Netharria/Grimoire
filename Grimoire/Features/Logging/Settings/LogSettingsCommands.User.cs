// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.ComponentModel;

namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    [RequireModuleEnabled(Module.UserLog)]
    [Command("User")]
    [Description("View or change the User Log Module Settings.")]
    public partial class User(IMediator mediator)
    {
        private readonly IMediator _mediator = mediator;
    }
}
