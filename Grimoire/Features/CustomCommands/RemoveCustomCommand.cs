// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.


// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.CustomCommands;
public sealed class RemoveCustomCommand
{
    [SlashCommandGroup("Commands", "Manage custom commands.")]
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Commands)]
    [SlashRequireUserGuildPermissions(DiscordPermissions.ManageGuild)]
    internal sealed partial class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Forget", "Forget a command")]
        internal async Task Forget(
            InteractionContext ctx,
            [Autocomplete(typeof(GetCustomCommandOptions.AutocomepleteProvider))]
        [Option("Name", "The name that the command is called.", true)] string name)
        {
            await ctx.DeferAsync();

            var response = await this._mediator.Send(new Request
            {
                CommandName = name,
                GuildId = ctx.Guild.Id,
            });

            await ctx.EditReplyAsync(GrimoireColor.Green, response.Message);
            await ctx.SendLogAsync(response, GrimoireColor.DarkPurple);
        }
    }

    public sealed record Request : IRequest<BaseResponse>
    {
        public required string CommandName { get; init; }
        public required ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext) : IRequestHandler<Request, BaseResponse>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async ValueTask<BaseResponse> Handle(Request request, CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.CustomCommands
                .Include(x => x.CustomCommandRoles)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Name == request.CommandName && x.GuildId == request.GuildId, cancellationToken);
            if (result is null)
                throw new AnticipatedException($"Did not find a saved command with name {request.CommandName}");

            this._grimoireDbContext.CustomCommands.Remove(result);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            var modChannelLog = await this._grimoireDbContext.Guilds
                    .AsNoTracking()
                    .WhereIdIs(request.GuildId)
                    .Select(x => x.ModChannelLog)
                    .FirstOrDefaultAsync(cancellationToken);
            return new BaseResponse
            {
                Message = $"Removed command {request.CommandName}",
                LogChannelId = modChannelLog
            };
        }
    }
}
