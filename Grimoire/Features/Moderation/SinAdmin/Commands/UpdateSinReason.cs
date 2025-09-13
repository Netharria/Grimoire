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

internal sealed class UpdateSinReason
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    internal sealed class Command(IMediator mediator, GuildLog guildLog)
    {
        private readonly IMediator _mediator = mediator;
        private readonly GuildLog _guildLog = guildLog;

        [Command("Reason")]
        [Description("Update the reason for a user's sin.")]
        public async Task ReasonAsync(SlashCommandContext ctx,
            [MinMaxValue(0)]
            [Parameter("SinId")]
            [Description("The id of the sin to be updated.")]
            int sinId,
            [MinMaxLength(maxLength: 1000)]
            [Parameter("Reason")]
            [Description("The reason the sin will be updated to.")]
            string reason)
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

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

            await this._guildLog.SendLogMessageAsync(new GuildLogMessage
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Color = GrimoireColor.Green,
                Description = $"{ctx.User.Mention} updated reason to {reason} for {message}"
            });
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public long SinId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command,
            CancellationToken cancellationToken)
        {
            var dbcontext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var result = await dbcontext.Sins
                .Where(sin => sin.Id == command.SinId)
                .Where(sin => sin.GuildId == command.GuildId)
                .Select(sin => new
                {
                    Sin = sin,
                    UserName = sin.Member.User.UsernameHistories
                        .OrderByDescending(usernameHistory => usernameHistory.Timestamp)
                        .Select(usernameHistory => usernameHistory.Username)
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (result is null) throw new AnticipatedException("Could not find a sin with that ID.");

            result.Sin.Reason = command.Reason;

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
