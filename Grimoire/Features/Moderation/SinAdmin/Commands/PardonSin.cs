// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed class PardonSin
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Pardon", "Pardon a user's sin. This leaves the sin in the logs but marks it as pardoned.")]
        public async Task PardonAsync(InteractionContext ctx,
            [Minimum(0)] [Option("SinId", "The sin id that is to be pardoned.")]
            long sinId,
            [MaximumLength(1000)] [Option("Reason", "The reason the sin is getting pardoned.")]
            string reason = "")
        {
            await ctx.DeferAsync();
            var response = await this._mediator.Send(new Request
            {
                SinId = sinId, GuildId = ctx.Guild.Id, ModeratorId = ctx.Member.Id, Reason = reason
            });

            var message = $"**ID:** {response.SinId} **User:** {response.SinnerName}";

            await ctx.EditReplyAsync(GrimoireColor.Green, message, "Pardoned");

            if (response.LogChannelId is null) return;

            if (!ctx.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                    out var loggingChannel)) return;

            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor("Pardon")
                .AddField("User", response.SinnerName, true)
                .AddField("Sin Id", response.SinId.ToString(), true)
                .AddField("Moderator", ctx.Member.Mention, true)
                .AddField("Reason", string.IsNullOrWhiteSpace(reason) ? "None" : reason, true)
                .WithColor(GrimoireColor.Green));
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
                .Where(x => x.Id == command.SinId)
                .Where(x => x.GuildId == command.GuildId)
                .Include(x => x.Pardon)
                .Select(x => new
                {
                    Sin = x,
                    UserName = x.Member.User.UsernameHistories
                        .OrderByDescending(x => x.Timestamp)
                        .Select(x => x.Username)
                        .FirstOrDefault(),
                    x.Guild.ModChannelLog
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
                LogChannelId = result.ModChannelLog
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public long SinId { get; init; }
        public string SinnerName { get; init; } = string.Empty;
    }
}
