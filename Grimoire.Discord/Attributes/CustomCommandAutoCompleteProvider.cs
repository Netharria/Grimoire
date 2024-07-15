// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.CustomCommands.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Discord.Attributes;
internal sealed class CustomCommandAutoCompleteProvider(IServiceProvider serviceProvider) : IAutocompleteProvider
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
    {
        await using var scope = this._serviceProvider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetService<IMediator>();

        if(mediator is null)
            return [];

        var results = await mediator.Send(new GetCustomCommandOptions.Query
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
