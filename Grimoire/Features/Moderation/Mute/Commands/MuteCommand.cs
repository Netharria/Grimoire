// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.DatabaseQueryHelpers;

namespace Grimoire.Features.Moderation.Mute.Commands;

public sealed class MuteUser
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequireUserGuildPermissions(DiscordPermissions.ManageMessages)]
    [SlashRequireBotPermissions(DiscordPermissions.ManageRoles)]
    internal sealed class Command(IMediator mediator) : ApplicationCommandModule
    {
        private readonly IMediator _mediator = mediator;

        [SlashCommand("Mute", "Prevents the user from being able to speak.")]
        public async Task MuteUserAsync(
            InteractionContext ctx,
            [Option("User", "The User to mute.")] DiscordUser user,
            [Option("DurationType", "Select whether the duration will be in minutes hours or days")]
            DurationType durationType,
            [Minimum(0)] [Option("DurationAmount", "Select the amount of time the mute will last.")]
            long durationAmount,
            [MaximumLength(1000)] [Option("Reason", "The reason why the user is getting muted.")]
            string? reason = null
        )
        {
            await ctx.DeferAsync();

            var member = user as DiscordMember;

            if (member is null)
                throw new AnticipatedException("That user is not on the server.");

            if (ctx.Guild.Id == member.Guild.Id) throw new AnticipatedException("That user is not on the server.");
            var response = await this._mediator.Send(new Request
            {
                UserId = member.Id,
                GuildId = ctx.Guild.Id,
                DurationAmount = durationAmount,
                DurationType = durationType,
                ModeratorId = ctx.User.Id,
                Reason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason
            });
            var muteRole = ctx.Guild.Roles.GetValueOrDefault(response.MuteRole);
            if (muteRole is null) throw new AnticipatedException("Did not find the configured mute role.");
            await member.GrantRoleAsync(muteRole, reason!);

            var embed = new DiscordEmbedBuilder()
                .WithAuthor("Mute")
                .AddField("User", user.Mention, true)
                .AddField("Sin Id", $"**{response.SinId}**", true)
                .AddField("Moderator", ctx.User.Mention, true)
                .AddField("Length", $"{durationAmount} {durationType.GetName()}", true)
                .WithColor(GrimoireColor.Red)
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (!string.IsNullOrWhiteSpace(reason))
                embed.AddField("Reason", reason);

            await ctx.EditReplyAsync(embed: embed);

            try
            {
                await member.SendMessageAsync(new DiscordEmbedBuilder()
                    .WithAuthor($"Mute Id {response.SinId}")
                    .WithDescription(
                        $"You have been muted for {durationAmount} {durationType.GetName()} by {ctx.User.Mention} for {reason}")
                    .WithColor(GrimoireColor.Red));
            }
            catch (Exception)
            {
                await ctx.SendLogAsync(response, GrimoireColor.Red,
                    message: $"Was not able to send a direct message with the mute details to {user.Mention}");
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
        public DurationType DurationType { get; init; }
        public long DurationAmount { get; init; }
        public ulong ModeratorId { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public sealed class Handler(GrimoireDbContext grimoireDbContext)
        : IRequestHandler<Request, Response>
    {
        private readonly GrimoireDbContext _grimoireDbContext = grimoireDbContext;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var response = await this._grimoireDbContext.Members
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Select(x => new { x.ActiveMute, x.Guild.ModerationSettings.MuteRole, x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);
            if (response is null) throw new AnticipatedException("Could not find User.");
            if (response.MuteRole is null) throw new AnticipatedException("A mute role is not configured.");
            if (response.ActiveMute is not null) this._grimoireDbContext.Mutes.Remove(response.ActiveMute);
            var lockEndTime = command.DurationType.GetDateTimeOffset(command.DurationAmount);
            var sin = new Sin
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                ModeratorId = command.ModeratorId,
                Reason = command.Reason,
                SinType = SinType.Mute,
                Mute = new Domain.Mute { GuildId = command.GuildId, UserId = command.UserId, EndTime = lockEndTime }
            };
            await this._grimoireDbContext.Sins.AddAsync(sin, cancellationToken);
            await this._grimoireDbContext.SaveChangesAsync(cancellationToken);
            return new Response
            {
                MuteRole = response.MuteRole.Value, LogChannelId = response.ModChannelLog, SinId = sin.Id
            };
        }
    }

    public sealed record Response : BaseResponse
    {
        public ulong MuteRole { get; init; }
        public long SinId { get; init; }
    }
}
