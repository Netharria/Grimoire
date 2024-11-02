// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

namespace Grimoire.Features.Moderation.SinAdmin;

[SlashRequireGuild]
[SlashRequireModuleEnabled(Module.Moderation)]
internal sealed class SinLogCommands(IMediator mediator) : ApplicationCommandModule
{
    private readonly IMediator _mediator = mediator;

    [SlashCommand("SinLog", "Looks up the sin logs for the provided user.")]
    public async Task SinLogAsync(
        InteractionContext ctx,
        [Option("Type", "The Type of logs to lookup.")]
        SinQueryType sinQueryType,
        [Option("User", "The user to look up the logs for. Leave blank for self.")]
        DiscordUser? user = null)
    {
        await ctx.DeferAsync(!ctx.Member.Permissions.HasPermission(DiscordPermissions.ManageMessages));
        user ??= ctx.User;


        if (!ctx.Member.Permissions.HasPermission(DiscordPermissions.ManageMessages) && ctx.User != user)
            throw new AnticipatedException("Only moderators can look up logs for someone else.");
        if (sinQueryType == SinQueryType.Mod)
        {
            var modResponse = await this._mediator.Send(new GetModActionCountsQuery
            {
                UserId = user.Id, GuildId = ctx.Guild.Id
            });
            if (modResponse is null)
            {
                await ctx.EditReplyAsync(GrimoireColor.Red, "Did not find a moderator with that id.");
                return;
            }

            await ctx.EditReplyAsync(embed: new DiscordEmbedBuilder()
                .WithAuthor($"Moderation log for {user.GetUsernameWithDiscriminator()}")
                .AddField("Bans", modResponse.BanCount.ToString(), true)
                .AddField("Mutes", modResponse.MuteCount.ToString(), true)
                .AddField("Warns", modResponse.WarnCount.ToString(), true)
                .WithColor(GrimoireColor.Purple));
            return;
        }

        var response = await this._mediator.Send(new GetUserSinsQuery
        {
            UserId = user.Id, GuildId = ctx.Guild.Id, SinQueryType = sinQueryType
        });
        if (response.SinList.Length == 0)
            await ctx.EditReplyAsync(GrimoireColor.Green, "That user does not have any logs",
                $"Sin log for {user.GetUsernameWithDiscriminator()}");
        foreach (var message in response.SinList)
            await ctx.EditReplyAsync(GrimoireColor.Green, message,
                $"Sin log for {user.GetUsernameWithDiscriminator()}");
    }
}
