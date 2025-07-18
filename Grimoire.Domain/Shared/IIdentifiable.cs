// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Numerics;

namespace Grimoire.Domain.Shared;

public interface IIdentifiable<out T> where T : IBinaryInteger<T>
{
    public T Id { get; }
}
