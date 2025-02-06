// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.SinAdmin.Commands;

internal sealed class UpdateSinReason
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequireUserGuildPermissions(DiscordPermissions.ManageMessages)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Reason", "Update the reason for a user's sin.")]
        public async Task ReasonAsync(InteractionContext ctx,
            [Minimum(0)] [Option("SinId", "The sin id that will have its reason updated.")]
            long sinId,
            [MaximumLength(1000)] [Option("Reason", "The reason the sin will be updated to.")]
            string reason)
        {
            await ctx.DeferAsync();
            var response = await this._mediator.Send(new Request
            {
                SinId = sinId, GuildId = ctx.Guild.Id, Reason = reason
            });

            var message = $"**ID:** {response.SinId} **User:** {response.SinnerName}";

            await ctx.EditReplyAsync(embed: new DiscordEmbedBuilder()
                .WithAuthor("Reason Updated")
                .AddField("Id", response.SinId.ToString(), true)
                .AddField("User", response.SinnerName, true)
                .AddField("Reason", reason)
                .WithTimestamp(DateTimeOffset.UtcNow)
                .WithColor(GrimoireColor.Green));

            if (response.LogChannelId is null) return;

            if (!ctx.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                    out var loggingChannel)) return;

            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator()} updated reason to {reason} for {message}")
                .WithColor(GrimoireColor.Green));
        }
    }
    public sealed record Request : IRequest<Response>
    {
        public long SinId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext)
        : IRequestHandler<Request, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<Response> Handle(Request command,
            CancellationToken cancellationToken)
        {
            var result = await this._grimoireDbContext.Sins
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

            result.Sin.Reason = command.Reason;

            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);

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

