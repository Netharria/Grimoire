// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.DatabaseQueryHelpers;
using Grimoire.Discord.Extensions;

namespace Grimoire.Core.Features.MessageLogging.Commands;

public sealed record AddMessageCommand : ICommand
{
    public ulong MessageId { get; init; }
    public ulong ChannelId { get; init; }
    public string MessageContent { get; init; } = string.Empty;
    public ulong UserId { get; init; }
    public AttachmentDto[] Attachments { get; init; } = [];
    public ulong? ReferencedMessageId { get; init; }
    public ulong GuildId { get; init; }
}

public sealed class AddMessageCommandHandler(IGrimoireDbContext grimoireDbContext) : ICommandHandler<AddMessageCommand>
{
    private readonly IGrimoireDbContext _grimoireDbContext = grimoireDbContext;

    public async ValueTask<Unit> Handle(AddMessageCommand command, CancellationToken cancellationToken)
    {
        var result = await this._grimoireDbContext.Guilds
            .AsNoTracking()
            .WhereIdIs(command.GuildId)
            .Select(guild => new
            {
                guild.MessageLogSettings.ModuleEnabled,
                ChannelExists = guild.Channels.Any(x => x.Id == command.ChannelId),
                MemberExists = guild.Members.Any(x => x.UserId == command.UserId)
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
            if (!await this._grimoireDbContext.Users.AnyAsync(x => x.Id == command.UserId, cancellationToken))
                await this._grimoireDbContext.Users.AddAsync(new User { Id = command.UserId }, cancellationToken);
            await this._grimoireDbContext.Members.AddAsync(new Member
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                XpHistory = new List<XpHistory>
                    {
                        new() {
                            UserId = command.UserId,
                            GuildId = command.GuildId,
                            Xp = 0,
                            Type = XpHistoryType.Created,
                            TimeOut = DateTime.UtcNow
                        }
                    },
            }, cancellationToken);
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
                new() {
                    MessageId = command.MessageId,
                    MessageContent = command.MessageContent.UnicodeToUTF8(),
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
