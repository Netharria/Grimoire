// -----------------------------------------------------------------------
// <copyright file="UserExtensions.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using Cybermancy.Domain;

namespace Cybermancy.Core.Extensions
{
    public static class UserExtensions
    {
        public static UserLevel GetUserLevel(this User user, ulong guildId) => user.UserLevels.FirstOrDefault(x => x.GuildId == guildId);
    }
}
