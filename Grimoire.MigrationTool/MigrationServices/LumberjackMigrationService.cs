// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using Grimoire.Core;
using Grimoire.Domain;
using Grimoire.MigrationTool.Domain;
using Grimoire.MigrationTool.Domain.Lumberjack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Grimoire.MigrationTool.MigrationServices
{
    public static class LumberjackMigrationService
    {
        public static async Task MigrateLumberJackDatabaseAsync(IConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LumberjackPath")))
            {
                Log.Warning("Filepath for Lumberjack DB was empty.");
                return;
            }
            await MigrateSettingsAsync(configuration);
            await MigrateChannelsAsync(configuration);
            await MigrateUsersAsync(configuration);
            await MigrateMembersAsync(configuration);
            await MigrateUsernamesAsync(configuration);
            await MigrateNicknamesAsync(configuration);
            await MigrateAvatarsAsync(configuration);
            await MigrateMessagesAsync(configuration);
            await MigrateAttachmentsAsync(configuration);
            await MigrateTrackers(configuration);
        }

        private static async Task MigrateTrackers(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);

            var lumberjackTrackers = await lumberjackContext.Trackers
                .Select(tracker => new Grimoire.Domain.Tracker
                {
                    UserId = tracker.UserId,
                    GuildId = tracker.GuildId,
                    LogChannelId = tracker.LogChannelId,
                    EndTime = tracker.Endtime,
                    ModeratorId = tracker.ModeratorId
                }).ToListAsync();

            var grimoireTrackers = await grimoireContext.Trackers.Select(x => new { x.UserId, x.GuildId }).ToListAsync();

            var attachmentsToAdd = lumberjackTrackers.ExceptBy(grimoireTrackers, x => new { x.UserId, x.GuildId });

            await grimoireContext.AddRangeAsync(attachmentsToAdd);
            await grimoireContext.SaveChangesAsync();
        }

        private static async Task MigrateAttachmentsAsync(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);

            var lumberjackAttachments = await lumberjackContext.AttachmentUrls
                .Select(attachment => new Attachment
                {
                    Id = ulong.Parse(new DirectoryInfo(Path.GetDirectoryName(attachment.Url)).Name),
                    FileName = Path.GetFileName(attachment.Url),
                    MessageId = attachment.MessageId
                }).ToListAsync();
            var grimoireAttachments = await grimoireContext.Attachments.Select(x => x.Id).ToListAsync();

            var attachmentsToAdd = lumberjackAttachments.ExceptBy(grimoireAttachments, x => x.Id);
            await grimoireContext.AddRangeAsync(attachmentsToAdd);
            await grimoireContext.SaveChangesAsync();
        }

        private static async Task MigrateMessagesAsync(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);

            var lumberjackMessages = await lumberjackContext.Messages
                .Select(message => new Grimoire.Domain.Message
                {
                    Id = message.Id,
                    UserId = message.AuthorId,
                    ChannelId = message.ChannelId,
                    GuildId = message.GuildId,
                    CreatedTimestamp = message.CreatedAt,
                    MessageHistory = new List<MessageHistory>
                        {
                            new MessageHistory
                            {
                                MessageId = message.Id,
                                MessageContent = message.CleanContent,
                                GuildId = message.GuildId,
                                Action = MessageAction.Created,
                                TimeStamp = message.CreatedAt
                            }
                        }
                }).ToListAsync();
            var grimoireMessages = await grimoireContext.Messages.Select(x => x.Id).ToListAsync();

            var messagesToAdd = lumberjackMessages.ExceptBy(grimoireMessages, x => x.Id);

            await grimoireContext.AddRangeAsync(messagesToAdd);
            await grimoireContext.SaveChangesAsync();
        }

        private static async Task MigrateAvatarsAsync(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);

            var lumberjackAvatar = await lumberjackContext.Messages
                .Select(x => new Avatar
                {
                    UserId = x.AuthorId,
                    GuildId = x.GuildId,
                    FileName = x.AvatarUrl.Replace("size=1024", "size=128", StringComparison.CurrentCultureIgnoreCase),
                    Timestamp = x.CreatedAt
                }).ToListAsync();

            var grimoireAvatars = await grimoireContext.Avatars
                .Select(x => new
                {
                    x.UserId,
                    x.GuildId,
                    x.FileName
                }).ToListAsync();

            var avatarsToAdd = lumberjackAvatar.DistinctBy(x => new { x.UserId, x.FileName })
                .ExceptBy(grimoireAvatars, x => new
                {
                    x.UserId,
                    x.GuildId,
                    x.FileName
                });

            await grimoireContext.AddRangeAsync(avatarsToAdd);
            await grimoireContext.SaveChangesAsync();
        }

        private static async Task MigrateNicknamesAsync(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);

            var lumberjackNicknames = await lumberjackContext.Messages
                .Select(x => new NicknameHistory
                {
                    GuildId = x.GuildId,
                    UserId = x.AuthorId,
                    Nickname = x.AuthorDisplayName,
                    Timestamp = x.CreatedAt
                }).ToListAsync();

            var grimoireNicknameHistory = await grimoireContext.NicknameHistory.Select(x => new
            {
                x.GuildId,
                x.UserId,
                x.Nickname
            }).ToListAsync();

            var nickNamesToAdd = lumberjackNicknames.DistinctBy(x => new
            {
                x.GuildId,
                x.UserId,
                x.Nickname
            }).ExceptBy(grimoireNicknameHistory,x => new
            {
                x.GuildId,
                x.UserId,
                x.Nickname
            });

            await grimoireContext.AddRangeAsync(nickNamesToAdd);
            await grimoireContext.SaveChangesAsync();
        }

        private static async Task MigrateUsernamesAsync(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);

            var lumberjackUsernameHistory = await lumberjackContext.Messages
                .Select(x => new UsernameHistory
                {
                    Username = x.AuthorName,
                    UserId = x.AuthorId,
                    Timestamp = x.CreatedAt
                })
                .ToListAsync();
            var grimoireUsernames = await grimoireContext.UsernameHistory.Select(x => new
            {
                x.Username,
                x.UserId
            }).ToListAsync();

            var usernameHistoryToAdd = lumberjackUsernameHistory
                .DistinctBy(x => new
                {
                    x.Username,
                    x.UserId
                })
                .ExceptBy(grimoireUsernames,x => new
                {
                    x.Username,
                    x.UserId
                });

            await grimoireContext.AddRangeAsync(usernameHistoryToAdd);
            await grimoireContext.SaveChangesAsync();
        }

        private static async Task MigrateMembersAsync(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);

            var lumberjackMembers = await lumberjackContext.Messages
                .Select(x => new Member
                {
                    UserId = x.AuthorId,
                    GuildId = x.GuildId,
                    XpHistory = new List<XpHistory>
                        {
                            new XpHistory
                            {
                                UserId = x.AuthorId,
                                GuildId = x.GuildId,
                                Xp = 0,
                                Type = XpHistoryType.Created,
                                TimeOut = DateTimeOffset.UtcNow
                            }
                        },
                }).Distinct().ToListAsync();
            var grimoireMembers = await grimoireContext.Members.
                Select(x => new { x.UserId, x.GuildId }).ToListAsync();
            var usersToAdd = lumberjackMembers.ExceptBy(grimoireMembers,x => new { x.UserId, x.GuildId });

            await grimoireContext.AddRangeAsync(usersToAdd);
            await grimoireContext.SaveChangesAsync();
        }

        private static async Task MigrateUsersAsync(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);

            var users = await lumberjackContext.Messages
                .Select(x => new User
                {
                    Id = x.AuthorId
                })
                .Distinct().ToListAsync();
            var grimoireUsers = await grimoireContext.Users.Select(x => x.Id).ToListAsync();
            var usersToAdd = users.ExceptBy(grimoireUsers,x => x.Id);

            await grimoireContext.AddRangeAsync(usersToAdd);
            await grimoireContext.SaveChangesAsync();
        }

        private static async Task MigrateChannelsAsync(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);
            var channels = await lumberjackContext.Messages
                .Select(x => new Channel
                {
                    Id = x.ChannelId,
                    GuildId = x.GuildId
                }).Distinct().ToListAsync();

            var grimoireChannels = await grimoireContext.Channels.Select(x => x.Id).ToListAsync();

            var channelsToAdd = channels.ExceptBy(grimoireChannels, x => x.Id);
            await grimoireContext.AddRangeAsync(channelsToAdd);
            await grimoireContext.SaveChangesAsync();
        }

        private static async Task MigrateSettingsAsync(IConfiguration configuration)
        {
            using var lumberjackContext = new LumberjackDbContext();
            using var grimoireContext = GetGrimoireContext(configuration);

            var lumberjackSettings = await lumberjackContext.LogChannels.ToListAsync();

            foreach (var lumberjackGuild in lumberjackSettings)
            {
                var grimoireGuild = await grimoireContext.Guilds
                    .Include(x => x.MessageLogSettings)
                    .Include(x => x.UserLogSettings)
                    .Include(x => x.Channels)
                    .FirstOrDefaultAsync(guild => guild.Id == lumberjackGuild.GuildId);

                if (grimoireGuild is null)
                {
                    grimoireGuild = new Guild
                    {
                        Id = lumberjackGuild.GuildId,
                        LevelSettings = new GuildLevelSettings(),
                        ModerationSettings = new GuildModerationSettings(),
                        UserLogSettings = new GuildUserLogSettings(),
                        MessageLogSettings = new GuildMessageLogSettings()
                    };
                    await grimoireContext.AddAsync(grimoireGuild);
                    await grimoireContext.SaveChangesAsync();
                }
                grimoireGuild.UpdateGuildLogSettings(lumberjackGuild);

                grimoireContext.Update(grimoireGuild);
                await grimoireContext.SaveChangesAsync();
            }
        }

        private static void UpdateGuildLogSettings(this Guild guild, LogChannelSettings logChannelSettings)
        {
            guild.UserLogSettings.JoinChannelLogId =
                logChannelSettings.JoinLogId != 0
                ? logChannelSettings.JoinLogId
                : null;
            guild.AddChannel(logChannelSettings.JoinLogId);
            guild.UserLogSettings.LeaveChannelLogId =
                logChannelSettings.LeaveLogId != 0
                ? logChannelSettings.LeaveLogId
                : null;
            guild.AddChannel(logChannelSettings.LeaveLogId);
            guild.UserLogSettings.UsernameChannelLogId =
                logChannelSettings.UsernameLogId != 0
                ? logChannelSettings.UsernameLogId
                : null;
            guild.AddChannel(logChannelSettings.UsernameLogId);
            guild.UserLogSettings.NicknameChannelLogId =
                logChannelSettings.NicknameLogId != 0
                ? logChannelSettings.NicknameLogId
                : null;
            guild.AddChannel(logChannelSettings.NicknameLogId);
            guild.UserLogSettings.AvatarChannelLogId =
                logChannelSettings.AvatarLogId != 0
                ? logChannelSettings.AvatarLogId
                : null;
            guild.AddChannel(logChannelSettings.AvatarLogId);
            guild.UserLogSettings.ModuleEnabled = true;

            guild.MessageLogSettings.DeleteChannelLogId =
                logChannelSettings.DeleteLogId != 0
                ? logChannelSettings.DeleteLogId
                : null;
            guild.AddChannel(logChannelSettings.DeleteLogId);
            guild.MessageLogSettings.BulkDeleteChannelLogId =
                logChannelSettings.BulkDeleteLogId != 0
                ? logChannelSettings.BulkDeleteLogId
                : null;
            guild.AddChannel(logChannelSettings.BulkDeleteLogId);
            guild.MessageLogSettings.EditChannelLogId =
                logChannelSettings.EditLogId != 0
                ? logChannelSettings.EditLogId
                : null;
            guild.AddChannel(logChannelSettings.EditLogId);
            guild.MessageLogSettings.ModuleEnabled = true;

            guild.ModChannelLog = logChannelSettings.ModLogId;
            guild.AddChannel(logChannelSettings.ModLogId);
        }

        private static void AddChannel(this Guild guild, ulong? channelId)
        {
            if (channelId is not null && channelId != 0 && !guild.Channels.Any(x => x.Id == channelId))
            {
                guild.Channels.Add(new Channel
                {
                    Id = channelId.Value,
                    GuildId = guild.Id
                });
            }
        }

        private static GrimoireDbContext GetGrimoireContext(IConfiguration configuration)
        {
            var connectionString =
                    configuration.GetConnectionString("Grimoire");
            return new GrimoireDbContext(
                new DbContextOptionsBuilder<GrimoireDbContext>()
                .UseNpgsql(connectionString)
                .UseLoggerFactory(new LoggerFactory().AddSerilog())
                .Options);
        }
    }
}
