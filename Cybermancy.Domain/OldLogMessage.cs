// -----------------------------------------------------------------------
// <copyright file="OldLogMessage.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Domain
{
    using System;
    using Cybermancy.Domain.Shared;

    public class OldLogMessage : Identifiable
    {
        public ulong ChannelId { get; set; }

        public ulong GuildId { get; set; }

        public virtual Guild Guild { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}