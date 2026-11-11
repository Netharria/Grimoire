// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.CustomCommands;

/// <summary>
///     The bot's own commands also run as text commands with the same <c>!</c> prefix as custom commands, so a custom
///     command may not share a name with one: <c>!leaderboard</c> always means <c>/Leaderboard</c> (KI-85).
/// </summary>
public static class BuiltInCommandNames
{
    extension(CommandsExtension extension)
    {
        /// <summary>
        ///     Whether <paramref name="name" /> is a top-level bot command. Text commands match names whatever their
        ///     case, so this does too. Grimoire gives no command a text alias; one added later must be checked here too.
        /// </summary>
        public bool IsBuiltInCommandName(string name)
            => extension.Commands.Values.Any(command =>
                string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
