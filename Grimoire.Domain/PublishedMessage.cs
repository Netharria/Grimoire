// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using JetBrains.Annotations;

namespace Grimoire.Domain;

public enum PublishType
{
    Ban,
    Unban
}

[UsedImplicitly]
public class PublishedMessage
{
    public ulong MessageId { get; init; }

    public long SinId { get; init; }

    public virtual Sin Sin { get; init; } = null!;

    public PublishType PublishType { get; init; }
}
