// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using JetBrains.Annotations;

namespace Grimoire.Domain;

public enum MessageLogOverrideOption
{
    AlwaysLog,
    NeverLog
}

[UsedImplicitly]
public class MessageLogChannelOverride
{
    public ulong ChannelId { get; init; }
    public virtual Channel Channel { get; init; } = null!;
    public ulong GuildId { get; init; }
    public virtual Guild Guild { get; init; } = null!;
    public MessageLogOverrideOption ChannelOption { get; set; }
}
