// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.



using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.CustomCommands.Commands;
public sealed class AddCustomCommand
{
    public sealed record Command : ICommand<BaseResponse>
    {
        public required string CommandName { get; init; }
        public required ulong GuildId { get; init; }
        public required string Content { get; init; }
        public required bool IsEmbedded { get; init; }
        public required string? EmbedColor { get; init; }
        public required bool RestrictedUse { get; init; }
        public required RoleDto[] PermissionRoles { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : ICommandHandler<Command, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Command command, CancellationToken cancellationToken)
        {
            await this._grimoireDbContext.Roles.AddMissingRolesAsync(command.PermissionRoles, cancellationToken);

            var result = await this._grimoireDbContext.CustomCommands
                .Include(x => x.CustomCommandRoles)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Name == command.CommandName && x.GuildId == command.GuildId, cancellationToken);
            var commandRoles = command.PermissionRoles.Select(x =>
                new CustomCommandRole
                {
                    CustomCommandName = command.CommandName,
                    GuildId = command.GuildId,
                    RoleId = x.Id,
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
                Message = $"Added {command.CommandName} custom command.",
                LogChannelId = modChannelLog
            };
        }
    }
}
