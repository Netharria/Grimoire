// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using DSharpPlus.Commands.ContextChecks;
using Grimoire.DatabaseQueryHelpers;
using Grimoire.Features.Shared.Channels.GuildLog;

namespace Grimoire.Features.Moderation.Mute.Commands;

public sealed class UnmuteUser
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    [RequirePermissions([DiscordPermission.ManageRoles], [])]
    internal sealed class Command(IMediator mediator, GuildLog guildLog)
    {
        private readonly IMediator _mediator = mediator;
        private readonly GuildLog _guildLog = guildLog;

        [Command("Unmute")]
        [Description("Unmutes a user.")]
        public async Task UnmuteUserAsync(
            SlashCommandContext ctx,
            [Parameter("User")]
            [Description("The user to unmute.")]
            DiscordMember member)
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            if (ctx.Guild.Id != member.Guild.Id) throw new AnticipatedException("That user is not on the server.");
            var response = await this._mediator.Send(new Request { UserId = member.Id, GuildId = ctx.Guild.Id });
            var muteRole = ctx.Guild.Roles.GetValueOrDefault(response.MuteRole);
            if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
            await member.RevokeRoleAsync(muteRole, $"Unmuted by {ctx.User.Mention}");

            var embed = new DiscordEmbedBuilder()
                .WithAuthor("Unmute")
                .AddField("User", member.Mention, true)
                .AddField("Moderator", ctx.User.Mention, true)
                .WithColor(GrimoireColor.Green)
                .WithTimestamp(DateTimeOffset.UtcNow);


            await ctx.EditReplyAsync(embed: embed);

            try
            {
                await member.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithAuthor("Unmuted")
                    .WithDescription($"You have been unmuted by {ctx.User.Mention}")
                    .WithColor(GrimoireColor.Green));
            }
            catch (Exception)
            {
                await this._guildLog.SendLogMessageAsync(new GuildLogMessage
                {
                    GuildId = ctx.Guild.Id,
                    GuildLogType = GuildLogType.Moderation,
                    Color = GrimoireColor.Red,
                    Description =
                        $"Was not able to send a direct message with the unmute details to {member.Mention}"
                });
            }

            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
            {
                GuildId = ctx.Guild.Id,
                GuildLogType = GuildLogType.Moderation,
                Embed = embed
            });
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public ulong UserId { get; init; }
        public ulong GuildId { get; init; }
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var response = await dbContext.Mutes
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Select(x => new { Mute = x, x.Guild.ModerationSettings.MuteRole})
                .FirstOrDefaultAsync(cancellationToken);
            if (response is null) throw new AnticipatedException("That user doesn't seem to be muted.");
            if (response.MuteRole is null) throw new AnticipatedException("A mute role isn't currently configured.");
            dbContext.Mutes.Remove(response.Mute);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response { MuteRole = response.MuteRole.Value };
        }
    }

    public sealed record Response
    {
        public ulong MuteRole { get; init; }
    }
}
