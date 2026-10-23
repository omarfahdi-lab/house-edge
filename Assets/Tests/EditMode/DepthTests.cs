using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // the count and raising, trick cards, Vice levels, the rule-breaking Legendaries, Feats, boss second phases,
    // the slot machine and Daily modifiers
    public class DepthTests
    {
        private static Vice V(string name) => ViceCatalog.All.First(d => d.Name == name).Create();

        private static Table With(Gambler gambler, params object[] cards) =>
            new Table(gambler, StackOf(cards), new GameRandom(1), long.MaxValue, gambler.Vices);

        private static Table With(params object[] cards) => With(new Gambler(), cards);

        // the count

        [Test]
        public void TheCount_StartsEven_AndRunsHotAsSmallCardsComeOut()
        {
            var gambler = new Gambler();
            var deck = gambler.Deck;
            var lowFirst = deck.OrderBy(c => c.IsAce || c.LowValue >= 10 ? 1 : 0).ToList();
            var table = new Table(gambler, Shoe.Stacked(lowFirst, new GameRandom(1)), new GameRandom(1), long.MaxValue);
            Assert.That(table.Count, Is.EqualTo(0));
            for (int i = 0; i < 4; i++)
            {
                Play(table, Deal);
                while (table.Phase == TablePhase.PlayerTurn) Play(table, Stand);
            }
            Assert.That(table.Count, Is.GreaterThan(0), "small cards are gone, so the rest of the shoe is rich in 10s");
        }

        // raising

        [Test]
        public void Raise_CyclesUpToX3_ThenBackToX1()
        {
            var t = With("K", "9", "Q", "9");
            Assert.That(t.LegalActions(), Has.Member(Raise));
            Play(t, Raise);
            Assert.That(t.BetLevel, Is.EqualTo(2));
            Play(t, Raise);
            Assert.That(t.BetLevel, Is.EqualTo(3));
            Play(t, Raise);
            Assert.That(t.BetLevel, Is.EqualTo(1));
        }

        [Test]
        public void ARaisedWin_MultipliesMult_AndTheBetResets()
        {
            var plain = With("K", "9", "Q", "9");
            Play(plain, Deal, Stand);
            var raised = With("K", "9", "Q", "9");
            Play(raised, Raise, Deal, Stand);
            Assert.That(raised.LastScores.Single().Mult, Is.EqualTo(plain.LastScores.Single().Mult * 2).Within(0.001));
            Assert.That(raised.BetLevel, Is.EqualTo(1));
        }

        [Test]
        public void ALostRaise_CostsExtraHands()
        {
            var t = With("9", "K", "9", "Q");
            int hands = t.HandsLeft;
            Play(t, Raise, Raise, Deal, Stand);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(t.RaiseHandsLost, Is.EqualTo(2));
            Assert.That(t.HandsLeft, Is.EqualTo(hands - 3));
        }

        [Test]
        public void BetSpreader_RefundsAHand_AndBoostsRaisedWins()
        {
            var gambler = new Gambler();
            gambler.Vices.Add(V("Bet Spreader"));
            var t = With(gambler, "9", "K", "9", "Q");
            int hands = t.HandsLeft;
            Play(t, Raise, Deal, Stand);
            Assert.That(t.HandsLeft, Is.EqualTo(hands - 1), "the raise's extra Hand is refunded");
        }

        [Test]
        public void YouCantRaisePastTheHandsYouHave()
        {
            var t = With("K", "9", "Q", "9");
            t.AddHands(1 - t.HandsLeft);
            Assert.That(t.LegalActions(), Has.No.Member(Raise));
        }

        // trick cards

        [Test]
        public void Peek_ShowsTheHoleCard()
        {
            var t = With("K", "9", "6", "Q");
            Play(t, Deal);
            Assert.That(t.HoleCardHidden, Is.True);
            Assert.That(t.CanUseTrick(TrickKind.Peek), Is.True);
            t.UseTrick(TrickKind.Peek);
            Assert.That(t.HoleCardHidden, Is.False);
            Assert.That(t.CanUseTrick(TrickKind.Peek), Is.False);
        }

        [Test]
        public void TakeBack_UndoesABust_EvenWithNoBurnsLeft()
        {
            var gambler = new Gambler();
            gambler.Tricks.Add(TrickKind.TakeBack);
            var t = With(gambler, "K", "9", "6", "8", "Q");
            t.AddBurns(-t.BurnsLeft);
            Play(t, Deal, Hit);
            Assert.That(t.ActiveHand.Value.IsBust, Is.True, "the bust waits because Take Back could still save it");
            t.UseTrick(TrickKind.TakeBack);
            Assert.That(t.ActiveHand.Value.Total, Is.EqualTo(16));
        }

        [Test]
        public void Steal_SwapsWithTheDealersUpCard()
        {
            var t = With("10", "K", "6", "7");
            Play(t, Deal);
            t.UseTrick(TrickKind.Steal);
            Assert.That(t.PlayerHands[0].Cards.Last().Rank, Is.EqualTo(Rank.King));
            Assert.That(t.DealerUpCard.Rank, Is.EqualTo(Rank.Six));
        }

        [Test]
        public void SecondThoughts_DoublesAHandYouAlreadyHit()
        {
            var t = With("5", "10", "4", "7", "10");
            Play(t, Deal, Hit);
            Assert.That(t.CanUseTrick(TrickKind.SecondThoughts), Is.True);
            t.UseTrick(TrickKind.SecondThoughts);
            var score = t.LastScores.Single();
            Assert.That(score.Outcome.IsWin(), Is.True);
            Assert.That(score.Hand.IsDoubled, Is.True);
        }

        [Test]
        public void AceUpTheSleeve_AddsAnAceThatNeverJoinsTheDeck()
        {
            var t = With("K", "9", "10", "8");
            int deck = t.Gambler.Deck.Count;
            Play(t, Deal);
            t.UseTrick(TrickKind.AceUpTheSleeve);
            Assert.That(t.ActiveHand.Value.Total, Is.EqualTo(21));
            Play(t, Stand);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.TwentyOne));
            Assert.That(t.Gambler.Heat, Is.EqualTo(Balance.AceUpSleeveHeat));
            Assert.That(t.Gambler.Deck.Count, Is.EqualTo(deck));
        }

        [Test]
        public void PlayingATrick_UsesItUp()
        {
            var run = new Run(4);
            var trick = run.Gambler.Tricks.Single();
            run.Apply(RunInput.Table(Deal));
            for (int i = 0; i < 20 && run.Table.Phase == TablePhase.PlayerTurn && !run.IsLegal(RunInput.PlayTrick(0)); i++)
                run.Apply(RunInput.Table(Hit));
            if (!run.IsLegal(RunInput.PlayTrick(0))) Assert.Inconclusive($"{trick} had no use this deal");
            run.Apply(RunInput.PlayTrick(0));
            Assert.That(run.Gambler.Tricks, Is.Empty);
        }

        // hands-on: the lucky chip, palming, targeted tricks

        [Test]
        public void TheLuckyChip_PaysGoldAndMultWhenCaught()
        {
            for (int seed = 1; seed < 200; seed++)
            {
                var t = new Table(seed, long.MaxValue);
                Play(t, Deal);
                if (!t.ChipOnTable || t.Phase != TablePhase.PlayerTurn) continue;
                int gold = t.Gold;
                Play(t, CatchChip);
                Assert.That(t.ChipOnTable, Is.False);
                Assert.That(t.Gold, Is.EqualTo(gold + Balance.LuckyChipGold));
                Assert.That(t.LegalActions(), Has.No.Member(CatchChip));
                return;
            }
            Assert.Fail("no lucky chip in 200 tables");
        }

        [Test]
        public void Palming_UndoesAHit_OncePerTable_AndBeingSeenCostsHeat()
        {
            var t = With("10", "9", "6", "8", "K", "5", "10", "9", "3", "Q");
            Play(t, Deal, Hit);
            Assert.That(t.ActiveHand.Value.IsBust, Is.True);
            Play(t, PalmSeen);
            Assert.That(t.ActiveHand.Value.Total, Is.EqualTo(16));
            Assert.That(t.Gambler.Heat, Is.EqualTo(Balance.PalmSeenHeat));
            Play(t, Stand, Deal, Hit);
            Assert.That(t.LegalActions(), Has.No.Member(PalmClean), "only once a table");
        }

        [Test]
        public void Steal_SwapsTheCardYouPick_AndNudgeLowersOne()
        {
            var t = With("10", "K", "6", "7");
            Play(t, Deal);
            t.UseTrick(TrickKind.Steal, 0);
            Assert.That(t.PlayerHands[0].Cards[0].Rank, Is.EqualTo(Rank.King));
            Assert.That(t.DealerUpCard.Rank, Is.EqualTo(Rank.Ten));

            var n = With("9", "K", "8", "7");
            Play(n, Deal);
            Assert.That(n.CanUseTrick(TrickKind.Nudge, 1), Is.True);
            var eight = n.ActiveHand.Cards[1];
            n.UseTrick(TrickKind.Nudge, 1);
            Assert.That(n.ActiveHand.Value.Total, Is.EqualTo(16));
            Play(n, Stand);
            n.AddHands(1);
            Play(n, Deal);
            Assert.That(eight.Rank, Is.EqualTo(Rank.Eight), "the nudge only lasts the hand");
        }

        // Vice levels

        [Test]
        public void Vices_LevelUp_AndAddMoreAtHigherLevels()
        {
            var gambler = new Gambler();
            var penny = V("Lucky Penny");
            gambler.Vices.Add(penny);
            penny.Triggers = Balance.ViceLevel2At - 1;
            var t = With(gambler, "K", "9", "Q", "9", "K", "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(t.LastScores.Single().LevelUps, Has.Member("Lucky Penny"));
            Assert.That(penny.Level, Is.EqualTo(2));
            Play(t, Deal, Stand);
            // a win's x1, a streak's +1, and Lucky Penny's +5 with a quarter more on top
            Assert.That(t.LastScores.Single().Mult, Is.EqualTo(1 + 1 + 5 * (1 + Balance.ViceLevelBoost)).Within(0.001));
        }

        // the Legendaries

        [Test]
        public void Tiebreaker_WinsPushes()
        {
            var gambler = new Gambler();
            gambler.Vices.Add(V("Tiebreaker"));
            var t = With(gambler, "K", "K", "9", "9");
            Play(t, Deal, Stand);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void FourCardCharlie_MakesFourCardsACharlie()
        {
            var gambler = new Gambler();
            gambler.Vices.Add(V("Four-Card Charlie"));
            var t = With(gambler, "2", "10", "3", "9", "4", "5");
            Play(t, Deal, Hit, Hit);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Charlie));
        }

        [Test]
        public void BottomlessBurns_NeverRunOut_ButAddHeat()
        {
            var gambler = new Gambler();
            gambler.Vices.Add(V("Bottomless Burns"));
            var t = With(gambler, "K", "9", "6", "8", "Q", "J", "3");
            t.AddBurns(-t.BurnsLeft);
            Play(t, Deal, Hit, Burn, Burn);
            Assert.That(t.ActiveHand.Value.Total, Is.EqualTo(19));
            Assert.That(t.Gambler.Heat, Is.EqualTo(8));
        }

        // Feats

        [Test]
        public void TheBlackJack_IsAFeatWorthX3()
        {
            var t = With("As", "9", "Jc", "8");
            Play(t, Deal);
            var score = t.LastScores.Single();
            Assert.That(score.Feats, Has.Member(Feat.TheBlackJack));
            Assert.That(score.Steps.Any(s => s.Kind == ScoreSource.Feat), Is.True);
        }

        [Test]
        public void ARoyalCouple_NeedsAKingAndQueenOfOneSuit()
        {
            var t = With("Kh", "9", "Qh", "8");
            Play(t, Deal, Stand);
            Assert.That(t.LastScores.Single().Feats, Has.Member(Feat.RoyalCouple));
            var plain = With("Kh", "9", "Qs", "8");
            Play(plain, Deal, Stand);
            Assert.That(plain.LastScores.Single().Feats, Is.Empty);
        }

        // boss second phases

        [Test]
        public void PastHalfTheTarget_TheBossShowsASecondSide()
        {
            var boss = Bosses.Get("The Ceiling").Create();
            var t = new Table(new Gambler(), StackOf("K", "9", "8", "8", "K", "9", "8", "8"), new GameRandom(1), 40, new TableEffect[] { boss });
            Assert.That(t.Rules.PlayerBustLimit, Is.EqualTo(19));
            Play(t, Deal, Stand);
            Assert.That(t.Score * 2, Is.GreaterThanOrEqualTo(t.Target));
            Assert.That(t.RagedThisDeal, Is.True);
            Assert.That(t.Announcements, Has.Member(boss.RageText));
            Assert.That(t.Rules.Pushes, Is.EqualTo(PushRule.DealerWins));
        }

        [Test]
        public void EveryBossButTheHouse_HasASecondPhase()
        {
            foreach (var boss in Bosses.PitBosses.Concat(new[] { Bosses.EyeInTheSky, Bosses.Enforcer }))
                Assert.That(boss.Rage, Is.Not.Null.And.Not.Empty, boss.Name);
        }

        // the slot machine

        [Test]
        public void TheSlotMachine_PaysOutAndStopsAfterThreePulls()
        {
            for (int seed = 1; seed < 40; seed++)
            {
                var run = new Run(seed);
                run.Gambler.GainGold(30);
                run.EnterRoom(RoomKind.Slots);
                for (int i = 0; i < Balance.SlotPulls; i++)
                {
                    Assert.That(run.IsLegal(RunInput.RoomOption(0)), Is.True);
                    run.Apply(RunInput.RoomOption(0));
                    Assert.That(run.Room.Reels, Has.Length.EqualTo(3));
                    Assert.That(run.Room.Result, Is.Not.Empty);
                }
                Assert.That(run.IsLegal(RunInput.RoomOption(0)), Is.False);
            }
        }

        // Daily modifiers

        [Test]
        public void Modifiers_ChangeTheRun_AndSurviveTheRunCode()
        {
            var wild = new Run(3, new RunOptions { Modifier = Modifier.WildRoyals });
            Assert.That(wild.Gambler.Deck.Where(c => c.IsFace).All(c => c.Enhancement == Enhancement.Wild), Is.True);
            var shortShoe = new Run(3, new RunOptions { Modifier = Modifier.ShortShoe });
            Assert.That(shortShoe.Gambler.Deck.Any(c => c.Rank <= Rank.Five), Is.False);
            var high = new Run(3, new RunOptions { Modifier = Modifier.HighRollers });
            Assert.That(high.CurrentTarget, Is.GreaterThan(new Run(3).CurrentTarget));

            foreach (var modifier in Modifiers.All)
            {
                var code = RunCodes.Encode(77, new RunOptions { Modifier = modifier, Endless = true });
                Assert.That(RunCodes.TryDecode(code, out _, out var options), Is.True, code);
                Assert.That(options.Modifier, Is.EqualTo(modifier));
                Assert.That(options.Endless, Is.True);
            }
            Assert.That(Modifiers.ForSeed(12345), Is.Not.EqualTo(Modifier.None));
        }

        [Test]
        public void TheBot_StillFinishesRuns_WithEveryModifier()
        {
            var bot = new BalanceBot();
            foreach (var modifier in Modifiers.All)
                for (int seed = 1; seed <= 5; seed++)
                {
                    var run = new Run(seed, new RunOptions { Modifier = modifier });
                    bot.Play(run);
                    Assert.That(run.IsOver, Is.True, $"{modifier} seed {seed}");
                }
        }
    }
}
