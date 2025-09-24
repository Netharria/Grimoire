// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;
using Grimoire.Settings.Enums;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed class ForgetSin
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    internal sealed class Command(IMediator mediator, GuildLog guildLog)
    {
        private readonly GuildLog _guildLog = guildLog;
        private readonly IMediator _mediator = mediator;

        [Command("Forget")]
        [Description("Forget a user's sin. This will permanently remove the sin from the bots memory.")]
        public async Task ForgetAsync(SlashCommandContext ctx,
            [MinMaxValue(0)] [Parameter("SinId")] [Description("The id of the sin to be forgotten.")]
            int sinId)
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var response = await this._mediator.Send(new Request { SinId = sinId, GuildId = ctx.Guild.Id });

            var message = $"**ID:** {response.SinId} **User:** {response.SinnerName}";

            await ctx.EditReplyAsync(GrimoireColor.Green, message, "Forgot");

            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Embed = new DiscordEmbedBuilder()
                    .WithAuthor($"{ctx.Guild.CurrentMember.Nickname} has been commanded to forget.")
                    .AddField("User", response.SinnerName, true)
                    .AddField("Sin Id", response.SinId.ToString(), true)
                    .AddField("Moderator", ctx.User.Mention, true)
                    .WithColor(GrimoireColor.Green)
            });
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public long SinId { get; init; }
        public GuildId GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Sins
                .Where(sin => sin.Id == command.SinId)
                .Where(sin => sin.GuildId == command.GuildId)
                .Select(sin => new
                {
                    Sin = sin,
                    UserName = sin.Member.User.UsernameHistories
                        .OrderByDescending(usernameHistory => usernameHistory.Timestamp)
                        .Select(usernameHistory => usernameHistory.Username)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result is null) throw new AnticipatedException("Could not find a sin with that ID.");


            dbContext.Sins.Remove(result.Sin);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response
            {
                SinId = command.SinId, SinnerName = result.UserName ?? UserExtensions.Mention(result.Sin.UserId)
            };
        }
    }

    public sealed record Response
    {
        public long SinId { get; init; }
        public string SinnerName { get; init; } = string.Empty;
    }
}
