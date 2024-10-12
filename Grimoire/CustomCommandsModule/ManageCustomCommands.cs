// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Grimoire.Features.CustomCommands.Commands;

namespace Grimoire.CustomCommandsModule;

[SlashCommandGroup("Commands", "Manage custom commands.")]
[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Commands)]
[SlashRequireUserGuildPermissions(DiscordPermissions.ManageGuild)]
internal sealed partial class ManageCustomCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [GeneratedRegex(@"[0-9A-Fa-f]{6}\b", RegexOptions.Compiled, 1000)]
    public static partial Regex ValidHexColor();

    [SlashCommand("Learn", "Learn a new command or update an existing one")]
    internal async Task Learn(
        InteractionContext ctx,
        [MaximumLength(24)]
        [MinimumLength(0)]
        [Option("Name", "The name that the command will be called. This is used to activate the command.")] string name,
        [MaximumLength(2000)]
        [Option("Content", "The content of the command. Use %mention or %message to add a message arguments")] string content,
        [Option("Embed", "Put the message in an embed")] bool embed = false,
        [MaximumLength(6)]
        [Option("EmbedColor", "Hexadecimal color of the embed")] string? embedColor = null,
        [Option("RestricedUse", "Only explictly allowed roles can use this command")] bool restrictedUse = false,
        [Option("PermissionRoles", "Deny roles the ability to use this command or allow roles if command is restricted use")] string allowedRolesText = "")
    {
        await ctx.DeferAsync();

        if (name.Split(' ').Length > 1)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, message: $"No spaces are allowed in command name.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(embedColor) && !ValidHexColor().IsMatch(embedColor))
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, message: $"Embed Color provided but is not a valid hex color code.");
            return;
        }

        var permissionRoles = await ParseStringAndGetRoles(ctx, allowedRolesText);

        if (permissionRoles.Length == 0 && restrictedUse)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, message: $"Command set as restricted but no roles allowed to use it.");
            return;
        }

        var response = await this._mediator.Send(new AddCustomCommand.Command
        {
            CommandName = name,
            GuildId = ctx.Guild.Id,
            Content = content,
            IsEmbedded = embed,
            EmbedColor = embedColor,
            RestrictedUse = restrictedUse,
            PermissionRoles = permissionRoles,
        });

        await ctx.EditReplyAsync(GrimoireColor.Green, response.Message);
        await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
    }

    internal async static ValueTask<RoleDto[]> ParseStringAndGetRoles(InteractionContext ctx, string rolesText)
    {
        if (string.IsNullOrWhiteSpace(rolesText))
            return [];

        var matchedIds = await ctx.ParseStringIntoIdsAndGroupByTypeAsync(rolesText);

        if (!matchedIds.TryGetValue("Role", out var matchedRoles))
            return [];
        return matchedRoles
            .Select(x =>
                new RoleDto
                {
                    Id = ulong.Parse(x),
                    GuildId = ctx.Guild.Id
                }).ToArray();
    }

    [SlashCommand("Forget", "Forget a command")]
    internal async Task Forget(
        InteractionContext ctx,
        [Autocomplete(typeof(CustomCommandAutoCompleteProvider))]
        [Option("Name", "The name that the command is called.", true)] string name)
    {
        await ctx.DeferAsync();

        var response = await this._mediator.Send(new RemoveCustomCommand.Command
        {
            CommandName = name,
            GuildId = ctx.Guild.Id,
        });

        await ctx.EditReplyAsync(GrimoireColor.Green, response.Message);
        await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
    }
}
