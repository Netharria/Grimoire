// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.Text.RegularExpressions;
using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    [GeneratedRegex(@"[0-9A-Fa-f]{6}\b", RegexOptions.Compiled, 1000)]
    private static partial Regex ValidHexColor();

    [SlashCommand("Learn", "Learn a new command or update an existing one")]
    public async Task Learn(
        InteractionContext ctx,
        [MaximumLength(24)]
        [MinimumLength(0)]
        [Option("Name", "The name that the command will be called. This is used to activate the command.")]
        string name,
        [MaximumLength(2000)]
        [Option("Content", "The content of the command. Use %mention or %message to add a message arguments")]
        string content,
        [Option("Embed", "Put the message in an embed")]
        bool embed = false,
        [MaximumLength(6)] [Option("EmbedColor", "Hexadecimal color of the embed")]
        string? embedColor = null,
        [Option("RestrictedUse", "Only explicitly allowed roles can use this command")]
        bool restrictedUse = false,
        [Option("PermissionRoles",
            "Deny roles the ability to use this command or allow roles if command is restricted use")]
        string allowedRolesText = "")
    {
        await ctx.DeferAsync();

        if (name.Contains(' '))
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "No spaces are allowed in command name.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(embedColor) && !ValidHexColor().IsMatch(embedColor))
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Embed Color provided but is not a valid hex color code.");
            return;
        }

        var permissionRoles = await ParseStringAndGetRoles(ctx, allowedRolesText)
            .ToListAsync();

        if (restrictedUse && permissionRoles.Count != 0)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Command set as restricted but no roles allowed to use it.");
            return;
        }

        var response = await this._mediator.Send(new AddCustomCommand.Request
        {
            CommandName = name,
            GuildId = ctx.Guild.Id,
            Content = content,
            IsEmbedded = embed,
            EmbedColor = embedColor,
            RestrictedUse = restrictedUse,
            PermissionRoles = permissionRoles
        });

        await ctx.EditReplyAsync(GrimoireColor.Green, response.Message);
        await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
    }

    private static async IAsyncEnumerable<RoleDto> ParseStringAndGetRoles(InteractionContext ctx, string rolesText)
    {
        if (string.IsNullOrWhiteSpace(rolesText))
            yield break;

        await foreach (var role in ctx.ParseStringIntoIdsAndGroupByTypeAsync(rolesText)
                           .Where(x => x.Key == "Role")
                           .SelectMany(roleList =>
                               roleList.Select(role =>
                                   new RoleDto { Id = ulong.Parse(role), GuildId = ctx.Guild.Id })))
            yield return role;
    }
}

public sealed class AddCustomCommand
{
    public sealed record Request : IRequest<BaseResponse>
    {
        public required string CommandName { get; init; }
        public required ulong GuildId { get; init; }
        public required string Content { get; init; }
        public required bool IsEmbedded { get; init; }
        public required string? EmbedColor { get; init; }
        public required bool RestrictedUse { get; init; }
        public required IReadOnlyCollection<RoleDto> PermissionRoles { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Request, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            await this._grimoireDbContext.Roles.AddMissingRolesAsync(command.PermissionRoles, cancellationToken);

            var result = await this._grimoireDbContext.CustomCommands
                .Include(x => x.CustomCommandRoles)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Name == command.CommandName && x.GuildId == command.GuildId,
                    cancellationToken);
            var commandRoles = command.PermissionRoles.Select(x =>
                new CustomCommandRole
                {
                    CustomCommandName = command.CommandName, GuildId = command.GuildId, RoleId = x.Id
                }).ToList();
            if (result is null)
            {
                result = new CustomCommand
                {
                    Name = command.CommandName,
                    GuildId = command.GuildId,
                    Content = command.Content,
                    HasMention = command.Content.Contains("%mention", StringComparison.OrdinalIgnoreCase),
                    HasMessage = command.Content.Contains("%message", StringComparison.OrdinalIgnoreCase),
                    IsEmbedded = command.IsEmbedded,
                    EmbedColor = command.EmbedColor,
                    RestrictedUse = command.RestrictedUse,
                    CustomCommandRoles = commandRoles
                };
                await this._grimoireDbContext.AddAsync(result, cancellationToken);
            }
            else
            {
                result.Name = command.CommandName;
                result.GuildId = command.GuildId;
                result.Content = command.Content;
                result.HasMention = command.Content.Contains("%mention", StringComparison.OrdinalIgnoreCase);
                result.HasMessage = command.Content.Contains("%message", StringComparison.OrdinalIgnoreCase);
                result.IsEmbedded = command.IsEmbedded;
                result.EmbedColor = command.EmbedColor;
                result.RestrictedUse = command.RestrictedUse;
                result.CustomCommandRoles.Clear();
                foreach (var role in commandRoles)
                    result.CustomCommandRoles.Add(role);
            }

            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            var modChannelLog = await this._grimoireDbContext.Guilds
                .AsNoTracking()
                .WhereIdIs(command.GuildId)
                .Select(x => x.ModChannelLog)
                .FirstOrDefaultAsync(cancellationToken);
            return new BaseResponse
            {
                Message = $"Added {command.CommandName} custom command.", LogChannelId = modChannelLog
            };
        }
    }
}
