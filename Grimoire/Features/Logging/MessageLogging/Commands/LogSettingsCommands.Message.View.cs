// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.




// ReSharper disable once CheckNamespace
namespace Grimoire.Features.Logging.Settings;

public partial class LogSettingsCommands
{
    public partial class Message
    {
        [Command("View")]
        [Description("View the current settings for the Message Log Module.")]
        public async Task ViewAsync(SlashCommandContext ctx)
        {
            await ctx.DeferResponseAsync(true);

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var response = await this._mediator.Send(new GetMessageLogSettings.Query { GuildId = ctx.Guild.Id });
            if (response is null)
            {
                await ctx.EditReplyAsync(GrimoireColor.Red, "Message Log settings could not be found for this server.");
                return;
            }

            var deleteChannelLog =
                response.DeleteChannelLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(response.DeleteChannelLog.Value)).Mention;
            var bulkDeleteChannelLog =
                response.BulkDeleteChannelLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(response.BulkDeleteChannelLog.Value)).Mention;
            var editChannelLog =
                response.EditChannelLog is null
                    ? "None"
                    : (await ctx.Guild.GetChannelAsync(response.EditChannelLog.Value)).Mention;
            await ctx.EditReplyAsync(
                title: "Current Logging System Settings",
                message: $"**Module Enabled:** {response.IsLoggingEnabled}\n" +
                         $"**Delete Log:** {deleteChannelLog}\n" +
                         $"**Bulk Delete Log:** {bulkDeleteChannelLog}\n" +
                         $"**Edit Log:** {editChannelLog}\n");
        }
    }
}

public sealed class GetMessageLogSettings
{
    public sealed record Query : IRequest<Response?>
    {
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Query request, CancellationToken cancellationToken)
        {
            await using var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.GuildMessageLogSettings
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => new Response
                {
                    EditChannelLog = x.EditChannelLogId,
                    DeleteChannelLog = x.DeleteChannelLogId,
                    BulkDeleteChannelLog = x.BulkDeleteChannelLogId,
                    IsLoggingEnabled = x.ModuleEnabled
                }).FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record Response
    {
        public ulong? EditChannelLog { get; init; }
        public ulong? DeleteChannelLog { get; init; }
        public ulong? BulkDeleteChannelLog { get; init; }
        public required bool IsLoggingEnabled { get; init; }
    }
}
