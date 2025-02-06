// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed class ForgetSin
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequireUserGuildPermissions(DiscordPermissions.ManageMessages)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Forget", "Forget a user's sin. This will permanently remove the sin from the database.")]
        public async Task ForgetAsync(InteractionContext ctx,
            [Minimum(0)] [Option("SinId", "The sin id that will be forgotten.")]
            long sinId)
        {
            await ctx.DeferAsync();
            var response = await this._mediator.Send(new Request { SinId = sinId, GuildId = ctx.Guild.Id });

            var message = $"**ID:** {response.SinId} **User:** {response.SinnerName}";

            await ctx.EditReplyAsync(GrimoireColor.Green, message, "Forgot");

            if (response.LogChannelId is null) return;

            if (!ctx.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                    out var loggingChannel)) return;

            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithAuthor($"{ctx.Guild.CurrentMember.Nickname} has been commanded to forget.")
                .AddField("User", response.SinnerName, true)
                .AddField("Sin Id", response.SinId.ToString(), true)
                .AddField("Moderator", ctx.Member.Mention, true)
                .WithColor(GrimoireColor.Green));
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public long SinId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbContext.Sins
                .Where(x => x.Id == command.SinId)
                .Where(x => x.GuildId == command.GuildId)
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

            if (result is null) throw new AnticipatedException("Could not find a sin with that ID.");


            dbContext.Sins.Remove(result.Sin);
            await dbContext.SaveChangesAsync(cancellationToken);

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


