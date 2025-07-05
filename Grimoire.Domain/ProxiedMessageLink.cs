// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

[UsedImplicitly]
public class ProxiedMessageLink
{
    public ulong ProxyMessageId { get; init; }
    public virtual Message ProxyMessage { get; init; } = null!;
    public ulong OriginalMessageId { get; init; }
    public virtual Message OriginalMessage { get; init; } = null!;
    public string? SystemId { get; init; }
    public string? MemberId { get; init; }
}
