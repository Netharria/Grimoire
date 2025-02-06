// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.Mute.Commands;

public sealed class UnmuteUser
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequireUserGuildPermissions(DiscordPermissions.ManageMessages)]
    [SlashRequireBotPermissions(DiscordPermissions.ManageRoles)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Unmute", "Removes the mute on the user allowing them to speak.")]
        public async Task UnmuteUserAsync(
            InteractionContext ctx,
            [Option("User", "The User to unmute.")]
            DiscordUser user)
        {
            await ctx.DeferAsync();

            if (user is not DiscordMember member)
                throw new AnticipatedException("That user is not on the server.");
            if (ctx.Guild.Id == member.Id) throw new AnticipatedException("That user is not on the server.");
            var response = await this._mediator.Send(new Request { UserId = member.Id, GuildId = ctx.Guild.Id });
            var muteRole = ctx.Guild.Roles.GetValueOrDefault(response.MuteRole);
            if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
            await member.RevokeRoleAsync(muteRole, $"Unmuted by {ctx.Member.Mention}");

            var embed = new DiscordEmbedBuilder()
                .WithAuthor("Unmute")
                .AddField("User", user.Mention, true)
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
                await ctx.SendLogAsync(response, GrimoireColor.Red,
                    message: $"Was not able to send a direct message with the unmute details to {user.Mention}");
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
                .Select(x => new { Mute = x, x.Guild.ModerationSettings.MuteRole, x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);
            if (response is null) throw new AnticipatedException("That user doesn't seem to be muted.");
            if (response.MuteRole is null) throw new AnticipatedException("A mute role isn't currently configured.");
            dbContext.Mutes.Remove(response.Mute);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new Response { MuteRole = response.MuteRole.Value, LogChannelId = response.ModChannelLog };
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong MuteRole { get; init; }
    }
}
