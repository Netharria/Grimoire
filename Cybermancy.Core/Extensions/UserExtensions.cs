// -----------------------------------------------------------------------
// <copyright file="UserExtensions.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Core.Extensions
{
    using System.Linq;
    using Cybermancy.Domain;

    public static class UserExtensions
    {
        public static UserLevel GetUserLevel(this User user, ulong guildId)
        {
            return user.UserLevels.FirstOrDefault(x => x.GuildId == guildId);
        }
    }
}
