// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;
using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class GuildMessageLogSettings : IModule
{
    public ulong GuildId { get; init; }
    [Obsolete("Temporary to help identify where configurations are changed.")]
    public ulong? DeleteChannelLogId { get; set; }

    public virtual Channel? DeleteChannelLog { get; init; }
    [Obsolete("Temporary to help identify where configurations are changed.")]
    public ulong? BulkDeleteChannelLogId { get; set; }

    public virtual Channel? BulkDeleteChannelLog { get; init; }
    [Obsolete("Temporary to help identify where configurations are changed.")]
    public ulong? EditChannelLogId { get; set; }

    public virtual Channel? EditChannelLog { get; init; }

    public virtual Guild Guild { get; set; } = null!;
    [Obsolete("Temporary to help identify where configurations are changed.")]
    public bool ModuleEnabled { get; set; }
}
