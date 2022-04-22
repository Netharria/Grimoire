// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
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
                LevelSettings = new GuildLevelSettings(),
                ModerationSettings = new GuildModerationSettings(),
                LogSettings = new GuildLogSettings(),
            };
        public static Guild Guild2 { get; } =
            new Guild
            {
                Id = 2,
                LevelSettings = new GuildLevelSettings(),
                ModerationSettings = new GuildModerationSettings(),
                LogSettings = new GuildLogSettings(),
            };
        public static Channel Channel { get; } = new Channel { Id = 3, GuildId = 1 };

        public static User User1 { get; } = new User { Id = 4 };
        public static User User2 { get; } = new User { Id = 5 };
        public static Member Member1 { get; } = new Member { UserId = User1.Id, GuildId = Guild1.Id };
        public static Member Member2 { get; } = new Member { UserId = User2.Id, GuildId = Guild1.Id };
        public static Member Member3 { get; } = new Member { UserId = User1.Id, GuildId = Guild2.Id, IsXpIgnored = true };
        public static ICybermancyDbContext Create()
        {
            var options = new DbContextOptionsBuilder<CybermancyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new CybermancyDbContext(options);

            context.Database.EnsureCreated();

            context.Guilds.AddRange(Guild1, Guild2);
            context.Channels.Add(Channel);
            context.Users.AddRange(User1, User2);
            context.Members.AddRange(Member1, Member2, Member3);
            context.SaveChanges();

            return context;
        }
    }
}
