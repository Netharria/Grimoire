// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Extensions;

public static class MessageCreatedEventArgsExtensions
{
    [Pure]
    public static UserId GetAuthorUserId(this MessageCreatedEventArgs args) => new (args.Author.Id);
    [Pure]
    // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    public static GuildId? GetGuildId(this MessageCreatedEventArgs args) => args.Guild is not null ? new GuildId(args.Guild.Id) : null;
    [Pure]
    public static MessageId GetMessageId(this MessageCreatedEventArgs args) => new (args.Message.Id);
    [Pure]
    public static ChannelId GetChannelId(this MessageCreatedEventArgs args) => new (args.Channel.Id);
}
