// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public class Attachment : IIdentifiable<ulong>
    {
        public ulong Id { get; set; }
        public ulong MessageId { get; set; }
        public virtual Message Message { get; set; } = null!;
        public string FileName { get; set; } = string.Empty;
    }
}
