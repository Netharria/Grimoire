using Grimoire.Settings.Enums;
using JetBrains.Annotations;

namespace Grimoire.Features.Shared.Commands;

internal sealed partial class GeneralSettingsCommands
{
    [UsedImplicitly]
    [Command("View")]
    [Description("View the current general settings for this server.")]
    public async Task ViewAsync(CommandContext ctx)
    {
        if (ctx is SlashCommandContext slashContext)
            await slashContext.DeferResponseAsync(true);
        else
            await ctx.DeferResponseAsync();

        var guild = ctx.Guild!;

        var modLogChannelId = await this._settingsModule.GetLogChannelSetting(GuildLogType.Moderation, guild.GetGuildId());
        var userCommandChannelId = await this._settingsModule.GetUserCommandChannel(guild.GetGuildId());

        var moderationLogText = modLogChannelId is null
            ? "None"
            : ChannelExtensions.Mention(modLogChannelId.Value);
        var userCommandChannelText = userCommandChannelId is null
            ? "None"
            : ChannelExtensions.Mention(userCommandChannelId.Value);
        await ctx.EditReplyAsync(title: "General Settings",
            message: $"**Moderation Log:** {moderationLogText}\n**User Command Channel:** {userCommandChannelText}");
    }
}
