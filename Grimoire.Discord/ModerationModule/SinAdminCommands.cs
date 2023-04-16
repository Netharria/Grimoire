// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core.Features.Moderation.Commands.SinAdminCommands.PardonSin;
using Grimoire.Core.Features.Moderation.Commands.SinAdminCommands.UpdateSinReason;
using Grimoire.Core.Features.Moderation.Commands.SinAdminCommands.ForgetSin;

namespace Grimoire.Discord.ModerationModule
{
    [SlashRequireGuild]
    [SlashRequireModuleEnabled(Module.Moderation)]
    [SlashRequirePermissions(Permissions.ManageMessages)]
    public class SinAdminCommands : ApplicationCommandModule
    {
        private readonly IMediator _mediator;

        public SinAdminCommands(IMediator mediator)
        {
            this._mediator = mediator;
        }

        [SlashCommand("Pardon", "Pardon a user's sin. This leaves the sin in the logs but marks it as pardoned.")]
        public async Task PardonAsync(InteractionContext ctx,
            [Minimum(0)]
            [Option("SinId", "The sin id that is to be pardoned.")] long sinId,
            [MaximumLength(1000)]
            [Option("Reason", "The reason the sin is getting pardoned.")] string reason = "")
        {
            var response = await this._mediator.Send(new PardonSinCommand
            {
                SinId = sinId,
                GuildId = ctx.Guild.Id,
                ModeratorId = ctx.Member.Id,
                Reason = reason
            });

            var message = $"**ID:** {response.SinId} **User:** {response.SinnerName}";

            await ctx.ReplyAsync(GrimoireColor.Green, message, "Pardoned");

            if (response.LogChannelId is null) return;

            if (!ctx.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                out var loggingChannel)) return;

            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator()} pardoned {message} for {reason}")
                .WithColor(GrimoireColor.Green));
        }

        [SlashCommand("Reason", "Update the reason for a user's sin.")]
        public async Task ReasonAsync(InteractionContext ctx,
            [Minimum(0)]
            [Option("SinId", "The sin id that will have its reason updated.")] long sinId,
            [MaximumLength(1000)]
            [Option("Reason", "The reason the sin will be updated to.")] string reason = "")
        {
            var response = await this._mediator.Send(new UpdateSinReasonCommand
            {
                SinId = sinId,
                GuildId = ctx.Guild.Id,
                Reason = reason
            });

            var message = $"**ID:** {response.SinId} **User:** {response.SinnerName}";

            await ctx.ReplyAsync(GrimoireColor.Green, $"Updated reason to {reason} for {message}");

            if (response.LogChannelId is null) return;

            if (!ctx.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                out var loggingChannel)) return;

            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator()} updated reason to {reason} for {message}")
                .WithColor(GrimoireColor.Green));
        }

        [SlashCommand("Forget", "Forget a user's sin. This will permanantly remove the sin from the database.")]
        public async Task ForgetAsync(InteractionContext ctx,
            [Minimum(0)]
            [Option("SinId", "The sin id that will be forgotten.")] long sinId)
        {
            var response = await this._mediator.Send(new ForgetSinCommand
            {
                SinId = sinId,
                GuildId = ctx.Guild.Id
            });

            var message = $"**ID:** {response.SinId} **User:** {response.SinnerName}";

            await ctx.ReplyAsync(GrimoireColor.Green, message, "Forgot");

            if (response.LogChannelId is null) return;

            if (!ctx.Guild.Channels.TryGetValue(response.LogChannelId.Value,
                out var loggingChannel)) return;

            await loggingChannel.SendMessageAsync(new DiscordEmbedBuilder()
                .WithDescription($"{ctx.Member.GetUsernameWithDiscriminator()} ordered {ctx.Guild.CurrentMember.Nickname} to forget {message}")
                .WithColor(GrimoireColor.Green));
        }
    }
}
