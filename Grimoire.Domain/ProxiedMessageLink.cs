// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public sealed class ProxiedMessageLink
{
    public required ulong ProxyMessageId { get; init; }
    public Message? ProxyMessage { get; init; }
    public required ulong OriginalMessageId { get; init; }
    public Message? OriginalMessage { get; init; }
    public string? SystemId { get; init; }
    public string? MemberId { get; init; }
}
