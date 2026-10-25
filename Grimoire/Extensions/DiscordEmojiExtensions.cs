// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Extensions;

public static class DiscordEmojiExtensions
{
    extension(DiscordEmoji emoji)
    {
        /// <summary>DSharpPlus gives standard Unicode emoji an id of 0; every custom emoji has a real one.</summary>
        [Pure]
        public Validation<ReactionEmoji> ToReactionEmoji()
            => emoji.Id == 0
                ? ReactionEmoji.Standard.Create(emoji.Name).Map(ReactionEmoji (standard) => standard)
                : ReactionEmoji.Custom.Create(new EmojiId(emoji.Id), emoji.Name, emoji.IsAnimated)
                    .Map(ReactionEmoji (custom) => custom);
    }
}
