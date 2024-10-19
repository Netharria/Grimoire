// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Logging.Settings;
public partial class LogSettingsCommands
{
    public partial class Message
    {
        [SlashCommand("View", "View the current settings for the Message Log Module.")]
        public async Task ViewAsync(InteractionContext ctx)
        {
            await ctx.DeferAsync(true);
            var response = await this._mediator.Send(new GetMessageLogSettings.Query{ GuildId = ctx.Guild.Id });
            var DeleteChannelLog =
                response.DeleteChannelLog is null ?
                "None" :
                (await ctx.Guild.GetChannelAsync(response.DeleteChannelLog.Value)).Mention;
            var BulkDeleteChannelLog =
                response.BulkDeleteChannelLog is null ?
                "None" :
                (await ctx.Guild.GetChannelAsync(response.BulkDeleteChannelLog.Value)).Mention;
            var EditChannelLog =
                response.EditChannelLog is null ?
                "None" :
                (await ctx.Guild.GetChannelAsync(response.EditChannelLog.Value)).Mention;
            await ctx.EditReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.IsLoggingEnabled}\n" +
                $"**Delete Log:** {DeleteChannelLog}\n" +
                $"**Bulk Delete Log:** {BulkDeleteChannelLog}\n" +
                $"**Edit Log:** {EditChannelLog}\n");
        }
    }
}

public sealed class GetMessageLogSettings
{
    public sealed record Query : IRequest<Response>
    {
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Query, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<Response> Handle(Query request, CancellationToken cancellationToken)
        {
            return await this._grimoireDbContext.GuildMessageLogSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new Response
                {
                    EditChannelLog = x.EditChannelLogId,
                    DeleteChannelLog = x.DeleteChannelLogId,
                    BulkDeleteChannelLog = x.BulkDeleteChannelLogId,
                    IsLoggingEnabled = x.ModuleEnabled
                }).FirstAsync(cancellationToken: cancellationToken);
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong? EditChannelLog { get; init; }
        public ulong? DeleteChannelLog { get; init; }
        public ulong? BulkDeleteChannelLog { get; init; }
        public bool IsLoggingEnabled { get; init; }
    }
}

