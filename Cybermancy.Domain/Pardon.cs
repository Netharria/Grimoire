// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;

namespace Cybermancy.Domain
{
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