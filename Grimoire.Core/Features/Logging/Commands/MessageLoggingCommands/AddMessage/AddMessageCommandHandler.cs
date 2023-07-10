// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;

namespace Grimoire.Core.Features.Logging.Commands.MessageLoggingCommands.AddMessage;

public class AddMessageCommandHandler : ICommandHandler<AddMessageCommand>
{
    private readonly IGrimoireDbContext _grimoireDbContext;

    public AddMessageCommandHandler(IGrimoireDbContext grimoireDbContext)
    {
        this._grimoireDbContext = grimoireDbContext;
    }

    public async ValueTask<Unit> Handle(AddMessageCommand command, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Guilds
            .AsNoTracking()
            .WhereIdIs(command.GuildId)
            .Select(guild => new
            {
                guild.MessageLogSettings.ModuleEnabled,
                ChannelExists = guild.Channels.Any(x => x.Id == command.ChannelId)
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
            MessageHistory = new List<MessageHistory>{
                new MessageHistory
                {
                    MessageId = command.MessageId,
                    MessageContent = command.MessageContent
                        .Replace("\x00", ""),
                    GuildId = command.GuildId,
                    Action = MessageAction.Created
                }
            }
        };
        await this._grimoireDbContext.Messages.AddAsync(message, cancellationToken);
        await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
