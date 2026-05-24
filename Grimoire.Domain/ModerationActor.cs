// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

public abstract record ModerationActor
{
    /// <summary>A known Discord moderator performed the action.</summary>
    public sealed record Moderator(ModeratorId Id) : ModerationActor;

    /// <summary>The system performed the action (automated or no moderator context available).</summary>
    public sealed record System : ModerationActor;
}
