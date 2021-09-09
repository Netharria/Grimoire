using System.Collections.Generic;
using System.Linq;
using Cybermancy.Domain.Shared;
using Cybermancy.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;

namespace Cybermancy.Persistence.Tests.Repositories
{
    public class BaseIdRepositoryTestBase<T> where T : Identifiable
    {
        protected BaseIdRepository<T> MockRepository { get; private set; }
        protected Mock<CybermancyDbContext> MockDbContext { get; private set; }
        protected Mock<DbSet<T>> MockDbSet { get; private set; }

        public void InstatiateMocks(List<T> list)
        {
            MockDbSet = list.AsQueryable().BuildMockDbSet();
            var options = new DbContextOptions<CybermancyDbContext>();
            MockDbContext = new Mock<CybermancyDbContext>(options);
            MockDbContext.Setup(x => x.Set<T>()).Returns(MockDbSet.Object);

            MockRepository = new BaseIdRepository<T>(MockDbContext.Object);
        }
    }
}