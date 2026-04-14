// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Extensions;

public static class MessageCreatedEventArgsExtensions
{
    extension(MessageCreatedEventArgs args)
    {
        [Pure]
        public UserId GetAuthorUserId() => new(args.Author.Id);

        [Pure]
        public GuildId? GetGuildId() =>
            // DSharpPlus hasn't finished implementing nullable notations
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            args.Guild is not null ? new GuildId(args.Guild.Id) : null;

        [Pure]
        public MessageId GetMessageId() => new(args.Message.Id);

        [Pure]
        public ChannelId GetChannelId() => new(args.Channel.Id);
    }
}
