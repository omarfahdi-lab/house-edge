using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    public class FloorMapTests
    {
        [Test]
        public void AfterTheLowTablesShop_YouPickOneOfTwoRooms()
        {
            var run = RunTests.FindRunThatBeatsFirstTable();
            run.Apply(RunInput.LeaveShop());
            Assert.That(run.Phase, Is.EqualTo(RunPhase.RoomChoice));
            Assert.That(run.RoomChoices.Count, Is.EqualTo(2));
            Assert.That(run.RoomChoices.Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void LeavingTheRoom_GoesToTheHighTable()
        {
            var run = RoomRuns.In(RoomKind.LostAndFound);
            run.Apply(RunInput.LeaveRoom());
            Assert.That(run.Phase, Is.EqualTo(RunPhase.AtTable));
            Assert.That(run.Stage, Is.EqualTo(TableKind.High));
        }

        [Test]
        public void LowAndHighTables_HaveADealer_BossTablesDont()
        {
            var run = new Run(1);
            Assert.That(run.Dealer, Is.Not.Null);
            Assert.That(run.Table.Effects, Has.Member(run.Dealer));
            var boss = AutoPlay.FindRunAt(TableKind.Boss);
            Assert.That(boss.Dealer, Is.Null);
        }

        [Test]
        public void ARunWithRooms_ReplaysExactly()
        {
            var run = RoomRuns.In(RoomKind.Bar);
            run.Apply(RunInput.RoomOption(run.RoomOptions().FindIndex(o => o.Kind == RoomActionKind.FreeCocktail)));
            run.Apply(RunInput.LeaveRoom());
            var replay = Run.Replay(run.Seed, run.Inputs);
            Assert.That(replay.Stage, Is.EqualTo(run.Stage));
            Assert.That(replay.Gambler.Cocktails.Select(c => c.Kind), Is.EqualTo(run.Gambler.Cocktails.Select(c => c.Kind)));
            Assert.That(replay.Dealer.Name, Is.EqualTo(run.Dealer.Name));
        }
    }

    public class RoomTests
    {
        private static int Option(Run run, RoomActionKind kind) => run.RoomOptions().FindIndex(o => o.Kind == kind);

        [Test]
        public void Bar_SellsCocktailsAtHalfPrice()
        {
            var run = RoomRuns.In(RoomKind.Bar, gold: 10);
            int buy = Option(run, RoomActionKind.BuyCocktail);
            run.Apply(RunInput.RoomOption(buy));
            Assert.That(run.Gambler.Gold, Is.EqualTo(10 - Balance.BarCocktailPrice));
            Assert.That(run.Gambler.Cocktails.Count, Is.EqualTo(1));
            Assert.That(run.IsLegal(RunInput.RoomOption(Option(run, RoomActionKind.FreeCocktail))), Is.False, "half price or free, not both");
        }

        [Test]
        public void Bar_OrGivesAFreeCocktail()
        {
            var run = RoomRuns.In(RoomKind.Bar, gold: 0);
            run.Apply(RunInput.RoomOption(Option(run, RoomActionKind.FreeCocktail)));
            Assert.That(run.Gambler.Cocktails.Count, Is.EqualTo(1));
            Assert.That(run.Room.Used, Is.True);
        }

        [Test]
        public void BackRoom_PlaysOneHand_ThenDoublesOrTakesYourGold()
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var run = RoomRuns.In(RoomKind.BackRoom, gold: 10, firstSeed: 1 + attempt * 7);
                run.Apply(RunInput.RoomOption(Option(run, RoomActionKind.PlayBackRoom)));
                Assert.That(run.ActiveTable, Is.Not.SameAs(run.Table));
                Assert.That(run.IsLegal(RunInput.LeaveRoom()), Is.False, "finish the hand first");
                while (!run.Room.Used)
                {
                    var legal = run.ActiveTable.LegalActions();
                    var action = legal.Contains(Deal) ? Deal : legal.Contains(DeclineInsurance) ? DeclineInsurance : Stand;
                    run.Apply(RunInput.Table(action));
                }
                var outcome = run.Room.BackRoomTable.LastScores[0].Outcome;
                if (outcome.IsWin()) Assert.That(run.Gambler.Gold, Is.EqualTo(20));
                else if (outcome == Outcome.Lose || outcome == Outcome.Bust) Assert.That(run.Gambler.Gold, Is.EqualTo(0));
                else Assert.That(run.Gambler.Gold, Is.EqualTo(10));
                Assert.That(run.IsLegal(RunInput.LeaveRoom()), Is.True);
                if (outcome.IsWin()) return;
            }
        }

        [Test]
        public void PawnShop_BuysVicesAtFullPrice()
        {
            var run = RoomRuns.In(RoomKind.PawnShop, gold: 0);
            run.Gambler.Vices.Add(ViceCatalog.Get("SoftTouch").Create());
            run.Apply(RunInput.RoomOption(Option(run, RoomActionKind.SellViceFull)));
            Assert.That(run.Gambler.Gold, Is.EqualTo(Balance.VicePrice(Rarity.Uncommon)));
            Assert.That(run.Gambler.Vices, Is.Empty);
        }

        [Test]
        public void PawnShop_SellsAUsedVice_ThatMightBeCursed()
        {
            var run = RoomRuns.In(RoomKind.PawnShop, gold: 20);
            run.Apply(RunInput.RoomOption(Option(run, RoomActionKind.BuyUsedRare)));
            Assert.That(run.Gambler.Vices.Count, Is.EqualTo(1));
            Assert.That(run.Gambler.Gold, Is.EqualTo(20 - Balance.UsedRarePrice));
        }

        [Test]
        public void LoanShark_LendsGold_AndTheDebtGrows50PercentPerTable()
        {
            var run = RoomRuns.In(RoomKind.LoanShark, gold: 0);
            run.Apply(RunInput.RoomOption(Option(run, RoomActionKind.Borrow)));
            Assert.That(run.Gambler.Gold, Is.EqualTo(10));
            Assert.That(run.Gambler.Debt, Is.EqualTo(10));
            Assert.That(run.Gambler.DebtDueFloor, Is.EqualTo(run.Floor + 1));
            run.Gambler.GrowDebt();
            Assert.That(run.Gambler.Debt, Is.EqualTo(15));
        }

        [Test]
        public void RepayingDebt_PaysWhatYouCan()
        {
            var g = new Gambler(gold: 7);
            g.Borrow(10, 2);
            g.RepayDebt();
            Assert.That(g.Debt, Is.EqualTo(0));
            Assert.That(g.Gold, Is.EqualTo(7));

            var poor = new Gambler(gold: 0);
            poor.Borrow(10, 2);
            poor.LoseGold(7);
            poor.RepayDebt();
            Assert.That(poor.Debt, Is.EqualTo(7));
        }

        [Test]
        public void CardRoom_RemovesDuplicatesOrEnhancesOneCard()
        {
            var remove = RoomRuns.In(RoomKind.CardRoom);
            int before = remove.Gambler.Deck.Count;
            remove.Apply(RunInput.RoomOption(Option(remove, RoomActionKind.RemoveCard), 0));
            Assert.That(remove.Gambler.Deck.Count, Is.EqualTo(before - 1));
            Assert.That(remove.IsLegal(RunInput.RoomOption(Option(remove, RoomActionKind.DuplicateCard), 0)), Is.False, "one service per visit");

            var enhance = RoomRuns.In(RoomKind.CardRoom);
            enhance.Apply(RunInput.RoomOption(Option(enhance, RoomActionKind.EnhanceCard), 3));
            Assert.That(enhance.Gambler.Deck[3].Enhancement, Is.Not.EqualTo(Enhancement.None));
        }

        [Test]
        public void CardRoom_NeedsACardPicked()
        {
            var run = RoomRuns.In(RoomKind.CardRoom);
            Assert.That(run.IsLegal(RunInput.RoomOption(Option(run, RoomActionKind.RemoveCard))), Is.False);
        }

        [Test]
        public void LostAndFound_GivesAFreeVice()
        {
            var run = RoomRuns.In(RoomKind.LostAndFound);
            run.Apply(RunInput.RoomOption(Option(run, RoomActionKind.TakeLostVice)));
            Assert.That(run.Gambler.Vices.Count, Is.EqualTo(1));
        }

        [Test]
        public void SecurityOffice_Clears10HeatPer2Gold()
        {
            var run = RoomRuns.In(RoomKind.SecurityOffice, gold: 4);
            run.Gambler.AddHeat(35);
            run.Apply(RunInput.RoomOption(Option(run, RoomActionKind.PayOffHeat)));
            Assert.That(run.Gambler.Heat, Is.EqualTo(15));
            Assert.That(run.Gambler.Gold, Is.EqualTo(0));
        }

        [Test]
        public void HighLimitLounge_SkipsTheNextTable_AndRaisesTheOneAfter()
        {
            var run = RoomRuns.In(RoomKind.HighLimitLounge);
            Assert.That(run.Stage, Is.EqualTo(TableKind.Low));
            run.Apply(RunInput.RoomOption(Option(run, RoomActionKind.SkipNextTable)));
            run.Apply(RunInput.LeaveRoom());
            Assert.That(run.Phase, Is.EqualTo(RunPhase.RoomChoice), "straight to the next room");
            Assert.That(run.Stage, Is.EqualTo(TableKind.High));
            AutoPlay.Advance(run);
            AutoPlay.Advance(run);
            Assert.That(run.Stage, Is.EqualTo(TableKind.Boss));
            long expected = (long)System.Math.Round(Balance.Target(run.Floor, TableKind.Boss) * run.Boss.TargetFactor * Balance.LoungeTargetFactor);
            Assert.That(run.Table.Target, Is.EqualTo(expected));
        }

        [Test]
        public void UnpaidDebt_SendsTheEnforcer_AtTheEndOfTheDueFloor()
        {
            Run run = null;
            for (int seed = 1; run == null || run.Phase == RunPhase.Lost; seed = run.Seed + 1)
            {
                run = AutoPlay.FindRunAt(TableKind.Boss, seed);
                run.Gambler.Borrow(10, run.Floor);
                AutoPlay.UntilThisTableEnds(run);
            }
            while (run.Phase != RunPhase.Shop) AutoPlay.Advance(run);
            run.Gambler.LoseGold(run.Gambler.Gold);
            run.Apply(RunInput.LeaveShop());
            Assert.That(run.AtEnforcer, Is.True);
            Assert.That(run.Boss.Name, Is.EqualTo("The Enforcer"));
        }
    }

    public class DealerTellTests
    {
        [Test]
        public void RookieRay_FidgetsOverATen_AndSometimesHits17()
        {
            var ray = new RookieRay();
            var t = new Table(Stack("9", "5", "9", "K"), long.MaxValue, new TableEffect[] { ray });
            t.Apply(Deal);
            Assert.That(ray.Tell(t), Is.EqualTo("Ray is fidgeting."));
            Assert.That(ray.TellMeaning(t), Does.Contain("10"));
            Assert.That(t.Rules.DealerMistakeOneIn, Is.EqualTo(5));
        }

        [Test]
        public void SilentSofia_HitsSoft17()
        {
            var t = new Table(Stack("10", "A", "9", "6", "2"), long.MaxValue, new TableEffect[] { new SilentSofia() });
            Play(t, Deal, DeclineInsurance, Stand);
            Assert.That(t.DealerCards.Count, Is.EqualTo(3));
        }

        [Test]
        public void ChattyChen_GivesAwayTheHoleCardsColor()
        {
            var chen = new ChattyChen();
            var t = new Table(Stack("9", "5", "9", "Kh"), long.MaxValue, new TableEffect[] { chen });
            t.Apply(Deal);
            Assert.That(chen.TellMeaning(t), Is.EqualTo("His hole card is red."));
        }

        [Test]
        public void OldPete_GivesAnExtraBurn()
        {
            var t = new Table(Stack("9"), long.MaxValue, new TableEffect[] { new OldPete() });
            Assert.That(t.BurnsLeft, Is.EqualTo(Balance.StartingBurns + 1));
        }

        [Test]
        public void VegasVal_LetsYouWinPushes()
        {
            var t = new Table(Stack("10", "10", "8", "8"), long.MaxValue, new TableEffect[] { new VegasVal() });
            Play(t, Deal, Stand);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void LuckyLou_TapsForAnAce()
        {
            var lou = new LuckyLou();
            var t = new Table(Stack("9", "5", "9", "A"), long.MaxValue, new TableEffect[] { lou });
            t.Apply(Deal);
            Assert.That(lou.Tell(t), Is.EqualTo("Lou taps the table."));
            Assert.That(t.Rules.ReshuffleAfterDealerBust, Is.True);
        }
    }

    public class CursedViceTests
    {
        private static Vice V(string id) => ViceCatalog.Get(id).Create();

        [Test]
        public void CursedVices_SellFor1()
        {
            var run = new Run(1);
            Assert.That(run.SellValue(V("DirtyMoney")), Is.EqualTo(1));
        }

        [Test]
        public void DirtyMoney_Adds12Mult_And5HeatPerWin()
        {
            var t = new Table(Stack("K", "9", "Q", "9"), long.MaxValue, new[] { V("DirtyMoney") });
            Play(t, Deal, Stand);
            Assert.That(t.LastScores.Single().Mult, Is.EqualTo(14));
            Assert.That(t.Gambler.Heat, Is.EqualTo(5));
        }

        [Test]
        public void DevilsDeal_TriplesTheMult_ButTakesAHand()
        {
            var t = new Table(Stack("K", "9", "Q", "9"), long.MaxValue, new[] { V("DevilsDeal") });
            Assert.That(t.HandsLeft, Is.EqualTo(Balance.StartingHands - 1));
            Play(t, Deal, Stand);
            Assert.That(t.LastScores.Single().Mult, Is.EqualTo(6));
        }

        [Test]
        public void BadHabit_MakesYouHitBeforeStanding()
        {
            var t = new Table(Stack("K", "9", "Q", "9", "A"), long.MaxValue, new[] { V("BadHabit") });
            t.Apply(Deal);
            Assert.That(t.LegalActions(), Has.No.Member(Stand));
            t.Apply(Hit);
            Assert.That(t.LegalActions(), Has.Member(Stand));
        }

        [Test]
        public void BorrowedTime_Adds2Hands_ThatCost2GoldEach()
        {
            var cards = Enumerable.Repeat(new[] { "10", "10", "7", "9" }, Balance.StartingHands + 2).SelectMany(x => x).ToArray();
            var t = new Table(Stack(cards), long.MaxValue, new[] { V("BorrowedTime") }, gold: 10);
            Assert.That(t.HandsLeft, Is.EqualTo(Balance.StartingHands + 2));
            for (int i = 0; i < Balance.StartingHands + 2; i++) Play(t, Deal, Stand);
            Assert.That(t.Gold, Is.EqualTo(6), "the last 2 Hands cost 2 gold each");
        }
    }

    internal static class RoomRuns
    {
        // a run standing in a room of this kind on floor 1, with exactly this much gold
        public static Run In(RoomKind kind, int gold = 10, int firstSeed = 1)
        {
            for (int seed = firstSeed; seed < firstSeed + 500; seed++)
            {
                var run = RunTests.FindRunThatBeatsFirstTableFrom(seed);
                if (run == null) break;
                run.Apply(RunInput.LeaveShop());
                int index = run.RoomChoices.ToList().IndexOf(kind);
                if (index < 0)
                {
                    seed = run.Seed;
                    continue;
                }
                run.Apply(RunInput.ChooseRoom(index));
                run.Gambler.LoseGold(run.Gambler.Gold);
                run.Gambler.GainGold(gold);
                return run;
            }
            Assert.Fail($"no seed offered {kind}");
            return null;
        }
    }
}
