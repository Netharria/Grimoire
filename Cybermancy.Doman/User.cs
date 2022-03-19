// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class User : Identifiable
    {
        public string UserName { get; set; } = string.Empty;

        public string AvatarUrl { get; set; } = string.Empty;

        public virtual ICollection<GuildUser> GuildMemberProfiles { get; set; } = new List<GuildUser>();
        public virtual ICollection<UsernameHistory> UsernameHistories { get; set; } = new List<UsernameHistory>();
    }
}
