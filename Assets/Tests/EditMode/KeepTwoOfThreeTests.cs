using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // Keep 2 of 3: the deal order is the usual player, dealer up, player, then your third card where
    // the dealer's hole card would be. the card you give away becomes his hole card
    public class KeepTwoOfThreeTests
    {
        private static Table KeepTable(params object[] topFirst)
        {
            var t = TableOf(topFirst);
            t.Rules.KeepTwoOfThree = true;
            return t;
        }

        [Test]
        public void Deal_OffersThreeCards_AndWaitsForAChoice()
        {
            var t = KeepTable("10", "10", "6", "5");
            t.Apply(Deal);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.Choosing));
            Assert.That(t.OfferedCards.Select(c => c.Value), Is.EqualTo(new[] { 10, 6, 5 }));
            Assert.That(t.DealerCards.Count, Is.EqualTo(1));
            Assert.That(t.LegalActions(), Is.EquivalentTo(new[] { GiveFirst, GiveSecond, GiveThird }));
            Assert.That(t.ActiveHand, Is.Null);
        }

        [Test]
        public void Giving_KeepsTheOtherTwo_AndTheGiftIsTheDealersHoleCardFaceUp()
        {
            var t = KeepTable("10", "10", "6", "5");
            Play(t, Deal, GiveSecond);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.PlayerTurn));
            Assert.That(t.ActiveHand.Cards.Select(c => c.Value), Is.EqualTo(new[] { 10, 5 }));
            Assert.That(t.DealerCards.Select(c => c.Value), Is.EqualTo(new[] { 10, 6 }));
            Assert.That(t.HoleCardHidden, Is.False);
            Assert.That(t.OfferedCards, Is.Empty);
        }

        [Test]
        public void TheDealerPlaysTheHandYouGaveHim()
        {
            // he has 10 + 6 = 16, hits the 9 and busts
            var t = KeepTable("10", "10", "6", "5", "9");
            Play(t, Deal, GiveSecond, Stand);
            Assert.That(t.DealerBusted, Is.True);
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void GivingATenToAnAce_IsABlackjack_WithNoInsuranceOffered()
        {
            var t = KeepTable("9", "A", "K", "8");
            t.Apply(Deal);
            Assert.That(t.LegalActions(), Has.No.Member(TakeInsurance));
            t.Apply(GiveSecond);
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(t.Phase, Is.EqualTo(TablePhase.ReadyToDeal));
        }

        [Test]
        public void KeepingABlackjack_EndsYourTurn()
        {
            var t = KeepTable("A", "9", "K", "2", "8");
            Play(t, Deal, GiveThird);
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Blackjack));
        }

        [Test]
        public void ABlackStamp_CantBeGivenToTheDealer()
        {
            var t = KeepTable("10", "9", C("K", Stamp.Black), "4");
            t.Apply(Deal);
            Assert.That(t.LegalActions(), Is.EquivalentTo(new[] { GiveFirst, GiveThird }));
        }

        [Test]
        public void TheOfferIsDiscarded_WhenTheTableIsCleared()
        {
            var t = KeepTable("10", "10", "6", "5", "9", "2", "3", "4", "7", "8");
            int before = t.CardsInShoe;
            Play(t, Deal, GiveFirst, Stand);
            Assert.That(t.CardsInShoe, Is.LessThan(before));
            Assert.That(t.OfferedCards, Is.Empty);
        }

        [Test]
        public void OffByDefault()
        {
            var t = TableOf("10", "10", "6", "5");
            t.Apply(Deal);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.PlayerTurn));
            Assert.That(t.ActiveHand.Cards.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheRunOption_TurnsItOnWithFewerHands_ButNotAtTheTutorialTable()
        {
            var plain = new Run(7);
            var run = new Run(7, new RunOptions { KeepTwoOfThree = true });
            Assert.That(run.Table.Rules.KeepTwoOfThree, Is.True);
            Assert.That(run.Table.HandsLeft, Is.EqualTo(plain.Table.HandsLeft - Balance.KeepTwoOfThreeHandsLess));
            var tutorial = new Run(7, new RunOptions { KeepTwoOfThree = true, Tutorial = true });
            Assert.That(tutorial.Table.Rules.KeepTwoOfThree, Is.False);
            Assert.That(tutorial.Table.HandsLeft, Is.EqualTo(plain.Table.HandsLeft), "the tutorial table keeps its usual Hands");
        }

        [Test]
        public void RunCodes_RememberIt()
        {
            string code = RunCodes.Encode(12345, new RunOptions { KeepTwoOfThree = true });
            Assert.That(RunCodes.TryDecode(code, out _, out var back), Is.True, code);
            Assert.That(back.KeepTwoOfThree, Is.True);
            RunCodes.TryDecode(RunCodes.Encode(12345, new RunOptions()), out _, out var plain);
            Assert.That(plain.KeepTwoOfThree, Is.False);
        }

        [Test]
        public void TheBot_PlaysWholeRuns_AndTheyReplayTheSame()
        {
            var options = new RunOptions { KeepTwoOfThree = true };
            for (int seed = 1; seed <= 5; seed++)
            {
                var run = new Run(seed, options);
                var inputs = new System.Collections.Generic.List<RunInput>();
                var bot = new BalanceBot();
                for (int i = 0; i < 5000 && !run.IsOver; i++)
                {
                    var input = bot.Choose(run);
                    inputs.Add(input);
                    run.Apply(input);
                }
                Assert.That(run.IsOver, Is.True, $"seed {seed}");
                var replayed = Run.Replay(seed, inputs, options);
                Assert.That(replayed.Gambler.Gold, Is.EqualTo(run.Gambler.Gold), $"seed {seed}");
                Assert.That(replayed.TablesBeaten, Is.EqualTo(run.TablesBeaten), $"seed {seed}");
            }
        }

        [Test]
        public void TheBot_GivesTheDealerTheCardThatHurtsHim()
        {
            // dealer shows 10; you're dealt 10, 6, 5. handing him the 10 (he'd have 20) is the worst choice
            var t = KeepTable("10", "10", "6", "5");
            t.Apply(Deal);
            Assert.That(BalanceBot.ChooseAction(t, t.Gambler), Is.Not.EqualTo(GiveFirst));
        }
    }
}
