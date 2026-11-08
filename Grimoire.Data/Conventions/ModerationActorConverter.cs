// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Grimoire.Data.Conventions;

/// <summary>
///     Stores a <see cref="ModerationActor" /> as the moderator's id, or null for the bot itself. The provider type is
///     <c>decimal?</c> rather than <c>ulong?</c> because EF's own ulong-to-decimal step can't take a null, and
///     <c>convertsNulls</c> is what lets a null column come back as a System actor.
/// </summary>
internal sealed class ModerationActorConverter()
    : ValueConverter<ModerationActor, decimal?>(actor => ToColumn(actor), value => FromColumn(value), convertsNulls: true)
{
    // Expression trees can't hold a switch, so the converter calls these instead.
    internal static decimal? ToColumn(ModerationActor actor)
        => actor switch
        {
            ModerationActor.Moderator moderator => moderator.Id.Value,
            _ => null
        };

    internal static ModerationActor FromColumn(decimal? value)
        => value is { } id
            ? new ModerationActor.Moderator(new ModeratorId((ulong)id))
            : new ModerationActor.System();
}
