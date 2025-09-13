// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class ModuleCommands
{
    [UsedImplicitly]
    [Command("View")]
    [Description("View the current states of the modules.")]
    public async Task ViewAsync(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync(true);

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response = await this._mediator.Send(new GetAllModuleStatesForGuild.Query { GuildId = ctx.Guild.Id });
        if (response is null)
        {
            await ctx.EditReplyAsync(GrimoireColor.Red, "Settings could not be found for this server.");
            return;
        }

        await ctx.EditReplyAsync(
            title: "Current states of modules.",
            message: $"**Leveling Enabled:** {response.LevelingIsEnabled}\n" +
                     $"**User Log Enabled:** {response.UserLogIsEnabled}\n" +
                     $"**Message Log Enabled:** {response.MessageLogIsEnabled}\n" +
                     $"**Moderation Enabled:** {response.ModerationIsEnabled}\n" +
                     $"**Commands Enabled:** {response.CommandsIsEnabled}\n");
    }
}

internal sealed class GetAllModuleStatesForGuild
{
    public sealed record Query : IRequest<Response?>
    {
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, Response?>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response?> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Guilds
                .AsNoTracking()
                .WhereIdIs(request.GuildId)
                .Select(x => new Response
                {
                    // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                    LevelingIsEnabled = x.LevelSettings != null && x.LevelSettings.ModuleEnabled,
                    UserLogIsEnabled = x.UserLogSettings != null && x.UserLogSettings.ModuleEnabled,
                    ModerationIsEnabled = x.ModerationSettings != null && x.ModerationSettings.ModuleEnabled,
                    MessageLogIsEnabled = x.MessageLogSettings != null && x.MessageLogSettings.ModuleEnabled,
                    CommandsIsEnabled = x.CommandsSettings != null && x.CommandsSettings.ModuleEnabled
                    // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                }).FirstOrDefaultAsync(cancellationToken);
        }
    }

    public sealed record Response
    {
        public bool LevelingIsEnabled { get; init; }
        public bool UserLogIsEnabled { get; init; }
        public bool ModerationIsEnabled { get; init; }
        public bool MessageLogIsEnabled { get; init; }
        public bool CommandsIsEnabled { get; init; }
    }
}
