// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Domain;

/// <summary>Represents a successful result with no meaningful value — the functional equivalent of <c>void</c>.</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
