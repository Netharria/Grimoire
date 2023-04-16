// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Domain.Shared;

namespace Grimoire.Domain
{
    public class User : IIdentifiable<ulong>, IMentionable
    {
        public ulong Id { get; set; }
        public virtual ICollection<Member> MemberProfiles { get; set; } = new List<Member>();
        public virtual ICollection<UsernameHistory> UsernameHistories { get; set; } = new List<UsernameHistory>();
    }
}
