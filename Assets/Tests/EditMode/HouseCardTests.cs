using System.Collections.Generic;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // deal order: player, dealer up, player, dealer hole, then whatever is drawn next
    public class HouseCardTests
    {
        private static Card H(string label, HouseCard kind)
        {
            var card = C(label);
            card.House = kind;
            return card;
        }

        // House card rules on, but none added: the test stacks its own
        private static Table HouseTable(params object[] topFirst) =>
            new Table(StackOf(topFirst), long.MaxValue, new TableEffect[] { new HouseRules(0) });

        [Test]
        public void Bounty_YoursDoublesAWinAndPaysGold()
        {
            var plain = HouseTable("10", "9", C("9"), "8");
            Play(plain, Deal, Stand);
            var bounty = HouseTable("10", "9", H("9", HouseCard.Bounty), "8");
            int gold = bounty.Gold;
            Play(bounty, Deal, Stand);
            Assert.That(bounty.LastScores[0].Outcome, Is.EqualTo(Outcome.Win));
            Assert.That(bounty.LastScores[0].Mult, Is.EqualTo(plain.LastScores[0].Mult * Balance.BountyMult).Within(1e-9));
            Assert.That(bounty.Gold, Is.EqualTo(gold + Balance.BountyGold));
        }

        [Test]
        public void Bounty_YoursCostsAnExtraHandWhenYouLose()
        {
            var t = HouseTable("10", "10", H("7", HouseCard.Bounty), "9");
            int hands = t.HandsLeft;
            Play(t, Deal, Stand);
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(t.HandsLeft, Is.EqualTo(hands - 1 - Balance.BountyHandsLost));
            Assert.That(t.Notes, Is.Not.Empty);
        }

        [Test]
        public void Bounty_TheDealersPaysYouForBeatingHim()
        {
            var t = HouseTable("10", "9", "10", H("8", HouseCard.Bounty));
            int gold = t.Gold;
            Play(t, Deal, Stand);
            Assert.That(t.Gold, Is.EqualTo(gold + Balance.BountyDealerGold));
        }

        [Test]
        public void Mirror_YoursCopiesTheDealersUpCard()
        {
            var t = HouseTable(H("2", HouseCard.Mirror), "K", "5", "7");
            t.Apply(Deal);
            Assert.That(t.ActiveHand.Value.Total, Is.EqualTo(15));
        }

        [Test]
        public void Mirror_TheDealersCopiesYourFirstCard()
        {
            var t = HouseTable("9", "10", "5", H("2", HouseCard.Mirror));
            Play(t, Deal, Stand);
            Assert.That(t.DealerValue.Total, Is.EqualTo(19));
        }

        [Test]
        public void Fuse_YoursAllowsOneMoreCardThenNoMore()
        {
            var t = HouseTable("5", "10", H("3", HouseCard.Fuse), "7", "2", "2");
            t.Apply(Deal);
            Assert.That(t.LegalActions(), Has.Member(Hit));
            t.Apply(Hit);
            Assert.That(t.LegalActions(), Has.No.Member(Hit));
            Assert.That(t.LegalActions(), Has.No.Member(DoubleDown));
            Assert.That(t.LegalActions(), Has.Member(Stand));
        }

        [Test]
        public void Fuse_TheDealersStopsHimBelow17()
        {
            var t = HouseTable("10", "6", "8", H("6", HouseCard.Fuse), "5");
            Play(t, Deal, Stand);
            Assert.That(t.DealerCards.Count, Is.EqualTo(2));
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void LoadedDie_YoursTurnsATieIntoAWin()
        {
            var t = HouseTable("10", "10", H("8", HouseCard.LoadedDie), "8");
            Play(t, Deal, Stand);
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void LoadedDie_TheDealers21IsABust()
        {
            var t = HouseTable("10", "10", "Q", H("5", HouseCard.LoadedDie), "6");
            Play(t, Deal, Stand);
            Assert.That(t.DealerValue.Total, Is.EqualTo(21));
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void Snitch_TheDealersShowsHisHoleCard()
        {
            var t = HouseTable("10", H("5", HouseCard.Snitch), "7", "9");
            t.Apply(Deal);
            Assert.That(t.HoleCardHidden, Is.False);
        }

        [Test]
        public void Snitch_YoursAddsHeat()
        {
            var t = HouseTable(H("10", HouseCard.Snitch), "9", "7", "9");
            int heat = t.Gambler.Heat;
            t.Apply(Deal);
            Assert.That(t.Gambler.Heat, Is.EqualTo(heat + Balance.SnitchHeat));
        }

        [Test]
        public void Leech_TheDealersStealsABurn_AndYoursAddsMult()
        {
            var t = HouseTable("10", "9", "7", H("9", HouseCard.Leech));
            int burns = t.BurnsLeft;
            t.Apply(Deal);
            Assert.That(t.BurnsLeft, Is.EqualTo(burns - 1));

            var plain = HouseTable("10", "9", C("9"), "8");
            Play(plain, Deal, Stand);
            var leech = HouseTable("10", "9", H("9", HouseCard.Leech), "8");
            Play(leech, Deal, Stand);
            Assert.That(leech.LastScores[0].Mult, Is.EqualTo(plain.LastScores[0].Mult + Balance.LeechMult).Within(1e-9));
        }

        [Test]
        public void KeepTwoOfThree_PreviewsWhatAMirrorWillCopy()
        {
            // dealer shows 8; you're offered 9, a Mirror and 6
            var t = HouseTable("9", "8", H("J", HouseCard.Mirror), "6");
            t.Rules.KeepTwoOfThree = true;
            t.Apply(Deal);
            var (keptMirror, _) = t.PreviewGive(0);
            Assert.That(keptMirror.Total, Is.EqualTo(14), "kept, the Mirror copies his 8");
            var (_, givenMirror) = t.PreviewGive(1);
            Assert.That(givenMirror.Total, Is.EqualTo(17), "given, it copies your first card, the 9");

            t.Apply(GiveSecond);
            Assert.That(t.DealerValue.Total, Is.EqualTo(givenMirror.Total));
        }

        [Test]
        public void TheRunOption_ShufflesThemIntoTheShoe_NotYourDeck()
        {
            var plain = new Run(3);
            var run = new Run(3, new RunOptions { HouseCards = true });
            Assert.That(run.Table.CardsInShoe, Is.EqualTo(plain.Table.CardsInShoe + Balance.HouseCards(1, false)));
            Assert.That(run.Gambler.Deck.Count, Is.EqualTo(plain.Gambler.Deck.Count));
        }

        [Test]
        public void TheyGetMoreCommonEveryFloor()
        {
            Assert.That(Balance.HouseCards(8, false), Is.GreaterThan(Balance.HouseCards(1, false)));
            Assert.That(Balance.HouseCards(1, true), Is.GreaterThan(Balance.HouseCards(1, false)));
        }

        [Test]
        public void RunCodes_RememberThem()
        {
            string code = RunCodes.Encode(99, new RunOptions { HouseCards = true, KeepTwoOfThree = true });
            Assert.That(RunCodes.TryDecode(code, out _, out var back), Is.True, code);
            Assert.That(back.HouseCards, Is.True);
            Assert.That(back.KeepTwoOfThree, Is.True);
        }

        [Test]
        public void TheBot_PlaysWholeRuns_AndTheyReplayTheSame()
        {
            var options = new RunOptions { HouseCards = true, KeepTwoOfThree = true };
            for (int seed = 1; seed <= 5; seed++)
            {
                var run = new Run(seed, options);
                var inputs = new List<RunInput>();
                var bot = new BalanceBot();
                for (int i = 0; i < 6000 && !run.IsOver; i++)
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
    }
}
