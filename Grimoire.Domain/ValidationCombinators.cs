// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Grimoire.Domain;

public static class Validation
{
    public static Validation<(T1, T2, T3, T4)> Combine<T1, T2, T3, T4>(
        Validation<T1> v1, Validation<T2> v2, Validation<T3> v3, Validation<T4> v4)
    {
        if (v1 is Validation<T1>.Valid(var a) && v2 is Validation<T2>.Valid(var b)
                                              && v3 is Validation<T3>.Valid(var c) && v4 is Validation<T4>.Valid(var d))
            return Validation<(T1, T2, T3, T4)>.Succeed((a, b, c, d));
        var errors = ImmutableArray.CreateBuilder<Error>();
        if (v1 is Validation<T1>.Invalid(var e1)) errors.AddRange(e1);
        if (v2 is Validation<T2>.Invalid(var e2)) errors.AddRange(e2);
        if (v3 is Validation<T3>.Invalid(var e3)) errors.AddRange(e3);
        if (v4 is Validation<T4>.Invalid(var e4)) errors.AddRange(e4);
        return new Validation<(T1, T2, T3, T4)>.Invalid(errors.ToImmutable());
    }
}
