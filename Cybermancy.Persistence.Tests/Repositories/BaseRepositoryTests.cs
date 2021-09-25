// This file is part of the Cybermancy Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cybermancy.Domain;
using Moq;
using NUnit.Framework;

namespace Cybermancy.Persistence.Tests.Repositories
{
    [TestFixture]
    public class BaseRepositoryTests : BaseRepositoryTestBase<GuildLevelSettings>
    {
        private readonly Guild _guild1 = new() { Id = 1 };
        private readonly Guild _guild2 = new() { Id = 2 };
        private GuildLevelSettings _settings1;
        private GuildLevelSettings _settings2;
        private readonly List<GuildLevelSettings> _list = new();

        [SetUp]
        public void SetUp()
        {
            this._settings1 = new GuildLevelSettings { Guild = this._guild1 };
            this._list.Add(this._settings1);
            this._settings2 = new GuildLevelSettings { Guild = this._guild2 };
            this._list.Add(this._settings2);
            this.InstatiateMocks(this._list);
        }

        [Test]
        public async Task GetAllAsync_ReturnsAllEntitiesAsync()
        {
            var result = await this.MockRepository.GetAllAsync();
            Assert.IsTrue(result.Any(x => x.Guild.Id == 1), "First Guild Setting Not Found");
            Assert.IsTrue(result.Any(x => x.Guild.Id == 2), "Second Guild Setting Not Found");
        }

        [Test]
        public async Task AddAsync_SavesEntityAsync()
        {
            await this.MockRepository.AddAsync(this._settings1);
            this.MockDbContext.Verify(x => x.Set<GuildLevelSettings>(), Times.Once);
            this.MockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }

        [Test]
        public async Task DeleteAsync_DeletesEntityAsync()
        {
            await this.MockRepository.DeleteAsync(this._settings1);
            this.MockDbContext.Verify(x => x.Set<GuildLevelSettings>(), Times.Once);
            this.MockDbContext.Verify(x => x.SaveChangesAsync(default), Times.Once);
        }
    }
}
