// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.CustomCommands.Queries;

namespace Grimoire.Discord.Attributes;
internal class CustomCommandAutoCompleteAttribute(IMediator mediator) : IAutocompleteProvider
{
    private readonly IMediator _mediator = mediator;

    public async Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
    {
        var results = await this._mediator.Send(new GetCustomCommandOptions.Query
        {
            EnteredText = (string) ctx.FocusedOption.Value,
            GuildId = ctx.Guild.Id
        });
        return results
            .Select(x => new DiscordAutoCompleteChoice(
                x.Name + " " + (x.HasMention ? "<Mention> " : "") + (x.HasMessage ? "<Message>" : ""),
                x.Name));
    }
}
