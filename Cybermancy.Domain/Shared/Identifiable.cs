// -----------------------------------------------------------------------
// <copyright file="Identifiable.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Domain.Shared
{
    public abstract class Identifiable
    {
        public ulong Id { get; set; }
    }
}