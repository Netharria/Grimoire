// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain
{
    public class UsernameHistory : IIdentifiable<long>
    {
        public long Id { get; set; }
        public ulong UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public string Username { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; }
    }
}
