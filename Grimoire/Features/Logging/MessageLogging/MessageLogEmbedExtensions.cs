// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.MessageLogging;

internal static class MessageLogEmbedExtensions
{
    extension(DiscordEmbedBuilder embed)
    {
        /// <summary>
        ///     Adds who wrote the message, from the stored ids: the message is often no longer cached, and a mention
        ///     needs no lookup. A PluralKit proxy names the account behind it instead, with its system and member.
        /// </summary>
        public DiscordEmbedBuilder AddMessageAuthorFields(UserId author, ProxyAuthor? proxy)
            => proxy is null
                ? embed.AddField("Author", UserExtensions.Mention(author), true)
                : embed.AddField("Original Author", UserExtensions.Mention(proxy.OriginalUserId), true)
                    .AddField("System Id", proxy.SystemId.Value, true)
                    .AddField("Member Id", proxy.MemberId.Value, true);
    }
}

/// <summary>
///     The account and PluralKit ids behind a proxy, from its stored <see cref="ProxiedMessageLink" /> (LOG-3). Its ids
///     are required and non-blank there, so there's no "Private" case (Q37).
/// </summary>
public sealed record ProxyAuthor(UserId OriginalUserId, PluralKitSystemId SystemId, PluralKitMemberId MemberId);
