// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed class GetCustomCommand
{
    private static bool IsUserAuthorized(CommandContext ctx, Response response) =>
        (response.RestrictedUse
            ? ctx.Member?.Roles.Any(x => response.PermissionRoles.Contains(x.Id))
            : ctx.Member?.Roles.All(x => !response.PermissionRoles.Contains(x.Id))) ?? false;

    [RequireGuild]
    [RequireModuleEnabled(Module.Commands)]
    internal sealed class Command(IMediator mediator)
    {
        private readonly IMediator _mediator = mediator;

        [Command("Command")]
        [Description("Call a custom command.")]
        [UsedImplicitly]
        internal async Task CallCommand(
            SlashCommandContext ctx,
            [SlashAutoCompleteProvider<GetCustomCommandOptions.AutocompleteProvider>]
            [Parameter("CommandName")]
            [Description("The name of the command to call.")]
            string name,
            [Parameter("Mention")]
            [Description("The person to mention if the command has one.")]
            SnowflakeObject? snowflakeObject = null,
            [Parameter("Message")]
            [Description("The custom message to add if the command has one.")]
            string message = "")
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var response = await this._mediator.Send(new Request { Name = name, GuildId = ctx.Guild.Id });

            if (response is null || !IsUserAuthorized(ctx, response))
            {
                await ctx.Interaction.DeleteOriginalResponseAsync();
                return;
            }

            if (response.HasMention)
                response.Content = response.Content.Replace(
                    "%Mention",
                    snowflakeObject switch
                    {
                        DiscordUser user => user.Mention,
                        DiscordRole role => role.Mention,
                        _ => string.Empty
                    }, StringComparison.OrdinalIgnoreCase);
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
                discordResponse.WithContent(response.Content);

            await ctx.EditResponseAsync(discordResponse);
        }
    }

    public sealed record Request : IRequest<Response?>
    {
        public required string Name { get; init; }
        public required ulong GuildId { get; init; }
    }

    [UsedImplicitly]
    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Request query, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);

            return await dbContext.CustomCommands
                .AsSplitQuery()
                .Where(command => command.GuildId == query.GuildId && command.Name == query.Name)
                .Select(command => new Response
                {
                    Content = command.Content,
                    HasMention = command.HasMention,
                    HasMessage = command.HasMessage,
                    IsEmbedded = command.IsEmbedded,
                    EmbedColor = command.EmbedColor,
                    RestrictedUse = command.RestrictedUse,
                    PermissionRoles = command.CustomCommandRoles.Select(commandRole => commandRole.RoleId)
                }).FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record Response
    {
        public required string Content { get; set; }
        public required bool HasMention { get; init; }
        public required bool HasMessage { get; init; }
        public required bool IsEmbedded { get; init; }
        public required string? EmbedColor { get; init; }
        public required bool RestrictedUse { get; init; }
        public required IEnumerable<ulong> PermissionRoles { get; init; }
    }
}
