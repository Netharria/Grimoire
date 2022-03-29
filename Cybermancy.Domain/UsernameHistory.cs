// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class UsernameHistory : IIdentifiable
    {
        public ulong Id { get; set; }
        public ulong UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public string Username { get; set; } = string.Empty;
        public DateTime Timestamp { get; } = DateTime.UtcNow;
    }
}
