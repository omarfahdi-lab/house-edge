using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // poker-style combos inside a hand, and the win streak. deal order: player, dealer up, player, dealer hole
    public class ComboTests
    {
        private static List<Combo> Find(params string[] labels) => Combos.Find(Cards(labels));

        private static HandScore Only(Table t) => t.LastScores.Single();

        // finding combos

        [Test]
        public void Pair_IsTwoCardsOfTheSameRank()
        {
            Assert.That(Find("9", "9h", "2d"), Is.EqualTo(new[] { Combo.Pair }));
        }

        [Test]
        public void TwoPair_IsTwoDifferentPairs()
        {
            Assert.That(Find("9", "9h", "8d", "8c"), Is.EqualTo(new[] { Combo.TwoPair }));
        }

        [Test]
        public void ThreeOfAKind_ReplacesThePair()
        {
            var found = Find("7", "7h", "7d");
            Assert.That(found, Is.EqualTo(new[] { Combo.ThreeOfAKind }));
            Assert.That(found, Has.No.Member(Combo.Pair));
        }

        [Test]
        public void Suited_IsATwoCardHandOfOneSuit()
        {
            Assert.That(Find("2h", "9h"), Is.EqualTo(new[] { Combo.Suited }));
        }

        [Test]
        public void Flush_IsThreeOrMoreOfOneSuit_AndReplacesSuited()
        {
            var found = Find("2h", "7h", "9h");
            Assert.That(found, Is.EqualTo(new[] { Combo.Flush }));
            Assert.That(found, Has.No.Member(Combo.Suited));
        }

        [Test]
        public void Run_IsThreeRanksInARow()
        {
            Assert.That(Find("5", "6h", "7d"), Is.EqualTo(new[] { Combo.Run }));
        }

        [Test]
        public void Run_CountsAcesLow_AndHigh_ButDoesntWrapAround()
        {
            Assert.That(Find("A", "2h", "3d"), Has.Member(Combo.Run), "A-2-3");
            Assert.That(Find("Q", "Kh", "Ad"), Has.Member(Combo.Run), "Q-K-A");
            Assert.That(Find("K", "Ah", "2d"), Has.No.Member(Combo.Run), "K-A-2");
        }

        [Test]
        public void TwoInARow_IsntARun()
        {
            Assert.That(Find("5", "6h", "9d"), Is.Empty);
        }

        [Test]
        public void RoyalCourt_IsAJackQueenAndKing()
        {
            // J-Q-K is also a Run, and they stack
            Assert.That(Find("J", "Qh", "Kd"), Is.EquivalentTo(new[] { Combo.RoyalCourt, Combo.Run }));
        }

        [Test]
        public void LowBall_IsFourOrMoreCards_All5OrLower()
        {
            // aces count as low
            Assert.That(Find("A", "2h", "4d", "5c"), Is.EqualTo(new[] { Combo.LowBall }));
            Assert.That(Find("2", "4h", "6d", "5c"), Has.No.Member(Combo.LowBall), "a 6 is too high");
            Assert.That(Find("A", "2h", "4d"), Has.No.Member(Combo.LowBall), "needs four cards");
        }

        [Test]
        public void Curses_DontCountTowardAnything()
        {
            // curse cards are a Two of Spades underneath. counted, these would be Three of a Kind and a Flush
            var hand = new List<Card> { C("2"), Card.Cursed(Curse.DeadCard), Card.Cursed(Curse.TaxCard) };
            Assert.That(Combos.Find(hand), Is.Empty);

            var pair = new List<Card> { C("9"), C("9h"), Card.Cursed(Curse.DeadCard) };
            Assert.That(Combos.Find(pair), Is.EqualTo(new[] { Combo.Pair }));
        }

        // scoring combos

        [Test]
        public void AWinningPair_AddsThePairsChipsAndMult()
        {
            // 9-9 = 18 beats 17: (18 + 5 Win + 10 Pair) x (1 Win + 1 Pair)
            var t = new Table(Stack("9", "10", "9h", "7"), long.MaxValue);
            Play(t, Deal, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Win));
            Assert.That(Only(t).Combos, Is.EqualTo(new[] { Combo.Pair }));
            Assert.That(Only(t).Chips, Is.EqualTo(18 + 5 + 10));
            Assert.That(Only(t).Mult, Is.EqualTo(1 + 1));
            Assert.That(Only(t).Steps.Select(s => s.Kind), Has.Member(ScoreSource.Combo));
        }

        [Test]
        public void Combos_DontScore_OnALoss()
        {
            // 8-8 = 16 loses to 19
            var t = new Table(Stack("8", "10", "8h", "9"), long.MaxValue);
            Play(t, Deal, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(Only(t).Combos, Is.Empty);
            Assert.That(Only(t).Total, Is.EqualTo(0));
        }

        [Test]
        public void Combos_DontScore_OnAPush()
        {
            // 9-9 = 18 ties 18: cards only, x1
            var t = new Table(Stack("9", "10", "9h", "8"), long.MaxValue);
            Play(t, Deal, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Push));
            Assert.That(Only(t).Combos, Is.Empty);
            Assert.That(Only(t).Total, Is.EqualTo(18));
        }

        [Test]
        public void AComboComp_AddsItsChipsAndMultPerLevel()
        {
            // the winning pair from above, plus 2 levels of the Pair Comp (+10 Chips, +1 Mult each)
            var t = new Table(Stack("9", "10", "9h", "7"), long.MaxValue);
            t.Gambler.LevelUp(Comp.Pair);
            t.Gambler.LevelUp(Comp.Pair);
            Play(t, Deal, Stand);
            Assert.That(Comps.AppliesTo(Only(t)), Has.Member(Comp.Pair));
            Assert.That(Only(t).Chips, Is.EqualTo(18 + 5 + 10 + 20));
            Assert.That(Only(t).Mult, Is.EqualTo(1 + 1 + 2));
        }

        [Test]
        public void AComboComp_DoesNothing_WithoutTheCombo()
        {
            // K-Q makes no combo, so the Pair Comp stays out of it
            var t = new Table(Stack("K", "9", "Q", "9"), long.MaxValue);
            t.Gambler.LevelUp(Comp.Pair);
            Play(t, Deal, Stand);
            Assert.That(Comps.AppliesTo(Only(t)), Has.No.Member(Comp.Pair));
            Assert.That(Only(t).Chips, Is.EqualTo(20 + 5));
        }

        // the win streak

        [Test]
        public void WinStreak_Adds1MultPerPreviousWin_AndALossResetsIt()
        {
            var t = StackedTableNoTarget(
                "K", "9", "Q", "9",   // win
                "K", "9", "Q", "9",   // win
                "K", "9", "Q", "9",   // win
                "K", "10", "7", "9",  // lose
                "K", "9", "Q", "9");  // win
            Play(t, Deal, Stand);
            Assert.That(Only(t).Mult, Is.EqualTo(1));
            Play(t, Deal, Stand);
            Assert.That(Only(t).Mult, Is.EqualTo(1 + 1));
            Play(t, Deal, Stand);
            Assert.That(Only(t).Mult, Is.EqualTo(1 + 2));
            Assert.That(t.WinStreak, Is.EqualTo(3));

            Play(t, Deal, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(t.WinStreak, Is.EqualTo(0));

            Play(t, Deal, Stand);
            Assert.That(Only(t).Mult, Is.EqualTo(1));
        }

        [Test]
        public void APush_LeavesTheStreakAlone()
        {
            var t = StackedTableNoTarget(
                "K", "9", "Q", "9",   // win
                "K", "9", "9", "K",   // push
                "K", "9", "Q", "9");  // win
            Play(t, Deal, Stand, Deal, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Push));
            Assert.That(Only(t).Mult, Is.EqualTo(1), "a push gets no streak Mult");
            Play(t, Deal, Stand);
            Assert.That(Only(t).Mult, Is.EqualTo(1 + 1));
        }
    }
}
