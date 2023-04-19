// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Grimoire.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Grimoire.Core.Test.Unit
{
    public class TestDatabaseFixture
    {

        private static readonly object _lock = new();

        public static Guild Guild1 { get; } =
            new Guild
            {
                Id = 1,
                LevelSettings = new GuildLevelSettings() { ModuleEnabled = true, Amount = 5, Base = 10, Modifier = 50, TextTime = TimeSpan.FromMinutes(3) },
                ModerationSettings = new GuildModerationSettings() { ModuleEnabled = true },
                UserLogSettings = new GuildUserLogSettings() { ModuleEnabled = true },
            };
        public static Guild Guild2 { get; } =
            new Guild
            {
                Id = 2,
                LevelSettings = new GuildLevelSettings() { Amount = 5, Base = 10, Modifier = 50, TextTime = TimeSpan.FromMinutes(3) },
                ModerationSettings = new GuildModerationSettings(),
                UserLogSettings = new GuildUserLogSettings(),
            };
        public static Channel Channel1 { get; } = new Channel { Id = 3, GuildId = 1 };
        public static Channel Channel2 { get; } = new Channel { Id = 12, GuildId = 1, IsXpIgnored = true };
        public static User User1 { get; } = new User { Id = 4 };
        public static User User2 { get; } = new User { Id = 5 };
        public static Member Member1 { get; } = new Member { UserId = User1.Id, GuildId = Guild1.Id, Guild = Guild1 };
        public static Member Member2 { get; } = new Member { UserId = User2.Id, GuildId = Guild1.Id, Guild = Guild1 };
        public static Member Member3 { get; } = new Member { UserId = User1.Id, GuildId = Guild2.Id, Guild = Guild2, IsXpIgnored = true };
        public static Role Role1 { get; } = new Role { Id = 6, GuildId = Guild1.Id, };
        public static Role Role2 { get; } = new Role { Id = 7, GuildId = Guild1.Id, IsXpIgnored = true };
        public static Reward Reward1 { get; } = new Reward { RoleId = Role2.Id, GuildId = Guild1.Id, RewardLevel = 10 };

        public TestDatabaseFixture()
        {
            lock (_lock)
            {
                using (var context = this.CreateContext())
                {
                    context.Database.EnsureDeleted();
                    context.Database.EnsureCreated();
                    context.Guilds.AddRange(Guild1, Guild2);
                    context.Channels.AddRange(Channel1, Channel2);
                    context.Users.AddRange(User1, User2);
                    context.Members.AddRange(Member1, Member2, Member3);
                    context.Roles.AddRange(Role1, Role2);
                    context.Rewards.AddRange(Reward1);
                    context.SaveChanges();
                }
            }
        }

        public GrimoireDbContext CreateContext()
        {
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
            return new GrimoireDbContext(
                new DbContextOptionsBuilder<GrimoireDbContext>()
                    .UseNpgsql(configuration.GetConnectionString("GrimoireConnectionString"))
                    .Options);
        }
    }
}
