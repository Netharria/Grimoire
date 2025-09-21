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
public sealed class PublishedMessage
{
    public required ulong MessageId { get; init; }

    public required long SinId { get; init; }

    public Sin? Sin { get; init; }

    public required PublishType PublishType { get; init; }
}
