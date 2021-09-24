// -----------------------------------------------------------------------
// <copyright file="Pardon.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace Cybermancy.Domain
{
    using System;

    public class Pardon
    {
        public ulong SinId { get; set; }

        public virtual Sin Sin { get; set; }

        public ulong ModeratorId { get; set; }

        public virtual User Moderator { get; set; }

        public DateTime PardonDate { get; set; }

        public string Reason { get; set; }
    }
}