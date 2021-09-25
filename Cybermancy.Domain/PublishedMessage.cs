// -----------------------------------------------------------------------
// <copyright file="PublishedMessage.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Cybermancy.Domain.Shared;

namespace Cybermancy.Domain
{
    public enum PublishType
    {
        Ban,
        Unban,
    }

    public class PublishedMessage
    {
        public ulong MessageId { get; set; }

        public ulong SinId { get; set; }

        public virtual Sin Sin { get; set; }

        public PublishType PublishType { get; set; }
    }
}