// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license.See LICENSE file in the project root for full license information.

using System.Text;

namespace Grimoire.Features.Moderation.Mute.Commands;

public partial class MuteAdminCommands
{
    private const int EmbedFieldLimit = 1024;

    /// <summary>Five full fields stay within Discord's limit for a whole embed.</summary>
    private const int MaxMutedUserFields = 5;

    [Command("View")]
    [Description("View the current configured mute role and any active mutes.")]
    public async Task ViewMutesAsync(CommandContext ctx)
    {
        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(true);
        else
            await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        var muteRole = await this._settingsModule.GetConfiguredMuteRole(guild.GetGuildId())
            .GetOrElse(() => null);
        var role = await guild.GetRoleOrDefaultAsync(muteRole);
        var now = DateTimeOffset.UtcNow;
        var users = await Task.WhenAll(await this._settingsModule.GetAllMutes(guild.GetGuildId())
            .Where(mute => mute.EndTime > now)
            .Select(mute => DescribeMutedUserAsync(guild, mute.UserId))
            .ToArrayAsync());
        var embed = new DiscordEmbedBuilder();

        embed.AddField("Mute Role", role?.Mention ?? "None");
        var fields = JoinIntoFields(users).ToArray();
        if (fields.Length == 0)
            embed.AddField("Muted Users", "None");
        foreach (var (index, field) in fields.Take(MaxMutedUserFields).Index())
            embed.AddField(index == 0 ? "Muted Users" : "Muted Users (continued)", field);
        if (fields.Length > MaxMutedUserFields)
            embed.AddField("And More", "There are too many muted users to list them all.");

        await ctx.ReplyAsync(embed: embed);
    }

    private static async Task<string> DescribeMutedUserAsync(DiscordGuild guild, UserId userId)
        => await guild.GetMemberOrDefaultAsync(userId) is { } member
            ? member.Mention
            : $"`{userId}` (left the server)";

    private static IEnumerable<string> JoinIntoFields(IEnumerable<string> users)
    {
        var field = new StringBuilder();
        foreach (var user in users)
        {
            if (field.Length + user.Length + 1 > EmbedFieldLimit)
            {
                yield return field.ToString();
                field.Clear();
            }

            field.Append(field.Length == 0 ? user : $" {user}");
        }

        if (field.Length > 0)
            yield return field.ToString();
    }
}
