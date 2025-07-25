// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


using System.ComponentModel;
using System.Text.RegularExpressions;
using DSharpPlus.Commands.ArgumentModifiers;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Channels;
using JetBrains.Annotations;

namespace Grimoire.Features.CustomCommands;

public sealed partial class CustomCommandSettings
{
    [GeneratedRegex(@"[0-9A-Fa-f]{6}\b", RegexOptions.None, 1000)]
    private static partial Regex ValidHexColor();

    //todo: fix this when variadic arguments are fixed
    [UsedImplicitly]
    [Command("Learn")]
    [Description("Learn a new command or update an existing one")]
    public async Task Learn(
        CommandContext ctx,
        [MinMaxLength(0, 24)]
        [Parameter("Name")]
        [Description("The name that the command will be called. This is used to activate the command.")]
        string name,
        [MinMaxLength(maxLength: 2000)]
        [Parameter("Content")]
        [Description("The content of the command. Use %mention or %message to add a message arguments")]
        string content,
        [Parameter("Embed")]
        [Description("Put the message in an embed")]
        bool embed = false,
        [MinMaxLength(maxLength: 6)]
        [Parameter("EmbedColor")]
        [Description("Hexadecimal color of the embed")]
        string? embedColor = null
        // ,
        // [Parameter("RestrictedUse")]
        // [Description("Only explicitly allowed roles can use this command")]
        // bool restrictedUse = false,
        // [Parameter("PermissionRoles")]
        // [Description("Deny roles the ability to use this command or allow roles if command is restricted use")]
        // [VariadicArgument(10)]
        // IReadOnlyList<DiscordRole>? allowedRoles = null
        )
    {
        IReadOnlyList<DiscordRole> allowedRoles;
        const bool restrictedUse = false;
        await ctx.DeferResponseAsync();

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

        if (ctx.Guild is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "This command can only be used in a server.");
            return;
        }

        allowedRoles = Array.Empty<DiscordRole>();

        var permissionRoles = allowedRoles.Select(role =>
            new RoleDto {Id = role.Id, GuildId = ctx.Guild.Id }).ToList();

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
        await this._channel.Writer.WriteAsync(
            new PublishToGuildLog
            {
                LogChannelId = response.LogChannelId,
                Description = $"{ctx.User.Mention} asked {ctx.Guild.CurrentMember.Mention} to learn a new command: {name}",
                Color = GrimoireColor.Purple
            });
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

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, BaseResponse>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<BaseResponse> Handle(Request command, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Roles.AddMissingRolesAsync(command.PermissionRoles, cancellationToken);

            var result = await dbContext.CustomCommands
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
                await dbContext.AddAsync(result, cancellationToken);
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

            await dbContext.SaveChangesAsync(cancellationToken);
            var modChannelLog = await dbContext.Guilds
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
