// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Cybermancy.Core.Contracts.Persistance;
using Cybermancy.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cybermancy.Core.Test.Unit
{
    public static class TestCybermancyDbContextFactory
    {
        public static Guild Guild1 { get; } =
            new Guild
            {
                Id = 1,
                LevelSettings = new GuildLevelSettings() { ModuleEnabled = true, Amount = 5, Base = 10, Modifier = 50, TextTime = TimeSpan.FromMinutes(3) },
                ModerationSettings = new GuildModerationSettings() { ModuleEnabled = true },
                LogSettings = new GuildLogSettings() { ModuleEnabled = true },
            };
        public static Guild Guild2 { get; } =
            new Guild
            {
                Id = 2,
                LevelSettings = new GuildLevelSettings() { Amount = 5, Base = 10, Modifier = 50, TextTime = TimeSpan.FromMinutes(3) },
                ModerationSettings = new GuildModerationSettings(),
                LogSettings = new GuildLogSettings(),
            };
        public static Channel Channel { get; } = new Channel { Id = 3, GuildId = 1 };

        public static User User1 { get; } = new User { Id = 4 };
        public static User User2 { get; } = new User { Id = 5 };
        public static Member Member1 { get; } = new Member { UserId = User1.Id, GuildId = Guild1.Id };
        public static Member Member2 { get; } = new Member { UserId = User2.Id, GuildId = Guild1.Id };
        public static Member Member3 { get; } = new Member { UserId = User1.Id, GuildId = Guild2.Id, IsXpIgnored = true };
        public static Role Role1 { get; } = new Role { Id = 6, GuildId = Guild1.Id, };
        public static Role Role2 { get; } = new Role { Id = 7, GuildId = Guild1.Id, };

        public static async Task<ICybermancyDbContext> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<CybermancyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new CybermancyDbContext(options);

            context.Database.EnsureCreated();

            await context.Guilds.AddRangeAsync(Guild1, Guild2);
            await context.Channels.AddAsync(Channel);
            await context.Users.AddRangeAsync(User1, User2);
            await context.Members.AddRangeAsync(Member1, Member2, Member3);
            await context.Roles.AddRangeAsync(Role1, Role2);
            await context.SaveChangesAsync();

            return context;
        }
    }
}
