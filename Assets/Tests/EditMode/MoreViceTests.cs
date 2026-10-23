using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // deal order: player, dealer up, player, dealer hole, then draws
    public class MoreViceTests
    {
        private static Vice V(string name) => ViceCatalog.All.First(d => d.Name == name).Create();

        private static Table With(Vice vice, params object[] cards) => With(new[] { vice }, cards);

        private static Table With(Vice[] vices, params object[] cards)
        {
            var gambler = new Gambler();
            gambler.Vices.AddRange(vices);
            return new Table(gambler, StackOf(cards), new GameRandom(1), long.MaxValue, vices);
        }

        private static HandScore Score(Table t) => t.LastScores.Single();

        [Test]
        public void TheCatalog_HasEveryViceFromTheDesign()
        {
            // 23 common, 27 uncommon, 20 rare, 10 legendary, 4 cursed (the suit and combo Vices brought it from 70 to 78,
            // the count Vices and the rule-breaking Legendaries to 84)
            Assert.That(ViceCatalog.All.Count, Is.EqualTo(84));
            Assert.That(ViceCatalog.OfRarity(Rarity.Common).Count(), Is.EqualTo(23));
            Assert.That(ViceCatalog.OfRarity(Rarity.Uncommon).Count(), Is.EqualTo(27));
            Assert.That(ViceCatalog.OfRarity(Rarity.Rare).Count(), Is.EqualTo(20));
            Assert.That(ViceCatalog.OfRarity(Rarity.Legendary).Count(), Is.EqualTo(10));
            Assert.That(ViceCatalog.OfRarity(Rarity.Cursed).Count(), Is.EqualTo(4));
            Assert.That(ViceCatalog.All.Select(v => v.Id).Distinct().Count(), Is.EqualTo(84));
        }

        [Test]
        public void EveryArchetype_HasVices()
        {
            foreach (Archetype a in System.Enum.GetValues(typeof(Archetype)))
                Assert.That(ViceCatalog.All.Count(v => v.Archetype == a && !v.IsCursed), Is.GreaterThanOrEqualTo(6), a.ToString());
        }

        [Test]
        public void PokerFace_Cuts40PercentOfHeat()
        {
            var g = new Gambler();
            g.Vices.Add(V("Poker Face"));
            g.AddHeat(10);
            Assert.That(g.Heat, Is.EqualTo(6));
        }

        [Test]
        public void SideHustle_MakesSideBetsCheaper()
        {
            var t = With(V("Side Hustle"), "K", "9", "Q", "9");
            Assert.That(t.SideBetCost(SideBet.PerfectPair), Is.EqualTo(1));
        }

        [Test]
        public void CardCounter_ShowsTheNextCard_AndAddsHeat()
        {
            var t = With(V("Card Counter"), "10", "10", "6", "9", "4");
            t.Apply(Deal);
            Assert.That(t.NextCardPreview?.ToString(), Is.EqualTo("4♠"));
            Assert.That(t.Gambler.Heat, Is.EqualTo(2));
        }

        [Test]
        public void XRaySpecs_ShowsTheHoleCard()
        {
            var t = With(V("X-Ray Specs"), "10", "10", "6", "9");
            t.Apply(Deal);
            Assert.That(t.HoleCardHidden, Is.False);
            Assert.That(t.Gambler.Heat, Is.EqualTo(3));
        }

        [Test]
        public void SplitPersonality_SplitsAnyTwo()
        {
            var t = With(V("Split Personality"), "8", "10", "K", "9");
            t.Apply(Deal);
            Assert.That(t.LegalActions(), Has.Member(Split));
        }

        [Test]
        public void StretchLimo_Lets22Stand()
        {
            var t = With(V("Stretch Limo"), "10", "10", "6", "8", "6", "K");
            Play(t, Deal, Hit);
            Assert.That(t.PlayerHands[0].Value.IsBust, Is.False);
        }

        [Test]
        public void InsuranceAgent_Pays4_WhenTheDealerShowsAnAce()
        {
            var t = With(V("Insurance Agent"), "10", "A", "7", "6");
            t.Apply(Deal);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold + 4));
        }

        [Test]
        public void Bankroll_Adds1MultPer5Gold()
        {
            var t = With(V("Bankroll"), "K", "9", "Q", "9");
            t.Gambler.GainGold(6);
            Play(t, Deal, Stand);
            // a win's x1 + 10 gold / 5
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 2));
        }

        [Test]
        public void Burnout_Gives4MultPerBurnThisTable()
        {
            var t = With(V("Burnout"), "10", "10", "6", "8", "K", "3");
            Play(t, Deal, Hit, Burn, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 4));
        }

        [Test]
        public void Pickpocket_StealsTheBustCard_AsAGildedCopy()
        {
            var t = With(V("Pickpocket"), "10", "10", "8", "6", "K");
            int deck = t.Gambler.Deck.Count;
            Play(t, Deal, Stand);
            Assert.That(t.Gambler.Deck.Count, Is.EqualTo(deck + 1));
            Assert.That(t.Gambler.Deck.Last().Enhancement, Is.EqualTo(Enhancement.Gilded));
        }

        [Test]
        public void DoublingCube_GrowsWithEveryDoubleDownWin()
        {
            var cube = V("Doubling Cube");
            var t = With(cube, "6", "10", "5", "8", "K", "K", "9", "Q", "9");
            Play(t, Deal, DoubleDown);
            Play(t, Deal, Stand);
            // a win's x1 + the table's +1 streak Mult for the first win + the cube's +1
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 1 + 1));
        }

        [Test]
        public void SleightOfHand_MakesTheFirstBurnFree()
        {
            var t = With(V("Sleight of Hand"), "10", "10", "2", "9", "3", "4");
            Play(t, Deal, Hit, Burn);
            Assert.That(t.BurnsLeft, Is.EqualTo(Balance.StartingBurns));
            Assert.That(t.Gambler.Heat, Is.EqualTo(2));
        }

        [Test]
        public void Bookie_Adds5Mult_WhenASideBetWins()
        {
            var t = With(V("Bookie"), "K", "9", "Q", "9");
            Play(t, BetLuckyLadies, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 5));
        }

        [Test]
        public void Daredevil_Adds4MultPerHitOn15OrMore()
        {
            var t = With(V("Daredevil"), "10", "10", "5", "8", "4");
            Play(t, Deal, Hit, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 4));
        }

        [Test]
        public void Horseshoe_SometimesSavesABust()
        {
            int saved = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var gambler = new Gambler();
                var shoe = V("Horseshoe");
                var t = new Table(gambler, StackOf("10", "10", "6", "9", "K"), new GameRandom(seed), long.MaxValue, new[] { shoe });
                Play(t, Deal, Hit, Stand);
                if (Score(t).Outcome != Outcome.Bust) saved++;
            }
            Assert.That(saved, Is.InRange(3, 20), "about 1 in 4");
        }

        [Test]
        public void Snowball_GrowsOnWins_AndMeltsOnABust()
        {
            var snow = (Vice)V("Snowball");
            var t = With(snow, "K", "9", "Q", "9", "K", "9", "Q", "9", "10", "10", "6", "9", "K", "K", "9", "Q", "9");
            Play(t, Deal, Stand, Deal, Stand);
            // (20 + 5) for the win, + 7 from the first win
            Assert.That(Score(t).Chips, Is.EqualTo(25 + 7));
            Play(t, Deal, Hit, Stand, Deal, Stand);
            Assert.That(Score(t).Chips, Is.EqualTo(25));
        }

        [Test]
        public void Patience_GrowsOnWinsWithoutAHit()
        {
            var t = With(V("Patience"), "K", "9", "Q", "9", "K", "9", "Q", "9");
            Play(t, Deal, Stand, Deal, Stand);
            // a win's x1 + the table's +1 streak Mult + Patience's +1 from the first win
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 1 + 1));
        }

        [Test]
        public void RiggedShoe_MakesTheDealerHitTo18()
        {
            var t = With(V("Rigged Shoe"), "10", "10", "9", "7", "5");
            Play(t, Deal, Stand);
            Assert.That(t.DealerCards.Count, Is.EqualTo(3));
        }

        [Test]
        public void LuckySevens_MultipliesPerSeven_And3SevensIsX7()
        {
            var two = With(V("Lucky Sevens"), "7", "9", "7", "8", "5");
            Play(two, Deal, Hit, Stand);
            // a win's x1 + the Pair combo's +1, then x1.5 per 7
            Assert.That(Score(two).Mult, Is.EqualTo((1 + 1) * 1.5 * 1.5).Within(0.001));

            var three = With(V("Lucky Sevens"), "7", "9", "7", "8", "7");
            Play(three, Deal, Hit, Stand);
            // a 21's x2 + Three of a Kind's +3, then x7, then the Triple Sevens Feat's x3
            Assert.That(Score(three).Mult, Is.EqualTo((2 + 3) * 7 * 3));
        }

        [Test]
        public void Mirror_CopiesTheViceToItsRight()
        {
            var t = With(new[] { V("Mirror"), V("Lucky Penny") }, "K", "9", "Q", "9");
            Play(t, Deal, Stand);
            // a win's x1 + Lucky Penny's +5, copied
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 5 + 5));
        }

        [Test]
        public void BlackCat_TriplesTheHandAfterABust()
        {
            var t = With(V("Black Cat"), "10", "10", "6", "9", "K", "K", "9", "Q", "9");
            Play(t, Deal, Hit, Stand, Deal, Stand);
            // a win's x1, tripled (the bust broke any streak)
            Assert.That(Score(t).Mult, Is.EqualTo(1 * 3));
        }

        [Test]
        public void AllIn_TriplesTheMult()
        {
            var t = With(V("All In"), "K", "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(1 * 3));
            Assert.That(t.Gambler.Vices[0].BlocksInterest, Is.True);
        }

        [Test]
        public void LastCall_TriplesTheFinalHand()
        {
            // pushes until the final hand, so no win streak builds up, then a win
            var cards = Enumerable.Repeat(new[] { "K", "9", "9", "K" }, Balance.StartingHands - 1).SelectMany(x => x)
                .Concat(new[] { "K", "9", "Q", "9" }).Cast<object>().ToArray();
            var t = With(V("Last Call"), cards);
            for (int i = 0; i < Balance.StartingHands - 1; i++) Play(t, Deal, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Push));
            Assert.That(Score(t).Mult, Is.EqualTo(1));
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(1 * 3));
        }

        [Test]
        public void Twins_DoubleSplitHands()
        {
            // 8-K and 8-K, both 18 beating 17. a diamond K keeps the first hand from being Suited: x1 doubled
            var t = With(V("Twins"), "8", "10", "8", "7", "Kd", "K");
            Play(t, Deal, Split, Stand, Stand);
            Assert.That(t.LastScores.All(s => s.Mult == 1 * 2), Is.True);
        }

        [Test]
        public void ColdRead_LetsYouStopTheDealer()
        {
            // dealer 10 + 6 would draw a 5 to make 21. stop him at 16 instead
            var t = With(V("Cold Read"), "10", "10", "8", "6", "5");
            Play(t, Deal, Stand);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.DealerDecision));
            Assert.That(t.NextCardPreview?.ToString(), Is.EqualTo("5♠"));
            t.Apply(StopDealer);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Win));
            Assert.That(t.Gambler.Heat, Is.EqualTo(Balance.ColdReadHeat));
        }

        [Test]
        public void CardMechanic_SwapsYourLastCardWithTheDealersUpCard()
        {
            var t = With(V("Card Mechanic"), "10", "A", "5", "9");
            Play(t, Deal, DeclineInsurance, SwapWithDealer);
            Assert.That(t.PlayerHands[0].Cards.Last().IsAce, Is.True);
            Assert.That(t.LegalActions(), Has.No.Member(SwapWithDealer), "once per hand");
        }

        [Test]
        public void SmoothTalker_Adds1MultPer3Heat()
        {
            var t = With(V("Smooth Talker"), "K", "9", "Q", "9");
            t.Gambler.AddHeat(20);
            Play(t, Deal, Stand);
            // a win's x1 + 20 Heat / 3
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 6));
        }

        [Test]
        public void KnifesEdge_TriplesAt21_AndHalvesAt18OrLess()
        {
            var t = With(V("Knife's Edge"), "10", "10", "8", "7");
            Play(t, Deal, Stand);
            // a win's x1, halved
            Assert.That(Score(t).Mult, Is.EqualTo(1 * 0.5));
        }

        [Test]
        public void DoubleOrBust_LetsYouDoubleAfterHitting()
        {
            var t = With(V("Double or Bust"), "2", "10", "3", "8", "4");
            Play(t, Deal, Hit);
            Assert.That(t.LegalActions(), Has.Member(DoubleDown));
        }

        [Test]
        public void LoanOfficer_StopsDebtGrowing()
        {
            var g = new Gambler();
            g.Vices.Add(V("Loan Officer"));
            g.Borrow(10, 2);
            g.GrowDebt();
            Assert.That(g.Debt, Is.EqualTo(10));
        }

        [Test]
        public void Echo_ScoresTheFirstCardTwice()
        {
            var t = With(V("Echo"), "K", "9", "Q", "9");
            Play(t, Deal, Stand);
            // (20 + 5) for the win, then the K's 10 again
            Assert.That(Score(t).Chips, Is.EqualTo(25 + 10));
        }

        [Test]
        public void Midas_GildsEveryCardInAWinningHand()
        {
            var t = With(V("Midas"), "K", "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(t.PlayerHands[0].Cards.All(c => c.Enhancement == Enhancement.Gilded), Is.True);
        }

        [Test]
        public void NaturalBorn_BlackjacksBeatDealerBlackjacks_AndGetX5()
        {
            var t = With(V("Natural Born"), "A", "K", "Q", "A");
            t.Apply(Deal);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Blackjack));
            // a blackjack's x3, times 5
            Assert.That(Score(t).Mult, Is.EqualTo(3 * 5));
        }

        [Test]
        public void HighStakes_GrowsWhenYouHitOn17AndSurvive()
        {
            var t = With(V("High Stakes"), "10", "10", "7", "8", "2", "K", "9", "Q", "9");
            Play(t, Deal, Hit, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(1 * 1.25).Within(0.001));
        }

        [Test]
        public void TheDealer_LetsYouStopTheDealerOncePerTable()
        {
            var t = With(V("The Dealer"), "10", "10", "8", "6", "5");
            Play(t, Deal, Stand);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.DealerDecision));
            t.Apply(StopDealer);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void Whale_MultipliesByGoldOver10()
        {
            var t = With(V("Whale"), "K", "9", "Q", "9");
            t.Gambler.GainGold(26);
            Play(t, Deal, Stand);
            // a win's x1, times 30 gold / 10
            Assert.That(Score(t).Mult, Is.EqualTo(1 * 3));
        }

        [Test]
        public void ChainReaction_MultipliesForEveryOtherViceThatTriggered()
        {
            var t = With(new[] { V("Lucky Penny"), V("Even Money"), V("Chain Reaction") }, "K", "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo((1 + 5 + 8) * 1.1 * 1.1).Within(0.001));
        }

        [Test]
        public void ArchetypeSets_UnlockWith3Vices()
        {
            var g = new Gambler();
            g.Vices.Add(V("Espresso"));
            g.Vices.Add(V("Iron Will"));
            Assert.That(ArchetypeBonuses.Has(g, Archetype.Steady), Is.False);
            g.Vices.Add(V("Low Roller"));
            Assert.That(ArchetypeBonuses.Has(g, Archetype.Steady), Is.True);
            var t = new Table(g, new GameRandom(1), 100, g.Vices);
            Assert.That(t.HandsLeft, Is.EqualTo(Balance.StartingHands + 1 + 1), "Espresso and the Steady set");
        }

        [Test]
        public void CheaterSet_HalvesHeat()
        {
            var g = new Gambler();
            g.Vices.Add(V("Card Counter"));
            g.Vices.Add(V("Tell Reader"));
            g.Vices.Add(V("X-Ray Specs"));
            g.AddHeat(10);
            Assert.That(g.Heat, Is.EqualTo(5));
        }
    }
}
