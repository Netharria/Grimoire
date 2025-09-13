// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.Mute.Commands;

public partial class MuteAdminCommands
{
    [Command("Refresh")]
    [Description("Refreshes the permissions of the currently configured mute role.")]
    public async Task RefreshMuteRoleAsync(SlashCommandContext ctx)
    {
        await ctx.DeferResponseAsync();

        if (ctx.Guild is null)
            throw new AnticipatedException("This command can only be used in a server.");

        var response = await this._mediator.Send(new GetMuteRole.Query { GuildId = ctx.Guild.Id });

        if (!ctx.Guild.Roles.TryGetValue(response, out var role))
        {
            await ctx.EditReplyAsync(GrimoireColor.Yellow, "Could not find configured mute role.");
            return;
        }

        await ctx.EditReplyAsync(GrimoireColor.DarkPurple, $"Refreshing permissions for {role.Mention} role.");
        var result = await SetMuteRolePermissionsAsync(ctx.Guild, role)
            .Where(x => !x.WasSuccessful)
            .ToArrayAsync();

        if (result.Length == 0)
            await ctx.EditReplyAsync(GrimoireColor.DarkPurple,
                $"Succussfully refreshed permissions for {role.Mention} role.");
        else
            await ctx.EditReplyAsync(GrimoireColor.Yellow,
                $"Was not able to set permissions for the following channels. " +
                $"{string.Join(' ', result.Select(x => x.Channel.Mention))}");
    }
}

public class GetMuteRole
{
    public sealed record Query : IRequest<ulong>
    {
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Query, ulong>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<ulong> Handle(Query request, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var muteRoleId = await dbContext.GuildModerationSettings
                .AsNoTracking()
                .Where(x => x.GuildId == request.GuildId)
                .Select(x => x.MuteRole)
                .FirstOrDefaultAsync(cancellationToken);
            if (muteRoleId is null) throw new AnticipatedException("No mute role is configured.");
            return muteRoleId.Value;
        }
    }
}
