using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    public class DealerTests
    {
        [TestCase(new[] { "10", "6" }, true)]
        [TestCase(new[] { "10", "7" }, false)]
        [TestCase(new[] { "A", "6" }, false)]
        [TestCase(new[] { "A", "5" }, true)]
        [TestCase(new[] { "10", "8" }, false)]
        public void HitsBelow17_StandsOnAll17s(string[] cards, bool hits)
        {
            Assert.That(DealerRules.ShouldHit(Value(cards)), Is.EqualTo(hits));
        }

        [Test]
        public void DealerDrawsUntil17()
        {
            // dealer 10 + 2, then 3 and 2 -> 17 and stops
            var t = StackedTableNoTarget("10", "10", "9", "2", "3", "2", "K");
            Play(t, Deal, Stand);
            Assert.That(t.DealerCards.Select(c => c.ToString()), Is.EqualTo(new[] { "10♠", "2♠", "3♠", "2♠" }));
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void HoleCard_StaysHidden_UntilThePlayerFinishes()
        {
            var t = StackedTableNoTarget("10", "10", "6", "7");
            t.Apply(Deal);
            Assert.That(t.HoleCardHidden, Is.True);
            Assert.That(t.DealerVisibleValue.Total, Is.EqualTo(10));
            t.Apply(Stand);
            Assert.That(t.HoleCardHidden, Is.False);
        }

        [Test]
        public void DealerBlackjack_UnderATen_EndsTheHandAtOnce()
        {
            var t = StackedTableNoTarget("10", "K", "9", "A");
            t.Apply(Deal);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(t.Phase, Is.EqualTo(TablePhase.ReadyToDeal));
            Assert.That(t.HandsLeft, Is.EqualTo(Balance.StartingHands - 1));
        }

        [Test]
        public void BothBlackjack_IsAPush()
        {
            var t = StackedTableNoTarget("A", "K", "Q", "A");
            t.Apply(Deal);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Push));
        }

        [Test]
        public void NoPeek_UnderALowCard()
        {
            var t = StackedTableNoTarget("10", "6", "9", "A");
            t.Apply(Deal);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.PlayerTurn));
        }
    }
}
