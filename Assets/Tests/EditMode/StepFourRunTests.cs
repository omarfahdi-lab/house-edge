using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    public class HeatTests
    {
        [TestCase(0, HeatLevel.Clean)]
        [TestCase(24, HeatLevel.Clean)]
        [TestCase(25, HeatLevel.Watched)]
        [TestCase(50, HeatLevel.Suspected)]
        [TestCase(75, HeatLevel.Flagged)]
        [TestCase(100, HeatLevel.Busted)]
        public void HeatLevels_FollowTheDesignTable(int heat, HeatLevel level)
        {
            Assert.That(Balance.HeatLevelFor(heat), Is.EqualTo(level));
        }

        [Test]
        public void Heat_IsCappedAt100()
        {
            var g = new Gambler();
            g.AddHeat(250);
            Assert.That(g.Heat, Is.EqualTo(100));
        }

        [Test]
        public void ScoringOver3xTheTargetInOneHand_Adds10Heat()
        {
            // blackjack scores 204 against a target of 50
            var t = new Table(Stack("A", "9", "K", "8"), 50);
            t.Apply(Deal);
            Assert.That(t.Gambler.Heat, Is.EqualTo(Balance.BigHandHeat));
        }

        [Test]
        public void Watched_MakesTheDealerReshuffleEvery2Hands()
        {
            var g = new Gambler();
            g.AddHeat(30);
            var t = new Table(g, new GameRandom(1), long.MaxValue);
            Assert.That(t.Rules.ReshuffleEveryDeals, Is.EqualTo(2));
        }

        [Test]
        public void Suspected_RaisesShopPrices25Percent()
        {
            var g = new Gambler();
            Assert.That(Shop.HeatAdjusted(4, g), Is.EqualTo(4));
            g.AddHeat(55);
            Assert.That(Shop.HeatAdjusted(4, g), Is.EqualTo(5));
            Assert.That(Shop.HeatAdjusted(8, g), Is.EqualTo(10));
        }

        [Test]
        public void HeatDrops10_AfterEachTable()
        {
            var run = new Run(1);
            run.Gambler.AddHeat(30);
            AutoPlay.UntilTableEnds(run);
            if (run.Phase != RunPhase.Shop) Assert.Inconclusive("seed 1 lost its first table");
            Assert.That(run.Gambler.Heat, Is.EqualTo(20));
        }

        [Test]
        public void At100Heat_SecurityTakesYouToTheEyeInTheSky()
        {
            Run run = null;
            for (int seed = 1; seed < 200 && (run == null || run.Phase == RunPhase.Lost); seed++)
            {
                run = new Run(seed);
                run.Gambler.AddHeat(100);
                AutoPlay.UntilThisTableEnds(run);
            }
            Assert.That(run.AtEyeInTheSky, Is.True);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.AtTable));
            Assert.That(run.Boss.Name, Is.EqualTo("The Eye in the Sky"));
            Assert.That(run.Table.Target, Is.EqualTo(Balance.Target(1, TableKind.Low) * 2));
        }
    }

    public class CocktailTests
    {
        private static Run ShopWith(CocktailKind kind)
        {
            var run = RunTests.FindRunThatBeatsFirstTable();
            run.Gambler.Cocktails.Add(new Cocktail(kind));
            return run;
        }

        private static int Slot(Run run) => run.Gambler.Cocktails.Count - 1;

        [Test]
        public void OldFashioned_GildsUpTo2Cards()
        {
            var run = ShopWith(CocktailKind.OldFashioned);
            run.Apply(RunInput.UseCocktail(Slot(run), 0, 1));
            Assert.That(run.Gambler.Deck[0].Enhancement, Is.EqualTo(Enhancement.Gilded));
            Assert.That(run.Gambler.Deck[1].Enhancement, Is.EqualTo(Enhancement.Gilded));
            Assert.That(run.Gambler.Cocktails, Is.Empty, "used up");
        }

        [Test]
        public void Martini_DuplicatesACard()
        {
            var run = ShopWith(CocktailKind.Martini);
            int before = run.Gambler.Deck.Count;
            var card = run.Gambler.Deck[5];
            run.Apply(RunInput.UseCocktail(Slot(run), 5));
            Assert.That(run.Gambler.Deck.Count, Is.EqualTo(before + 1));
            Assert.That(run.Gambler.Deck.Last().ToString(), Is.EqualTo(card.ToString()));
        }

        [Test]
        public void Screwdriver_RemovesUpTo2Cards()
        {
            var run = ShopWith(CocktailKind.Screwdriver);
            int before = run.Gambler.Deck.Count;
            run.Apply(RunInput.UseCocktail(Slot(run), 0, 1));
            Assert.That(run.Gambler.Deck.Count, Is.EqualTo(before - 2));
        }

        [Test]
        public void LongIslandAndLastWord_MoveACardsRank()
        {
            var run = ShopWith(CocktailKind.LongIsland);
            int index = run.Gambler.Deck.FindIndex(c => c.Rank == Rank.Five);
            run.Apply(RunInput.UseCocktail(Slot(run), index));
            Assert.That(run.Gambler.Deck[index].Rank, Is.EqualTo(Rank.Six));

            run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.LastWord));
            run.Apply(RunInput.UseCocktail(Slot(run), index));
            run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.LastWord));
            run.Apply(RunInput.UseCocktail(Slot(run), index));
            Assert.That(run.Gambler.Deck[index].Rank, Is.EqualTo(Rank.Four));
        }

        [Test]
        public void MojitoChampagneAndDarkAndStormy_EnhanceACard()
        {
            var run = ShopWith(CocktailKind.Mojito);
            run.Apply(RunInput.UseCocktail(Slot(run), 0));
            run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.Champagne));
            run.Apply(RunInput.UseCocktail(Slot(run), 1));
            run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.DarkAndStormy));
            run.Apply(RunInput.UseCocktail(Slot(run), 2));
            Assert.That(run.Gambler.Deck[0].Enhancement, Is.EqualTo(Enhancement.Flex));
            Assert.That(run.Gambler.Deck[1].Enhancement, Is.EqualTo(Enhancement.Fragile));
            Assert.That(run.Gambler.Deck[2].Stamp, Is.EqualTo(Stamp.Red));
        }

        [Test]
        public void DeckCocktails_OnlyWorkBetweenTables()
        {
            var run = new Run(1);
            run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.Mojito));
            Assert.That(run.IsLegal(RunInput.UseCocktail(0, 0)), Is.False);
        }

        [Test]
        public void IceWater_Adds2Burns_AtATable()
        {
            var run = new Run(1);
            run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.IceWater));
            run.Apply(RunInput.UseCocktail(0));
            Assert.That(run.Table.BurnsLeft, Is.EqualTo(Balance.StartingBurns + 2));
        }

        [Test]
        public void WhiskeySour_Pays2GoldPerVice()
        {
            var run = ShopWith(CocktailKind.WhiskeySour);
            run.Gambler.Vices.Add(ViceCatalog.Get("LuckyPenny").Create());
            run.Gambler.Vices.Add(ViceCatalog.Get("EvenMoney").Create());
            int gold = run.Gambler.Gold;
            run.Apply(RunInput.UseCocktail(Slot(run)));
            Assert.That(run.Gambler.Gold, Is.EqualTo(gold + 4));
        }

        [Test]
        public void ColdBrew_Lowers25Heat()
        {
            var run = ShopWith(CocktailKind.ColdBrew);
            run.Gambler.AddHeat(40);
            run.Apply(RunInput.UseCocktail(Slot(run)));
            Assert.That(run.Gambler.Heat, Is.EqualTo(15));
        }

        [Test]
        public void WildCard_CreatesAVice_WhenThereIsRoom()
        {
            var run = ShopWith(CocktailKind.WildCard);
            run.Apply(RunInput.UseCocktail(Slot(run)));
            Assert.That(run.Gambler.Vices.Count, Is.EqualTo(1));

            run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.WildCard));
            run.Gambler.ViceSlots = 1;
            Assert.That(run.IsLegal(RunInput.UseCocktail(Slot(run))), Is.False);
        }

        [Test]
        public void YouCanHoldAtMost2Cocktails()
        {
            var run = RunTests.FindRunThatBeatsFirstTable();
            run.Gambler.GainGold(100);
            run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.ColdBrew));
            run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.ColdBrew));
            int offer = run.Shop.Offers.ToList().FindIndex(o => o.Kind == OfferKind.Cocktail);
            Assert.That(run.IsLegal(RunInput.Buy(offer)), Is.False);
        }
    }

    public class CompTests
    {
        [Test]
        public void EachCompLevel_AddsItsChipsAndMult()
        {
            var t = new Table(Stack("K", "9", "Q", "9"), long.MaxValue);
            t.Gambler.LevelUp(Comp.Win);
            t.Gambler.LevelUp(Comp.Win);
            Play(t, Deal, Stand);
            Assert.That(t.LastScores.Single().Chips, Is.EqualTo(30 + 20));
            Assert.That(t.LastScores.Single().Mult, Is.EqualTo(2 + 2));
        }

        [Test]
        public void Comps_OnlyApplyToTheirOutcome()
        {
            var t = new Table(Stack("K", "9", "Q", "9"), long.MaxValue);
            t.Gambler.LevelUp(Comp.Blackjack);
            Play(t, Deal, Stand);
            Assert.That(t.LastScores.Single().Chips, Is.EqualTo(30));
        }

        [Test]
        public void BuyingAComp_LevelsItUp()
        {
            var run = RunTests.FindRunThatBeatsFirstTable();
            run.Gambler.GainGold(100);
            int offer = run.Shop.Offers.ToList().FindIndex(o => o.Kind == OfferKind.Comp);
            var comp = run.Shop.Offers[offer].Comp;
            run.Apply(RunInput.Buy(offer));
            Assert.That(run.Gambler.CompLevel(comp), Is.EqualTo(1));
        }
    }

    public class BossRunTests
    {
        [Test]
        public void EveryFloorHasAPitBoss_AndFloor8IsTheHouse()
        {
            var run = new Run(3);
            Assert.That(Bosses.PitBosses, Has.Member(run.FloorBoss));
        }

        [Test]
        public void BeatingAPitBoss_OffersRareOrLegendaryVices()
        {
            Run run = null;
            for (int seed = 1; seed < 400 && (run == null || run.Phase == RunPhase.Lost); seed = run.Seed + 1)
            {
                run = AutoPlay.FindRunAt(TableKind.Boss, seed);
                AutoPlay.UntilTableEnds(run);
            }
            Assert.That(run.Phase, Is.EqualTo(RunPhase.BossReward));
            Assert.That(run.Rewards.Count, Is.GreaterThan(0));
            run.Apply(RunInput.SkipReward());
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Shop));
        }

        [Test]
        public void BossTables_HaveTheirBossAsAnEffect()
        {
            var run = AutoPlay.FindRunAt(TableKind.Boss);
            Assert.That(run.Boss, Is.Not.Null);
            Assert.That(run.Table.Effects, Has.Member(run.Boss));
            Assert.That(run.Boss.Definition, Is.SameAs(run.FloorBoss));
        }
    }

    // a simple fixed strategy for driving runs in tests
    internal static class AutoPlay
    {
        public static void Step(Run run)
        {
            var t = run.Table;
            var legal = t.LegalActions();
            PlayerAction action;
            if (legal.Contains(Deal)) action = Deal;
            else if (legal.Contains(DeclineInsurance)) action = DeclineInsurance;
            else if (legal.Contains(LetDealerDraw)) action = LetDealerDraw;
            else if (legal.Contains(Burn) && t.ActiveHand.Value.IsBust) action = Burn;
            else if (t.ActiveHand.Value.Total < 17 && legal.Contains(Hit)) action = Hit;
            else action = Stand;
            run.Apply(RunInput.Table(action));
        }

        // one step through anything that isn't a table: skip rewards, leave shops, walk through a quiet room
        public static void Advance(Run run)
        {
            switch (run.Phase)
            {
                case RunPhase.BossReward: run.Apply(RunInput.SkipReward()); break;
                case RunPhase.Shop: run.Apply(RunInput.LeaveShop()); break;
                case RunPhase.RoomChoice: run.Apply(RunInput.ChooseRoom(QuietRoom(run))); break;
                case RunPhase.InRoom: run.Apply(RunInput.LeaveRoom()); break;
            }
        }

        // a room that changes nothing if you just walk out
        private static int QuietRoom(Run run)
        {
            for (int i = 0; i < run.RoomChoices.Count; i++)
                if (run.RoomChoices[i] != RoomKind.HighLimitLounge) return i;
            return 0;
        }

        // from a shop to the next table, through the room on the way
        public static void ToNextTable(Run run)
        {
            var table = run.Table;
            while (!run.IsOver && (run.Phase != RunPhase.AtTable || run.Table == table)) Advance(run);
        }

        public static void UntilTableEnds(Run run)
        {
            while (run.Phase == RunPhase.AtTable) Step(run);
        }

        // stops even if the run goes straight on to another table (The Eye in the Sky)
        public static void UntilThisTableEnds(Run run)
        {
            var table = run.Table;
            while (run.Phase == RunPhase.AtTable && run.Table == table) Step(run);
        }

        // a run standing at a table of this kind, found by trying seeds and topping up gold to buy nothing
        public static Run FindRunAt(TableKind kind, int firstSeed = 1)
        {
            for (int seed = firstSeed; seed < firstSeed + 400; seed++)
            {
                var run = new Run(seed);
                while (!run.IsOver && !(run.Phase == RunPhase.AtTable && run.Stage == kind && run.Table.DealsPlayed == 0))
                {
                    if (run.Phase == RunPhase.AtTable) Step(run);
                    else Advance(run);
                }
                if (!run.IsOver) return run;
            }
            Assert.Fail($"no seed reached a {kind} table");
            return null;
        }
    }
}
