// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Discord.Extensions
{
    public static class DiscordUserExtensions
    {
        public static string GetUsernameWithDiscriminator(this DiscordUser discordUser)
            => $"{discordUser.Username}#{discordUser.Discriminator}";
    }
}
