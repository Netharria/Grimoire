using System.Collections.Generic;
using System.Threading.Tasks;
using Cybermancy.Domain;
using Moq;
using NUnit.Framework;

namespace Cybermancy.Persistence.Tests.Repositories
{
    public class BaseIdRepositoryTests : BaseIdRepositoryTestBase<Guild>
    {
        private Guild Guild1 { get; } = new() {Id = 1};
        private Guild Guild2 { get; } = new() {Id = 2};
        private List<Guild> List { get; } = new();

        [SetUp]
        public void SetUp()
        {
            List.Add(Guild1);
            List.Add(Guild2);
            InstatiateMocks(List);
        }

        [Test]
        public void WhenCheckingIfEntityExists_ReturnCorrectResult()
        {
            Assert.IsTrue(MockRepository.Exists(1), "Expected Item to exist and it did not.");
            Assert.IsFalse(MockRepository.Exists(3), "Expected Item to not exist and it did.");
        }

        [Test]
        public async Task WhenGettingById_ReturnResult()
        {
            MockDbSet.Setup(x => x.FindAsync((ulong) 1)).ReturnsAsync(Guild1);
            var result = await MockRepository.GetByIdAsync(1);
            Assert.AreEqual(Guild1, result);
        }
    }
}