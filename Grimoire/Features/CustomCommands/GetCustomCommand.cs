// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.CustomCommands;
public sealed class GetCustomCommand
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Commands)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Command", "Call a custom command.")]
        internal async Task CallCommand(
            InteractionContext ctx,
            [Autocomplete(typeof(GetCustomCommandOptions.AutocompleteProvider))]
        [Option("CommandName", "Enter the name of the command.", autocomplete: true)]string name,
            [Option("Mention", "The person to mention if the command has one.")] SnowflakeObject? snowflakeObject = null,
            [Option("Message", "The custom message to add if the command has one.")] string message = "")
        {
            await ctx.DeferAsync();

            var response = await this._mediator.Send(new Request
            {
                Name = name,
                GuildId = ctx.Guild.Id
            });

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

    private static bool IsUserAuthorized(InteractionContext ctx, Response response) =>
            response.RestrictedUse
                ? ctx.Member.Roles.Any(x => response.PermissionRoles.Contains(x.Id))
                : ctx.Member.Roles.All(x => !response.PermissionRoles.Contains(x.Id));

    public sealed record Request : IRequest<Response>
    {
        public required string Name { get; set; }
        public required ulong GuildId { get; set; }
    }

    public sealed class Handler(GrimoireDbContext GrimoireDbContext) : IRequestHandler<Request, Response?>
    {
        private readonly GrimoireDbContext _grimoireDbContext = GrimoireDbContext;

        public async ValueTask<Response?> Handle(Request query, CancellationToken cancellationToken)
            => await this._grimoireDbContext.CustomCommands
            .AsSplitQuery()
            .Where(x => x.GuildId == query.GuildId && x.Name == query.Name)
            .Select(x => new Response
            {
                Content = x.Content,
                HasMention = x.HasMention,
                HasMessage = x.HasMessage,
                IsEmbedded = x.IsEmbedded,
                EmbedColor = x.EmbedColor,
                RestrictedUse = x.RestrictedUse,
                PermissionRoles = x.CustomCommandRoles.Select(x => x.RoleId),

            }).FirstOrDefaultAsync(cancellationToken);
    }

    public sealed record Response
    {
        public required string Content { get; set; }
        public required bool HasMention { get; set; }
        public required bool HasMessage { get; set; }
        public required bool IsEmbedded { get; set; }
        public required string? EmbedColor { get; set; }
        public required bool RestrictedUse { get; set; }
        public required IEnumerable<ulong> PermissionRoles { get; set; }
    }
}
