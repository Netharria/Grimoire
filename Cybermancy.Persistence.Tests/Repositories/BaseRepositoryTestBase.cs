// -----------------------------------------------------------------------
// <copyright file="BaseRepositoryTestBase.cs" company="Netharia">
// Copyright (c) Netharia. All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Cybermancy.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;

namespace Cybermancy.Persistence.Tests.Repositories
{
    public class BaseRepositoryTestBase<T>
        where T : class
    {
        protected BaseRepository<T> MockRepository { get; private set; }
        protected Mock<CybermancyDbContext> MockDbContext { get; private set; }
        protected Mock<DbSet<T>> MockDbSet { get; private set; }

        public void InstatiateMocks(List<T> list)
        {
            this.MockDbSet = list.AsQueryable().BuildMockDbSet();
            var options = new DbContextOptions<CybermancyDbContext>();
            this.MockDbContext = new Mock<CybermancyDbContext>(options);
            this.MockDbContext.Setup(x => x.Set<T>()).Returns(this.MockDbSet.Object);

            this.MockRepository = new BaseRepository<T>(this.MockDbContext.Object);
        }
    }
}
