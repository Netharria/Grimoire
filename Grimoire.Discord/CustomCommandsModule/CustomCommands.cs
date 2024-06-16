// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.CustomCommands.Queries;

namespace Grimoire.Discord.CustomCommandsModule;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Commands)]
internal sealed class CustomCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("Command", "Call a custom command.")]
    internal async Task Command(
        InteractionContext ctx,
        [Autocomplete(typeof(CustomCommandAutoCompleteAttribute))]
        [Option("CommandName", "Enter the name of the command.", autocomplete: true)]string name,
        [Option("Mention", "The person to mention if the command has one.")] SnowflakeObject? snowflakeObject = null,
        [Option("Message", "The custom message to add if the command has one.")] string message = "")
    {
        await ctx.DeferAsync();

        var response = await this._mediator.Send(new GetCommand.Query
        {
            Name = name,
            GuildId = ctx.Guild.Id
        });
        if(response is null)
        {
            await ctx.Interaction.DeleteOriginalResponseAsync();
            return;
        }
        if ((response.RestrictedUse && ctx.Member.Roles.All(x => !response.PermissionRoles.Contains(x.Id)))
            || (!response.RestrictedUse && ctx.Member.Roles.Any(x => response.PermissionRoles.Contains(x.Id))))
        {
            await ctx.Interaction.DeleteOriginalResponseAsync();
            return;
        }

        if (response.HasMention)
            response.Content = response.Content.Replace(
                "%Mention",
                snowflakeObject is null
                ? ""
                : snowflakeObject is DiscordUser user
                ? user.Mention
                : snowflakeObject is DiscordRole role
                ? role.Mention : "", StringComparison.OrdinalIgnoreCase);
        if (response.HasMessage)
            response.Content = response.Content.Replace("%Message", message, StringComparison.OrdinalIgnoreCase);

        var discordResponse = new DiscordWebhookBuilder();

        if (response.IsEmbedded)
        {
            var discordEmbed = new DiscordEmbedBuilder()
                .WithDescription(response.Content);
            if (response.EmbedColor is not null)
                discordEmbed.WithColor(new DiscordColor(response.EmbedColor));
            discordResponse.AddEmbed(discordEmbed);
        }
        else
        {
            discordResponse.WithContent(response.Content);
        }
        await ctx.EditResponseAsync(discordResponse);
    }
}
