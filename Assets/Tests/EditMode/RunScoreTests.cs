using NUnit.Framework;

namespace ZombieYard.Tests
{
    public sealed class RunScoreTests
    {
        [Test]
        public void OnlyDeliveredPurchasesSpendKillRewards()
        {
            var score = new RunScore();
            score.RecordKill(ZombieKind.Normal);
            Assert.AreEqual(1, score.Balance);
            score.RecordKill(ZombieKind.Red);
            Assert.AreEqual(3, score.Balance);
            score.RecordKill(ZombieKind.Purple);
            Assert.AreEqual(6, score.Balance);
            Assert.IsFalse(score.TryPurchase(7, () => throw new AssertionException("Unaffordable delivery")));
            Assert.IsFalse(score.TryPurchase(6, () => false));
            Assert.AreEqual(6, score.Balance);
            Assert.IsTrue(score.TryPurchase(6, () => true));
            Assert.AreEqual(0, score.Balance);
            Assert.AreEqual(3, score.Total);
            Assert.AreEqual((1, 1, 1), (score.Normal, score.Red, score.Purple));
        }
    }
}
