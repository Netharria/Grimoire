// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using Grimoire.Settings.Domain.Shared;

namespace Grimoire.Settings.Domain;

public enum MessageLogOverrideOption
{
    AlwaysLog,
    NeverLog
}

public sealed class MessageLogChannelOverride : IGuildChannel
{
    public MessageLogOverrideOption ChannelOption { get; set; }
    public required ulong ChannelId { get; init; }
    public required ulong GuildId { get; init; }
}
