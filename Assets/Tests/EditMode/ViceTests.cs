using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // deal order: player, dealer up, player, dealer hole, then draws
    public class ViceTests
    {
        private static Vice V(string name) => ViceCatalog.All.First(d => d.Name == name).Create();

        private static Table With(Vice vice, params string[] cards) =>
            new Table(Stack(cards), long.MaxValue, new TableEffect[] { vice });

        private static HandScore Score(Table t) => t.LastScores.Single();

        [Test]
        public void Catalog_HasTheFirst20Vices_WithUniqueIds()
        {
            Assert.That(ViceCatalog.All.Count, Is.GreaterThanOrEqualTo(20));
            Assert.That(ViceCatalog.All.Select(d => d.Id).Distinct().Count(), Is.EqualTo(ViceCatalog.All.Count));
        }

        [Test]
        public void EveryVice_CreatesAnInstanceThatKnowsItsDefinition()
        {
            foreach (var def in ViceCatalog.All)
                Assert.That(def.Create().Definition, Is.SameAs(def), def.Name);
        }

        [Test]
        public void LuckyPenny_Adds4MultOnAWin_NotOnAPush()
        {
            // the DESIGN.md example: win with 20 -> 30 Chips, 2 + 4 Mult
            var win = With(V("Lucky Penny"), "K", "9", "Q", "9");
            Play(win, Deal, Stand);
            Assert.That(Score(win).Total, Is.EqualTo(30 * 6));

            var push = With(V("Lucky Penny"), "K", "9", "9", "K");
            Play(push, Deal, Stand);
            Assert.That(Score(push).Mult, Is.EqualTo(1));
        }

        [Test]
        public void HotStreak_Grows2MultPerConsecutiveWin_AndResetsOnALoss()
        {
            var t = With(V("Hot Streak"),
                "K", "9", "Q", "9",   // win
                "K", "9", "Q", "9",   // win
                "K", "10", "7", "9",  // lose
                "K", "9", "Q", "9");  // win
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 2));
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 4));
            Play(t, Deal, Stand, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 2));
        }

        [Test]
        public void LowRoller_Adds8ChipsPerTwoThroughFive()
        {
            // 5 + 3 + 10 = 18 beats 17. two low cards -> +16 Chips
            var t = With(V("Low Roller"), "5", "10", "3", "7", "10");
            Play(t, Deal, Hit, Stand);
            Assert.That(Score(t).Chips, Is.EqualTo(18 + 10 + 16));
        }

        [Test]
        public void HighRoller_Adds2MultPerTenValueCard()
        {
            // the DESIGN.md example: two 10s, Lucky Penny + High Roller -> 30 x 10 = 300
            var t = new Table(Stack("K", "9", "Q", "9"), long.MaxValue, new[] { V("Lucky Penny"), V("High Roller") });
            Play(t, Deal, Stand);
            Assert.That(Score(t).Total, Is.EqualTo(300));
        }

        [Test]
        public void CardSharp_Adds25Chips_WithFourCards()
        {
            // 2 + 3 + 5 + 10 = 20 beats 17 with four cards
            var t = With(V("Card Sharp"), "2", "10", "3", "7", "5", "10");
            Play(t, Deal, Hit, Hit, Stand);
            Assert.That(Score(t).Chips, Is.EqualTo(20 + 10 + 25));
        }

        [Test]
        public void PhotoFinish_Adds10Mult_WhenWinningByExactlyOne()
        {
            var t = With(V("Photo Finish"), "K", "9", "9", "9");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 10));

            var byTwo = With(V("Photo Finish"), "K", "8", "Q", "10");
            Play(byTwo, Deal, Stand);
            Assert.That(Score(byTwo).Mult, Is.EqualTo(2));
        }

        [Test]
        public void SoreLoser_Pays3GoldOnALoss()
        {
            var t = With(V("Sore Loser"), "K", "10", "7", "9");
            Play(t, Deal, Stand);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold + 3));
        }

        [Test]
        public void DealersFriend_Adds30Chips_WhenTheDealerBusts()
        {
            var t = With(V("Dealer's Friend"), "10", "10", "8", "6", "K");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Chips, Is.EqualTo(18 + 10 + 15 + 30));
        }

        [Test]
        public void PocketPair_Adds6Mult_ForAStartingPair()
        {
            var t = With(V("Pocket Pair"), "9", "10", "9", "7");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 6));
        }

        [Test]
        public void SuitUp_Adds5Mult_WhenEveryCardSharesASuit()
        {
            var t = With(V("Suit Up"), "Kh", "9", "9h", "7", "K");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 5));
        }

        [Test]
        public void PennyPincher_Pays1GoldPerUnusedBurn_AtTableEnd()
        {
            var t = new Table(Stack("A", "9", "K", "8"), 1, new[] { V("Penny Pincher") });
            t.Apply(Deal);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.Won));
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold + Balance.StartingBurns));
        }

        [Test]
        public void Espresso_Adds1Hand()
        {
            var t = With(V("Espresso"), "K", "9");
            Assert.That(t.HandsLeft, Is.EqualTo(Balance.StartingHands + 1));
        }

        [Test]
        public void SteadyHand_Adds20Chips_ForWinningAfterStandingOn17OrLess()
        {
            var t = With(V("Steady Hand"), "10", "10", "7", "6", "K");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Chips, Is.EqualTo(17 + 10 + 15 + 20));
        }

        [Test]
        public void EvenMoney_Adds8Mult_ForAnEvenTotal()
        {
            var t = With(V("Even Money"), "K", "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 8));
        }

        [Test]
        public void CoinFlip_IsDeterministic_ForASeed()
        {
            long ScoreWithSeed(int seed)
            {
                var t = new Table(new Gambler(), Stack("K", "9", "Q", "9"), new GameRandom(seed), long.MaxValue, new[] { V("Coin Flip") });
                Play(t, Deal, Stand);
                return Score(t).Total;
            }
            var results = Enumerable.Range(1, 20).Select(ScoreWithSeed).ToList();
            Assert.That(results, Is.EqualTo(Enumerable.Range(1, 20).Select(ScoreWithSeed).ToList()));
            Assert.That(results.Distinct(), Is.EquivalentTo(new long[] { 60, 360 }), "either 30 x 2 or 30 x 12");
        }

        [Test]
        public void IronWill_Adds3MultPerLowStandThisTable()
        {
            var t = With(V("Iron Will"),
                "10", "10", "6", "6", "K",   // stand on 16, dealer busts
                "K", "9", "Q", "9");         // win with 20
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 3));
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 3), "standing on 20 doesn't count");
        }

        [Test]
        public void SoftTouch_DoublesTheMult_OfASoftHand()
        {
            var t = With(V("Soft Touch"), "A", "10", "8", "8");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(4));
        }

        [Test]
        public void Bouncer_TurnsTheFirstBustEachTableIntoAPush()
        {
            // bust 26 -> push scored as 21. the second bust is a real bust
            var t = new Table(Stack("10", "10", "6", "9", "K", "10", "10", "6", "9", "K"), long.MaxValue,
                new[] { V("Bouncer") });
            Play(t, Deal, Hit, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Push));
            Assert.That(Score(t).Total, Is.EqualTo(21));
            Play(t, Deal, Hit, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Bust));
        }

        [Test]
        public void FreshFelt_TriplesTheFirstHandOnly()
        {
            var t = With(V("Fresh Felt"), "K", "9", "Q", "9", "K", "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(6));
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2));
        }

        [Test]
        public void FaceValue_Adds10ChipsAnd1MultPerFaceCard()
        {
            var t = With(V("Face Value"), "K", "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Chips, Is.EqualTo(30 + 20));
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 2));
        }
    }
}
