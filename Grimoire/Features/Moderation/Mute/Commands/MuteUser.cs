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

namespace Grimoire.Features.Moderation.Mute.Commands;

public sealed class MuteUser
{
    [RequireGuild]
    [RequireModuleEnabled(Module.Moderation)]
    [RequireUserGuildPermissions(DiscordPermission.ManageMessages)]
    [RequirePermissions([DiscordPermission.ManageRoles], [])]
    internal sealed class Command(IMediator mediator, GuildLog guildLog)
    {
        private readonly GuildLog _guildLog = guildLog;
        private readonly IMediator _mediator = mediator;

        [Command("Mute")]
        [Description("Mutes a user for a specified amount of time.")]
        public async Task MuteUserAsync(
            SlashCommandContext ctx,
            [Parameter("User")] [Description("The user to mute.")]
            DiscordMember member,
            [Parameter("DurationType")] [Description("Select whether the duration will be in minutes hours or days")]
            DurationType durationType,
            [MinMaxValue(0)] [Parameter("DurationAmount")] [Description("The amount of time the mute will last.")]
            int durationAmount,
            [MinMaxLength(maxLength: 1000)] [Parameter("Reason")] [Description("The reason for the mute.")]
            string? reason = null
        )
        {
            await ctx.DeferResponseAsync();

            if (ctx.Guild is null)
                throw new AnticipatedException("This command can only be used in a server.");

            if (ctx.Guild.Id != member.Guild.Id) throw new AnticipatedException("That user is not on the server.");
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
                .AddField("User", member.Mention, true)
                .AddField("Sin Id", $"**{response.SinId}**", true)
                .AddField("Moderator", ctx.User.Mention, true)
                .AddField("Length", $"{durationAmount} {durationType}", true)
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
                        $"You have been muted for {durationAmount} {durationType} by {ctx.User.Mention} for {reason}")
                    .WithColor(GrimoireColor.Red));
            }
            catch (Exception)
            {
                await this._guildLog.SendLogMessageAsync(new GuildLogMessage
                {
                    GuildId = ctx.Guild.Id,
                    GuildLogType = GuildLogType.Moderation,
                    Description =
                        $"Was not able to send a direct message with the mute details to {member.Mention}.",
                    Color = GrimoireColor.Red
                });
            }

            await this._guildLog.SendLogMessageAsync(new GuildLogMessageCustomEmbed
            {
                GuildId = ctx.Guild.Id, GuildLogType = GuildLogType.Moderation, Embed = embed
            });
        }
    }

    public sealed record Request : IRequest<Response>
    {
        public ulong UserId { get; init; }
        public GuildId GuildId { get; init; }
        public DurationType DurationType { get; init; }
        public long DurationAmount { get; init; }
        public ulong ModeratorId { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public sealed class Handler(IDbContextFactory<GrimoireDbContext> dbContextFactory)
        : IRequestHandler<Request, Response>
    {
        private readonly IDbContextFactory<GrimoireDbContext> _dbContextFactory = dbContextFactory;

        public async Task<Response> Handle(Request command, CancellationToken cancellationToken)
        {
            var dbContext = await this._dbContextFactory.CreateDbContextAsync(cancellationToken);
            var response = await dbContext.Members
                .WhereMemberHasId(command.UserId, command.GuildId)
                .Select(x => new { x.ActiveMute, x.Guild.ModerationSettings.MuteRole, x.Guild.ModChannelLog })
                .FirstOrDefaultAsync(cancellationToken);
            if (response is null) throw new AnticipatedException("Could not find User.");
            if (response.MuteRole is null) throw new AnticipatedException("A mute role is not configured.");
            if (response.ActiveMute is not null) dbContext.Mutes.Remove(response.ActiveMute);
            var lockEndTime = command.DurationType.GetDateTimeOffset(command.DurationAmount);
            var sin = new Sin
            {
                UserId = command.UserId,
                GuildId = command.GuildId,
                ModeratorId = command.ModeratorId,
                Reason = command.Reason,
                SinType = SinType.Mute,
                Mute = new Domain.Obsolete.Mute { GuildId = command.GuildId, UserId = command.UserId, EndTime = lockEndTime }
            };
            await dbContext.Sins.AddAsync(sin, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new Response { MuteRole = response.MuteRole.Value, SinId = sin.Id };
        }
    }

    public sealed record Response
    {
        public ulong MuteRole { get; init; }
        public long SinId { get; init; }
    }
}
