// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;

public class GuildMessageLogSettings : IModule
{
    public ulong GuildId { get; set; }

    public virtual Guild Guild { get; set; } = null!;

    public ulong? DeleteChannelLogId { get; set; }

    public virtual Channel? DeleteChannelLog { get; set; }

    public ulong? BulkDeleteChannelLogId { get; set; }

    public virtual Channel? BulkDeleteChannelLog { get; set; }

    public ulong? EditChannelLogId { get; set; }

    public virtual Channel? EditChannelLog { get; set; }

    public bool ModuleEnabled { get; set; }
}
