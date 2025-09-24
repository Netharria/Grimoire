// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed class GetCustomCommand
{
    public static bool IsUserAuthorized(DiscordMember member, bool restrictedUse,
        IReadOnlyCollection<ulong> permissionRoles) =>
        (restrictedUse
            ? member?.Roles.Any(x => permissionRoles.Contains(x.Id))
            : member?.Roles.All(x => !permissionRoles.Contains(x.Id))
        ) ?? false;

    [RequireGuild]
    [RequireModuleEnabled(Module.Commands)]
    public sealed class Command(IDbContextFactory<GrimoireDbContext> dbContextFactory)
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        [Command("Command")]
        [Description("Call a custom command.")]
        [UsedImplicitly]
        public async Task CallCommand(
            CommandContext ctx,
            [SlashAutoCompleteProvider<GetCustomCommandOptions.AutocompleteProvider>]
            [Parameter("CommandName")]
            [Description("The name of the command to call.")]
            string name,
            [Parameter("Mention")] [Description("The person to mention if the command has one.")]
            SnowflakeObject? snowflakeObject = null,
            [Parameter("Message")] [Description("The custom message to add if the command has one.")]
            string message = "")
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null || ctx.Member is null)
                throw new AnticipatedException("This command can only be used in a server.");

            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync();

            var response = await dbContext.CustomCommands
                .GetCustomCommandQuery(ctx.Guild.Id, name)
                .FirstOrDefaultAsync();

            if (response is null || !IsUserAuthorized(ctx.Member, response.RestrictedUse, response.PermissionRoles))
            {
                await ctx.DeleteResponseAsync();
                return;
            }

            var content = response.Content;

            if (response.HasMention)
                content = content.Replace(
                    "%Mention",
                    snowflakeObject switch
                    {
                        DiscordUser user => user.Mention,
                        DiscordRole role => role.Mention,
                        _ => string.Empty
                    }, StringComparison.OrdinalIgnoreCase);
            if (response.HasMessage)
                content = content.Replace("%Message", message, StringComparison.OrdinalIgnoreCase);

            var discordResponse = new DiscordWebhookBuilder();

            if (response.IsEmbedded)
            {
                var discordEmbed = new DiscordEmbedBuilder()
                    .WithDescription(content);
                if (response.EmbedColor is not null)
                    discordEmbed.WithColor(new DiscordColor(response.EmbedColor));
                discordResponse.AddEmbed(discordEmbed);
            }
            else
                discordResponse.WithContent(content);

            await ctx.EditResponseAsync(discordResponse);
        }
    }
}
