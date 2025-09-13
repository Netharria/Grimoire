// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ArgumentModifiers;
using DSharpPlus.Commands.ContextChecks;
using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed class PardonSin
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    internal sealed class Command(IMediator mediator, GuildLog guildLog)
    {
        private readonly IMediator _mediator = mediator;
        private readonly GuildLog _guildLog = guildLog;

        [Command("Pardon")]
        [Description("Pardon a user's sin. This leaves the sin in the logs but marks it as pardoned.")]
        public async Task PardonAsync(SlashCommandContext ctx,
            [MinMaxValue(0)]
            [Parameter("SinId")]
            [Description("The id of the sin to be pardoned.")]
            int sinId,
            [MinMaxLength(maxLength: 1000)]
            [Parameter("Reason")]
            [Description("The reason the sin is getting pardoned.")]
            string reason = "")
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            var response = await this._mediator.Send(new Request
            {
                SinId = sinId, GuildId = ctx.Guild.Id, ModeratorId = ctx.User.Id, Reason = reason
            });

            var message = $"**ID:** {response.SinId} **User:** {response.SinnerName}";

            await ctx.EditReplyAsync(GrimoireColor.Green, message, "Pardoned");

            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Embed = new DiscordEmbedBuilder()
                    .WithAuthor("Pardon")
                    .AddField("User", response.SinnerName, true)
                    .AddField("Sin Id", response.SinId.ToString(), true)
                    .AddField("Moderator", ctx.User.Mention, true)
                    .AddField("Reason", string.IsNullOrWhiteSpace(reason) ? "None" : reason, true)
                    .WithColor(GrimoireColor.Green)
            });
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public long SinId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public ulong ModeratorId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var dbcontext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbcontext.Sins
                .Where(sin => sin.Id == command.SinId)
                .Where(sin => sin.GuildId == command.GuildId)
                .Include(sin => sin.Pardon)
                .Select(sin => new
                {
                    Sin = sin,
                    UserName = sin.Member.User.UsernameHistories
                        .OrderByDescending(usernameHistory => usernameHistory.Timestamp)
                        .Select(usernameHistory => usernameHistory.Username)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result is null)
                throw new AnticipatedException("Could not find a sin with that ID.");

            if (result.Sin.Pardon is not null)
                result.Sin.Pardon.Reason = command.Reason;
            else
                result.Sin.Pardon = new Pardon
                {
                    SinId = command.SinId,
                    GuildId = command.GuildId,
                    ModeratorId = command.ModeratorId,
                    Reason = command.Reason
                };
            await dbcontext.SaveChangesAsync(cancellationToken);

            return new Response
            {
                SinId = command.SinId,
                SinnerName = result.UserName ?? UserExtensions.Mention(result.Sin.UserId),
            };
        }
    }

    public sealed record Response
    {
        public long SinId { get; init; }
        public string SinnerName { get; init; } = string.Empty;
    }
}
