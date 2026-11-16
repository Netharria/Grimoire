// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using LanguageExt;

namespace Grimoire.Functional.Discord;

/// <summary>
///     What one Discord call was, for classifying its failure and wording the error (spec 1.4, section 3.3). From
///     section 1.6, <see cref="Name" /> is also the span name.
/// </summary>
/// <param name="Name">"Discord.GetMember", "Discord.SendMessage". Never holds content or a name.</param>
/// <param name="Action">What the bot was doing, as the user reads it: "send a message", "look up the member".</param>
/// <param name="Target">What it was done to: "that member", "&lt;#123&gt;". Used in NotFound's message.</param>
/// <param name="BotPermission">The permission the call needs, named as Discord's settings show it. None for a lookup.</param>
/// <param name="AboutRoleOrder">True when Discord also refuses because the bot's role is below the target's.</param>
/// <param name="IsGuildLookup">The lookup of the guild itself: a 403 there means the bot isn't in it (spec 1.4, OQ-9).</param>
internal sealed record DiscordOperation(
    string Name,
    string Action,
    string Target,
    Option<GuildId> Guild = default,
    Option<ChannelId> Channel = default,
    Option<string> BotPermission = default,
    bool AboutRoleOrder = false,
    bool IsGuildLookup = false);
