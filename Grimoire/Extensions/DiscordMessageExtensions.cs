// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Extensions;

public static class DiscordMessageExtensions
{
    extension(DiscordMessage message)
    {
        [Pure]
        public MessageId GetMessageId() => new(message.Id);

        [Pure]
        public MessageContent GetMessageContent()
            => MessageContent.Create(message.Content).Match(c => c, _ => throw new UnreachableException());
    }
}
