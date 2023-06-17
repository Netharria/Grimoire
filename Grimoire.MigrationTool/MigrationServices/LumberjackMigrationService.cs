// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using EFCore.BulkExtensions;
using Grimoire.Domain;
using Grimoire.MigrationTool.Domain;
using Grimoire.MigrationTool.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Grimoire.MigrationTool.MigrationServices
{
    public class LumberjackMigrationService
    {
        private readonly LumberjackDbContext _lumberjackContext;

        public LumberjackMigrationService(LumberjackDbContext context)
        {
            this._lumberjackContext = context;
        }
        public async Task MigrateLumberJackDatabaseAsync()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LumberjackPath")))
            {
                Log.Warning("Filepath for Lumberjack DB was empty.");
                return;
            }
            await this.MigrateSettingsAsync();
            await this.MigrateChannelsAsync();
            await this.MigrateUsersAsync();
            await this.MigrateMembersAsync();
            await this.MigrateUsernamesAsync();
            await this.MigrateNicknamesAsync();
            await this.MigrateAvatarsAsync();
            await this.MigrateMessagesAsync();
            await this.MigrateAttachmentsAsync();
            await this.MigrateTrackers();
        }

        private async Task MigrateTrackers()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();

            var lumberjackTrackers = await this._lumberjackContext.Trackers
                .Select(tracker => new Grimoire.Domain.Tracker
                {
                    UserId = tracker.UserId,
                    GuildId = tracker.GuildId,
                    LogChannelId = tracker.LogChannelId,
                    EndTime = tracker.Endtime,
                    ModeratorId = tracker.ModeratorId
                }).ToListAsync();

            var grimoireTrackers = await grimoireDbContext.Trackers.Select(x => new { x.UserId, x.GuildId }).ToListAsync();

            var attachmentsToAdd = lumberjackTrackers.ExceptBy(grimoireTrackers, x => new { x.UserId, x.GuildId });

            await grimoireDbContext.BulkInsertAsync(attachmentsToAdd);
            await grimoireDbContext.BulkSaveChangesAsync();
        }

        private async Task MigrateAttachmentsAsync()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
            var lumberjackAttachments = await this._lumberjackContext.AttachmentUrls
                .Select(attachment => new Attachment
                {
                    Id = ulong.Parse(new DirectoryInfo(Path.GetDirectoryName(attachment.Url)!).Name),
                    FileName = Path.GetFileName(attachment.Url),
                    MessageId = attachment.MessageId
                }).ToListAsync();
            var grimoireAttachments = await grimoireDbContext.Attachments.Select(x => x.Id).ToListAsync();

            var attachmentsToAdd = lumberjackAttachments.ExceptBy(grimoireAttachments, x => x.Id);
            await grimoireDbContext.BulkInsertAsync(attachmentsToAdd);
            await grimoireDbContext.BulkSaveChangesAsync();
        }

        private async Task MigrateMessagesAsync()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
            var lumberjackMessages = await this._lumberjackContext.Messages
                .Select(message => new Message
                {
                    Id = message.Id,
                    UserId = message.AuthorId,
                    ChannelId = message.ChannelId,
                    GuildId = message.GuildId,
                    CreatedTimestamp = message.CreatedAt.ToUniversalTime(),
                    MessageHistory = new List<MessageHistory>
                        {
                            new MessageHistory
                            {
                                MessageId = message.Id,
                                MessageContent = message.CleanContent,
                                GuildId = message.GuildId,
                                Action = MessageAction.Created,
                                TimeStamp = message.CreatedAt.ToUniversalTime()
                            }
                        }
                }).ToListAsync();

            var grimoireMessages = await grimoireDbContext.Messages.Select(x => x.Id).ToListAsync();
            var messagesToAdd = lumberjackMessages.ExceptBy(grimoireMessages, x => x.Id);

            await grimoireDbContext.BulkInsertAsync(messagesToAdd);
            await grimoireDbContext.BulkSaveChangesAsync();

            var messageHistoryToAdd = messagesToAdd.SelectMany(x => x.MessageHistory)
                .ToList();

            var bulkConfig = new BulkConfig();
            bulkConfig.SqlBulkCopyOptions = bulkConfig.SqlBulkCopyOptions & ~SqlBulkCopyOptions.KeepIdentity;
            bulkConfig.PropertiesToExclude = new List<string> { "Id" };

            await grimoireDbContext.BulkInsertAsync(messageHistoryToAdd, bulkConfig);
            await grimoireDbContext.BulkSaveChangesAsync();
        }

        private async Task MigrateAvatarsAsync()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
            var lumberjackAvatar = await this._lumberjackContext.Messages
                .Select(x => new Avatar
                {
                    UserId = x.AuthorId,
                    GuildId = x.GuildId,
                    FileName = x.AvatarUrl.Replace("size=1024", "size=128", StringComparison.CurrentCultureIgnoreCase),
                    Timestamp = x.CreatedAt
                }).ToListAsync();

            var grimoireAvatars = await grimoireDbContext.Avatars
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

            await grimoireDbContext.AddRangeAsync(avatarsToAdd);
            await grimoireDbContext.SaveChangesAsync();
        }

        private async Task MigrateNicknamesAsync()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
            var lumberjackNicknames = await this._lumberjackContext.Messages
                .Select(x => new NicknameHistory
                {
                    GuildId = x.GuildId,
                    UserId = x.AuthorId,
                    Nickname = x.AuthorDisplayName,
                    Timestamp = x.CreatedAt
                }).ToListAsync();

            var grimoireNicknameHistory = await grimoireDbContext.NicknameHistory.Select(x => new
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

            await grimoireDbContext.AddRangeAsync(nickNamesToAdd);
            await grimoireDbContext.SaveChangesAsync();
        }

        private async Task MigrateUsernamesAsync()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();

            var lumberjackUsernameHistory = await this._lumberjackContext.Messages
                .Select(x => new UsernameHistory
                {
                    Username = x.AuthorName,
                    UserId = x.AuthorId,
                    Timestamp = x.CreatedAt.ToUniversalTime()
                })
                .ToListAsync();
            var grimoireUsernames = await grimoireDbContext.UsernameHistory.Select(x => new
            {
                x.Username,
                x.UserId
            }).ToListAsync();

            lumberjackUsernameHistory.ForEach(x =>
            {
                if (x.Username.EndsWith("#0"))
                    x.Username = x.Username[..^2];
            });

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

            await grimoireDbContext.AddRangeAsync(usernameHistoryToAdd);
            await grimoireDbContext.SaveChangesAsync();
        }

        private async Task MigrateMembersAsync()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
            var lumberjackMembers = await this._lumberjackContext.Messages
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
            var grimoireMembers = await grimoireDbContext.Members.
                Select(x => new { x.UserId, x.GuildId }).ToListAsync();
            var usersToAdd = lumberjackMembers.ExceptBy(grimoireMembers,x => new { x.UserId, x.GuildId });

            await grimoireDbContext.AddRangeAsync(usersToAdd);
            await grimoireDbContext.SaveChangesAsync();
        }

        private async Task MigrateUsersAsync()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
            var users = await this._lumberjackContext.Messages
                .Select(x => new User
                {
                    Id = x.AuthorId
                })
                .Distinct().ToListAsync();
            var grimoireUsers = await grimoireDbContext.Users.Select(x => x.Id).ToListAsync();
            var usersToAdd = users.ExceptBy(grimoireUsers,x => x.Id);

            await grimoireDbContext.BulkInsertAsync(usersToAdd);
            await grimoireDbContext.BulkSaveChangesAsync();
        }

        private async Task MigrateChannelsAsync()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();

            var channels = await this._lumberjackContext.Messages
                .Select(x => new Channel
                {
                    Id = x.ChannelId,
                    GuildId = x.GuildId
                }).Distinct().ToListAsync();

            var grimoireChannels = await grimoireDbContext.Channels.Select(x => x.Id).ToListAsync();

            var channelsToAdd = channels.ExceptBy(grimoireChannels, x => x.Id);
            await grimoireDbContext.BulkInsertAsync(channelsToAdd);
            await grimoireDbContext.BulkSaveChangesAsync();
        }

        private async Task MigrateSettingsAsync()
        {
            using var grimoireDbContext = GrimoireDBContextBuilder.GetGrimoireDbContext();
            var lumberjackSettings = await this._lumberjackContext.LogChannels.ToListAsync();

            foreach (var lumberjackGuild in lumberjackSettings)
            {
                var grimoireGuild = await grimoireDbContext.Guilds
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
                    await grimoireDbContext.AddAsync(grimoireGuild);
                    await grimoireDbContext.SaveChangesAsync();
                }
                grimoireGuild.UpdateGuildLogSettings(lumberjackGuild);

                grimoireDbContext.Update(grimoireGuild);
            }
            await grimoireDbContext.SaveChangesAsync();
        }
    }
}
