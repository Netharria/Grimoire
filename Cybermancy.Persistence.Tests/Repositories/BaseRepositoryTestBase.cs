using Cybermancy.Persistance;
using Cybermancy.Persistance.Repositories;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace Cybermancy.Persistence.Tests.Repositories
{
    public class BaseRepositoryTestBase<T> where T : class
    {
        protected BaseRepository<T> MockRepository { get; private set; }
        protected Mock<CybermancyDbContext> MockDbContext { get; private set; }
        protected Mock<DbSet<T>> MockDbSet { get; private set; }
        public void InstatiateMocks(List<T> list)
        {
            MockDbSet = list.AsQueryable().BuildMockDbSet();
            var options = new DbContextOptions<CybermancyDbContext>();
            MockDbContext = new Mock<CybermancyDbContext>(options);
            MockDbContext.Setup(x => x.Set<T>()).Returns(MockDbSet.Object);

            MockRepository = new BaseRepository<T>(MockDbContext.Object);
        }
    }
}
