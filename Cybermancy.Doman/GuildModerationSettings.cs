// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Cybermancy.Domain
{
    public enum Duration
    {
        Days = 1,
        Months = 2,
        Years = 3,
    }

    public class GuildModerationSettings
    {
        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public ulong? PublicBanLog { get; set; }

        public Duration DurationType { get; set; }

        public int Duration { get; set; }

        public ulong? MuteRole { get; set; }
        public bool IsModerationEnabled { get; set; }
    }
}
