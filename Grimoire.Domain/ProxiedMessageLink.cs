// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain;
public class ProxiedMessageLink
{
    public ulong ProxyMessageId { get; set; }
    public virtual Message ProxyMessage { get; set; } = null!;
    public ulong OriginalMessageId { get; set; }
    public virtual Message OriginalMessage { get; set;} = null!;
    public string? SystemId { get; set; }
    public string? MemberId {  get; set; }
}
