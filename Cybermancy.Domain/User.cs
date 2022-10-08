// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class User : IIdentifiable<ulong>, IMentionable
    {
        public ulong Id { get; set; }
        public virtual ICollection<Member> MemberProfiles { get; set; } = new List<Member>();
        public virtual ICollection<UsernameHistory> UsernameHistories { get; set; } = new List<UsernameHistory>();
    }
}
