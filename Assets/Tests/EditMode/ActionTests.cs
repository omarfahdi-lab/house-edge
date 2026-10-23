using System;
using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // deal order: player, dealer up, player, dealer hole, then whatever is drawn next
    public class ActionTests
    {
        [Test]
        public void IllegalActions_Throw_AndAreNotRecorded()
        {
            var t = StackedTableNoTarget("10", "10", "7", "9");
            Assert.Throws<InvalidOperationException>(() => t.Apply(Hit));
            Assert.That(t.Inputs, Is.Empty);
        }

        [Test]
        public void OpeningHand_OffersHitStandDoubleAndSurrender()
        {
            var t = StackedTableNoTarget("10", "10", "7", "9");
            t.Apply(Deal);
            Assert.That(t.LegalActions(), Is.EquivalentTo(new[] { Hit, Stand, DoubleDown, Surrender }));
        }

        // double down

        [Test]
        public void DoubleDown_TakesExactlyOneCard_AndEndsTheHand()
        {
            var t = StackedTableNoTarget("6", "10", "5", "8", "2", "K");
            Play(t, Deal, DoubleDown);
            Assert.That(t.PlayerHands[0].Cards.Count, Is.EqualTo(3));
            Assert.That(t.PlayerHands[0].IsDoubled, Is.True);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.ReadyToDeal));
        }

        [Test]
        public void DoubleDown_IsOnlyOnTheFirstTwoCards()
        {
            var t = StackedTableNoTarget("2", "10", "3", "8", "2");
            Play(t, Deal, Hit);
            Assert.That(t.LegalActions(), Has.No.Member(DoubleDown));
        }

        // split

        [Test]
        public void Split_IsOnlyOfferedOnAPair()
        {
            var pair = StackedTableNoTarget("8", "10", "8", "9");
            pair.Apply(Deal);
            Assert.That(pair.LegalActions(), Has.Member(Split));

            var tenAndKing = StackedTableNoTarget("10", "10", "K", "9");
            tenAndKing.Apply(Deal);
            Assert.That(tenAndKing.LegalActions(), Has.No.Member(Split), "same value, different rank");
        }

        [Test]
        public void Split_MakesTwoHands_PlayedInOrder_AndScoredSeparately()
        {
            // 8|8 vs dealer 10+7. hand 1 draws 3 -> 11, hand 2 draws K -> 18
            var t = StackedTableNoTarget("8", "10", "8", "7", "3", "K", "10");
            Play(t, Deal, Split);
            Assert.That(t.PlayerHands.Count, Is.EqualTo(2));
            Assert.That(t.ActiveHandIndex, Is.EqualTo(0));

            // hand 1: hit 10 -> 21. hand 2 stands on 18
            Play(t, Hit, Stand);
            Assert.That(t.ActiveHandIndex, Is.EqualTo(1));
            t.Apply(Stand);

            Assert.That(t.LastScores.Select(s => s.Outcome), Is.EqualTo(new[] { Outcome.TwentyOne, Outcome.Win }));
            // 8-3-10 is a 21: (21 + 15) x 2. 8-K is a plain win: (18 + 5) x 1. neither makes a combo
            Assert.That(t.Score, Is.EqualTo((21 + 15) * 2 + (18 + 5) * 1));
            Assert.That(t.HandsLeft, Is.EqualTo(Balance.StartingHands - 1), "a split deal uses one Hand");
        }

        [Test]
        public void Split_CantSplitAgain_ButCanDoubleDown()
        {
            var t = StackedTableNoTarget("8", "10", "8", "7", "8", "3");
            Play(t, Deal, Split);
            Assert.That(t.LegalActions(), Has.No.Member(Split));
            Assert.That(t.LegalActions(), Has.Member(DoubleDown));
            Assert.That(t.LegalActions(), Has.No.Member(Surrender));
        }

        [Test]
        public void SplitAces_Making21_Score21_NotBlackjack()
        {
            // a heart King so A-K isn't Suited: a plain win, (21 + 5) x 1
            var t = StackedTableNoTarget("A", "10", "A", "7", "Kh", "9");
            Play(t, Deal, Split, Stand, Stand);
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Win));
            Assert.That(t.LastScores[0].Total, Is.EqualTo((21 + 5) * 1));
        }

        // burn

        [Test]
        public void Burn_IsOnlyAvailableAfterAHit()
        {
            var t = StackedTableNoTarget("10", "10", "2", "9", "3", "4");
            t.Apply(Deal);
            Assert.That(t.LegalActions(), Has.No.Member(Burn));
            t.Apply(Hit);
            Assert.That(t.LegalActions(), Has.Member(Burn));
        }

        [Test]
        public void Burn_ReplacesTheLastCard_AndUsesABurn()
        {
            var t = StackedTableNoTarget("10", "10", "2", "9", "3", "7");
            Play(t, Deal, Hit, Burn);
            Assert.That(t.PlayerHands[0].Cards.Select(c => c.Rank), Is.EqualTo(new[] { Rank.Ten, Rank.Two, Rank.Seven }));
            Assert.That(t.BurnsLeft, Is.EqualTo(Balance.StartingBurns - 1));
        }

        [Test]
        public void Burn_CanSaveABust()
        {
            // 10 + 6 hits a K (26). Burn swaps it for a 4 -> 20
            var t = StackedTableNoTarget("10", "10", "6", "9", "K", "4");
            Play(t, Deal, Hit);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.PlayerTurn), "waits so the player can burn");
            Assert.That(t.LegalActions(), Is.EquivalentTo(new[] { Burn, Stand, PalmClean, PalmSeen }));
            Play(t, Burn, Stand);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void Bust_WithNoBurnsLeft_EndsTheHand()
        {
            var t = StackedTableNoTarget("10", "10", "6", "9", "K", "Q", "J", "8");
            Play(t, Deal, Hit, Burn, Burn, Burn);
            // the palm could still save it, so the bust waits
            Assert.That(t.Phase, Is.EqualTo(TablePhase.PlayerTurn));
            Play(t, Stand);
            Assert.That(t.BurnsLeft, Is.EqualTo(0));
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Bust));
        }

        [Test]
        public void Burns_LastTheWholeTable_NotOneHand()
        {
            var t = StackedTableNoTarget("10", "10", "2", "9", "3", "4", "10", "10", "7", "9");
            Play(t, Deal, Hit, Burn, Stand, Deal);
            Assert.That(t.BurnsLeft, Is.EqualTo(Balance.StartingBurns - 1));
        }

        [Test]
        public void Burn_CantReplaceADoubleDownCard()
        {
            // 16 doubles into a K and busts. Burns are left, but the hand still ends
            var t = StackedTableNoTarget("10", "10", "6", "8", "K");
            Play(t, Deal, DoubleDown);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.ReadyToDeal));
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Bust));
        }

        // surrender

        [Test]
        public void Surrender_Costs3Gold_AndKeepsTheHand()
        {
            var t = StackedTableNoTarget("10", "10", "6", "9");
            Play(t, Deal, Surrender);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold - Balance.SurrenderCost));
            Assert.That(t.HandsLeft, Is.EqualTo(Balance.StartingHands));
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Surrender));
            Assert.That(t.Phase, Is.EqualTo(TablePhase.ReadyToDeal));
        }

        [Test]
        public void Surrender_NeedsEnoughGold()
        {
            var t = new Table(Stack("10", "10", "6", "9"), long.MaxValue, gold: 2);
            t.Apply(Deal);
            Assert.That(t.LegalActions(), Has.No.Member(Surrender));
        }

        [Test]
        public void Surrender_IsOnlyOnTheFirstTwoCards()
        {
            var t = StackedTableNoTarget("2", "10", "3", "9", "2");
            Play(t, Deal, Hit);
            Assert.That(t.LegalActions(), Has.No.Member(Surrender));
        }

        // insurance

        [Test]
        public void Insurance_IsOffered_WhenTheDealerShowsAnAce()
        {
            var t = StackedTableNoTarget("10", "A", "7", "6");
            t.Apply(Deal);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.InsuranceOffer));
            Assert.That(t.LegalActions(), Is.EquivalentTo(new[] { TakeInsurance, DeclineInsurance }));
        }

        [Test]
        public void Insurance_TurnsADealerBlackjackIntoAPush()
        {
            var t = StackedTableNoTarget("10", "A", "7", "K");
            Play(t, Deal, TakeInsurance);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold - Balance.InsuranceCost));
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Push));
            Assert.That(t.LastScores.Single().Total, Is.EqualTo(17));
        }

        [Test]
        public void DecliningInsurance_AgainstABlackjack_Loses()
        {
            var t = StackedTableNoTarget("10", "A", "7", "K");
            Play(t, Deal, DeclineInsurance);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold));
        }

        [Test]
        public void Insurance_WithoutADealerBlackjack_IsLostAndPlayGoesOn()
        {
            var t = StackedTableNoTarget("10", "A", "7", "6");
            Play(t, Deal, TakeInsurance);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.PlayerTurn));
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold - Balance.InsuranceCost));
        }

        [Test]
        public void Insurance_IsSkipped_WithoutEnoughGold()
        {
            var t = new Table(Stack("10", "A", "7", "6"), long.MaxValue, gold: 1);
            t.Apply(Deal);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.PlayerTurn));
        }

        // shared deck

        [Test]
        public void TheDeck_IsOnlyShuffledWhenTheTableStarts()
        {
            var t = StackedTableNoTarget("10", "10", "6", "9", "10", "10", "6", "9");
            int before = t.CardsInShoe;
            t.Apply(Deal);
            Assert.That(t.CardsInShoe, Is.EqualTo(before - 4), "cards come off the same deck, nothing is added back");
            t.Apply(Surrender);
            t.Apply(Deal);
            Assert.That(t.CardsInShoe, Is.EqualTo(before - 8), "used cards go to the discards, not back into the deck");
        }
    }
}
