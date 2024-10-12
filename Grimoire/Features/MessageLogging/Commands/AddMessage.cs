// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using Microsoft.Extensions.Logging;

namespace Grimoire.Features.MessageLogging.Commands;

public sealed partial class AddMessage
{
    public sealed record Command : ICommand
    {
        public required ulong MessageId { get; init; }
        public required ulong ChannelId { get; init; }
        public required string MessageContent { get; init; }
        public required ulong UserId { get; init; }
        public required AttachmentDto[] Attachments { get; init; }
        public ulong? ReferencedMessageId { get; init; }
        public required ulong GuildId { get; init; }
        public required List<ulong> ParentChannelTree { get; init; }
    }

    public sealed partial class Handler(GrimoireDbContext grimoireDbContext, ILogger<Handler> logger) : ICommandHandler<Command>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;
        private readonly ILogger<Handler> _logger = logger;

        public async ValueTask<Unit> Handle(Command command, CancellationToken cancellationToken)
        {
            try
            {
                var result = await this._grimoireDbContext.Guilds
            .AsNoTracking()
            .WhereIdIs(command.GuildId)
            .Select(guild => new
            {
                guild.MessageLogSettings.ModuleEnabled,
                ChannelExists = guild.Channels.Any(x => x.Id == command.ChannelId),
                MemberExists = guild.Members.Any(x => x.UserId == command.UserId),
                ChannelLogOverride = guild.MessageLogChannelOverrides
                    .Where(x => command.ParentChannelTree.Contains(x.ChannelId))
                    .Select(x => new { x.ChannelId, x.ChannelOption })
                    .ToArray()
            }).FirstOrDefaultAsync(cancellationToken);
                if (result is null)
                    throw new KeyNotFoundException("Guild was not found in database.");
                if (!result.ModuleEnabled)
                    return Unit.Value;
                if (!result.ChannelExists)
                {
                    var channel = new Channel
                    {
                        GuildId = command.GuildId,
                        Id = command.ChannelId
                    };
                    await this._grimoireDbContext.Channels.AddAsync(channel, cancellationToken);
                }
                if (!result.MemberExists)
                {
                    await this.AddMissingMember(command, cancellationToken);
                }

                if (!ShouldLogMessage(command, result.ChannelLogOverride.ToDictionary(k => k.ChannelId, v => v.ChannelOption)))
                    return Unit.Value;

                var message = new Message
                {
                    Id = command.MessageId,
                    UserId = command.UserId,
                    Attachments = command.Attachments
                .Select(x =>
                    new Attachment
                    {
                        Id = x.Id,
                        MessageId = command.MessageId,
                        FileName = x.FileName,
                    })
                .ToArray(),
                    ChannelId = command.ChannelId,
                    ReferencedMessageId = command.ReferencedMessageId,
                    GuildId = command.GuildId,
                    MessageHistory = [
                new() {
                    MessageId = command.MessageId,
                    MessageContent = command.MessageContent,
                    GuildId = command.GuildId,
                    Action = MessageAction.Created
                }
            ]
                };
                await this._grimoireDbContext.Messages.AddAsync(message, cancellationToken);
                await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
                return Unit.Value;
            }
            catch (DbUpdateException)
            {
                LogOriginalMessageForDebugging(_logger, command.MessageContent);
                throw;
            }
        }

        [LoggerMessage(LogLevel.Error, "Database through exception on message creation. This was the original message {message}")]
        private static partial void LogOriginalMessageForDebugging(ILogger logger, string message);

        private async Task AddMissingMember(Command command, CancellationToken cancellationToken)
        {
            if (!await this._grimoireDbContext.Users.AnyAsync(x => x.Id == command.UserId, cancellationToken))
                await this._grimoireDbContext.Users.AddAsync(new User { Id = command.UserId }, cancellationToken);
            await this._grimoireDbContext.Members.AddAsync(new Member
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                XpHistory =
                    [
                        new() {
                            UserId = command.UserId,
                            GuildId = command.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTime.UtcNow
                        }
                    ],
            }, cancellationToken);
        }

        private static bool ShouldLogMessage(Command command, Dictionary<ulong, MessageLogOverrideOption> overrides)
        {
            if (overrides.Count == 0)
                return true;
            foreach (var channel in command.ParentChannelTree)
            {
                if (!overrides.TryGetValue(channel, out var channelOverride))
                    continue;
                return channelOverride switch
                {
                    MessageLogOverrideOption.NeverLog => false,
                    MessageLogOverrideOption.AlwaysLog => true,
                    _ => true
                };
            }
            return true;
        }
    }
}




