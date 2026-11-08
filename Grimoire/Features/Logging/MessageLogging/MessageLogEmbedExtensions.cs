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
        public DiscordEmbedBuilder AddMessageAuthorFields(UserId author, UserId? originalAuthor,
            PluralKitSystemId? systemId, PluralKitMemberId? memberId)
            => originalAuthor is null
                ? embed.AddField("Author", UserExtensions.Mention(author), true)
                : embed.AddField("Original Author", UserExtensions.Mention(originalAuthor), true)
                    .AddField("System Id", OrPrivate(systemId?.Value), true)
                    .AddField("Member Id", OrPrivate(memberId?.Value), true);
    }

    private static string OrPrivate(string? id) => string.IsNullOrWhiteSpace(id) ? "Private" : id;
}
