// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Exceptions;
using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.Warn;

internal sealed class Warn
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequireUserPermissions(DiscordPermissions.ManageMessages)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Warn", "Issue a warning to the user.")]
        public async Task WarnAsync(InteractionContext ctx,
            [Option("User", "The user to warn.")] DiscordUser user,
            [MaximumLength(1000)] [Option("Reason", "The reason for the warn.")]
            string reason)
        {
            await ctx.DeferAsync();
            var member = user as DiscordMember;
            if (member is null)
                throw new AnticipatedException("The user supplied is not part of this server.");
            if (ctx.User == user)
                throw new AnticipatedException("You cannot warn yourself.");
            var response = await this._mediator.Send(new Request
            {
                UserId = user.Id, GuildId = ctx.Guild.Id, ModeratorId = ctx.User.Id, Reason = reason
            });
            var embed = new DiscordEmbedBuilder()
                .WithAuthor("Warn")
                .AddField("User", user.Mention, true)
                .AddField("Sin Id", $"**{response.SinId}**", true)
                .AddField("Moderator", ctx.User.Mention, true)
                .AddField("Reason", reason)
                .WithColor(GrimoireColor.Yellow)
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ctx.EditReplyAsync(embed: embed);

            try
            {
                await member.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithAuthor($"Warning Id {response.SinId}")
                    .WithDescription($"You have been warned by {ctx.User.Mention} for {reason}")
                    .WithColor(GrimoireColor.Yellow));
            }
            catch (Exception ex) when (ex is BadRequestException or UnauthorizedException)
            {
                await ctx.SendLogAsync(response, GrimoireColor.Red,
                    message: $"Was not able to send a direct message with the warn details to {user.Mention}");
            }

            if (response.LogChannelId is null) return;

            var logChannel = ctx.Guild.Channels.GetValueOrDefault(response.LogChannelId.Value);

            if (logChannel is null) return;

            await logChannel.SendMessageAsync(embed);
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
        public string Reason { get; init; } = string.Empty;
        public ulong ModeratorId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var dbcontext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var sin = new Sin
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                ModeratorId = command.ModeratorId,
                Reason = command.Reason,
                SinType = SinType.Warn
            };
            await dbcontext.Sins
                .AddAsync(sin, cancellationToken);
            await dbcontext.SaveChangesAsync(cancellationToken);
            var logChannelId = await dbcontext.Guilds
                .WhereIdIs(command.GuildId)
                .Select(x => x.ModChannelLog).FirstOrDefaultAsync(cancellationToken);
            return new Response { SinId = sin.Id, LogChannelId = logChannelId };
        }
    }

    public sealed record Response : BaseResponse
    {
        public long SinId { get; init; }
    }
}
