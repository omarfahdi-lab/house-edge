using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;

namespace HouseEdge.Tests
{
    public class DeckAndStakeTests
    {
        [Test]
        public void StartingDecks_HaveTheRightCards()
        {
            Assert.That(Decks.Build(StartingDeck.Standard).Count, Is.EqualTo(52));
            Assert.That(Decks.Build(StartingDeck.ShortDeck).Count, Is.EqualTo(36));
            Assert.That(Decks.Build(StartingDeck.ShortDeck).Any(c => c.Rank <= Rank.Five), Is.False);
            Assert.That(Decks.Build(StartingDeck.AceHigh).Count(c => c.Rank == Rank.Ace), Is.EqualTo(8));
            Assert.That(Decks.Build(StartingDeck.Lowball).Any(c => c.IsFace), Is.False);
            Assert.That(Decks.Build(StartingDeck.MarkedDeck).All(c => c.Enhancement == Enhancement.Marked), Is.True);
        }

        [Test]
        public void MarkedDeck_StartsWith30Heat()
        {
            var run = new Run(1, new RunOptions { Deck = StartingDeck.MarkedDeck });
            Assert.That(run.Gambler.Heat, Is.EqualTo(30));
        }

        [Test]
        public void DealersDeck_ShowsTheHoleCard_ButTakesAHand()
        {
            var run = new Run(1, new RunOptions { Deck = StartingDeck.DealersDeck });
            Assert.That(run.Table.HandsLeft, Is.EqualTo(Balance.StartingHands - 1));
            Assert.That(run.Table.Rules.HoleCardFaceUp, Is.True);
        }

        [Test]
        public void GreenStake_RaisesTargets_AndIncludesRed()
        {
            var run = new Run(1, new RunOptions { Stake = Stake.Green });
            Assert.That(run.Table.Target, Is.EqualTo((long)System.Math.Round(Balance.Target(1, TableKind.Low) * 1.1)));
        }

        [Test]
        public void BlackStake_StartsWithTwoDeadCards()
        {
            var run = new Run(1, new RunOptions { Stake = Stake.Black });
            Assert.That(run.Gambler.Deck.Count(c => c.Curse == Curse.DeadCard), Is.EqualTo(2));
        }

        [Test]
        public void GoldStake_DealerHitsSoft17()
        {
            var run = new Run(1, new RunOptions { Stake = Stake.Gold });
            Assert.That(run.Table.Rules.DealerHitsSoft17, Is.True);
        }

        [Test]
        public void RedStake_LowTablesPayNoBaseGold()
        {
            for (int seed = 1; seed < 200; seed++)
            {
                var run = new Run(seed, new RunOptions { Stake = Stake.Red });
                AutoPlay.UntilThisTableEnds(run);
                if (run.Phase != RunPhase.RoomChoice) continue;
                Assert.That(run.LastPayout.Base, Is.EqualTo(0));
                return;
            }
            Assert.Fail("no seed beat the first table");
        }

        [Test]
        public void EndlessTargets_KeepDoubling()
        {
            Assert.That(Balance.Target(9, TableKind.Low), Is.EqualTo(Balance.Target(8, TableKind.Low) * 2));
            Assert.That(Balance.Target(10, TableKind.Boss), Is.EqualTo(Balance.Target(8, TableKind.Boss) * 4));
        }
    }

    public class RunCodeTests
    {
        [TestCase(1)]
        [TestCase(-5)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void Codes_RoundTrip(int seed)
        {
            var options = new RunOptions { Deck = StartingDeck.Lowball, Stake = Stake.Purple, Endless = true };
            string code = RunCodes.Encode(seed, options);
            Assert.That(RunCodes.TryDecode(code, out int decoded, out var back), Is.True, code);
            Assert.That(decoded, Is.EqualTo(seed));
            Assert.That(back.Deck, Is.EqualTo(StartingDeck.Lowball));
            Assert.That(back.Stake, Is.EqualTo(Stake.Purple));
            Assert.That(back.Endless, Is.True);
        }

        [TestCase("")]
        [TestCase("hello")]
        [TestCase("ABC-99")]
        [TestCase("I0-00")]
        public void BadCodes_DontDecode(string code)
        {
            Assert.That(RunCodes.TryDecode(code, out _, out _), Is.False);
        }

        [Test]
        public void TheSameCode_StartsTheSameRun()
        {
            var run = new Run(77, new RunOptions { Deck = StartingDeck.AceHigh });
            RunCodes.TryDecode(run.Code, out int seed, out var options);
            var again = new Run(seed, options);
            Assert.That(again.Gambler.Deck.Count, Is.EqualTo(run.Gambler.Deck.Count));
            Assert.That(again.FloorBoss.Name, Is.EqualTo(run.FloorBoss.Name));
            Assert.That(again.Dealer.Name, Is.EqualTo(run.Dealer.Name));
        }

        [Test]
        public void DailySeed_IsTheSameForADate_AndDiffersByDay()
        {
            Assert.That(RunCodes.DailySeed(2026, 10, 21), Is.EqualTo(RunCodes.DailySeed(2026, 10, 21)));
            Assert.That(RunCodes.DailySeed(2026, 10, 21), Is.Not.EqualTo(RunCodes.DailySeed(2026, 10, 22)));
        }

        [Test]
        public void AFullRun_ReplaysFromItsInputs()
        {
            var run = new Run(5, new RunOptions { Deck = StartingDeck.ShortDeck });
            for (int i = 0; i < 400 && !run.IsOver; i++)
            {
                if (run.Phase == RunPhase.AtTable) AutoPlay.Step(run);
                else AutoPlay.Advance(run);
            }
            var replay = Run.Replay(run.Seed, run.Inputs, run.Options);
            Assert.That(replay.Phase, Is.EqualTo(run.Phase));
            Assert.That(replay.TotalScore, Is.EqualTo(run.TotalScore));
            Assert.That(replay.Floor, Is.EqualTo(run.Floor));
            Assert.That(replay.Gambler.Gold, Is.EqualTo(run.Gambler.Gold));
        }
    }

    public class ProfileTests
    {
        [Test]
        public void ANewProfile_HasTheStandardDeckAndWhiteStake()
        {
            var p = new Profile();
            Assert.That(p.UnlockedDecks, Is.EqualTo(new[] { StartingDeck.Standard }));
            Assert.That(p.HighestStake, Is.EqualTo(Stake.White));
        }

        [Test]
        public void Profiles_RoundTripThroughText()
        {
            var run = LostRun();
            var p = new Profile();
            p.Record(run);
            var back = Profile.Parse(p.Serialize());
            Assert.That(back.Runs, Is.EqualTo(1));
            Assert.That(back.BestScore, Is.EqualTo(run.TotalScore));
            Assert.That(back.UnlockedDecks, Is.EqualTo(p.UnlockedDecks));
        }

        [Test]
        public void BrokenText_GivesAFreshProfile()
        {
            var p = Profile.Parse("decks=x,9\nstake=banana\nruns=");
            Assert.That(p.UnlockedDecks, Is.EqualTo(new[] { StartingDeck.Standard }));
            Assert.That(p.Runs, Is.EqualTo(0));
        }

        [Test]
        public void AnUnfinishedRun_RecordsNothing()
        {
            var p = new Profile();
            Assert.That(p.Record(new Run(1)), Is.Empty);
            Assert.That(p.Runs, Is.EqualTo(0));
        }

        private static Run LostRun()
        {
            var run = new Run(3);
            for (int i = 0; i < 2000 && !run.IsOver; i++)
            {
                if (run.Phase == RunPhase.AtTable) AutoPlay.Step(run);
                else AutoPlay.Advance(run);
            }
            Assert.That(run.IsOver, Is.True);
            return run;
        }
    }
}

namespace HouseEdge.Tests
{
    public class BalanceRegressionTests
    {
        // the tuned curve keeps the bot near its target win rate; CI catches balance changes that break it
        [Test]
        public void TunedBalance_KeepsTheBotNearItsTargetWinRate()
        {
            var report = BalanceSimulation.Run(300, 1);
            Assert.That(report.WinRate, Is.InRange(0.07, 0.25), $"bot won {report.WinRate:P1}");
            // floor 1 asks something of you now (the bot used to clear it 9 runs in 10), but it shouldn't end most runs
            Assert.That((double)report.Reached[1, 0] / report.Runs, Is.GreaterThan(0.65), "floor 1 should be fair");
        }

        [Test]
        public void TheBot_AlwaysFinishesARun()
        {
            var bot = new BalanceBot();
            for (int seed = 1; seed <= 30; seed++)
            {
                var run = new Run(seed);
                bot.Play(run);
                Assert.That(run.IsOver, Is.True, $"seed {seed} got stuck in {run.Phase}");
            }
        }

        [Test]
        public void TheHistory_HasEveryTableAndAddsUpToTheTotal()
        {
            var bot = new BalanceBot();
            for (int seed = 1; seed <= 20; seed++)
            {
                var run = new Run(seed);
                bot.Play(run);
                Assert.That(run.History.Sum(r => r.Score), Is.EqualTo(run.TotalScore), $"seed {seed}");
                Assert.That(run.History.Count(r => r.Won), Is.EqualTo(run.TablesBeaten), $"seed {seed}");
                if (run.Phase == RunPhase.Lost) Assert.That(run.History.Last().Won, Is.False);
            }
        }
    }
}

namespace HouseEdge.Tests
{
    public class EndlessTests
    {
        [Test]
        public void Endless_KeepsGoingAfterTheHouse()
        {
            var bot = new BalanceBot();
            for (int seed = 1; seed < 200; seed++)
            {
                var run = new Run(seed, new RunOptions { Endless = true });
                for (int i = 0; i < 8000 && !run.IsOver && !(run.BeatTheHouse && run.Floor > Balance.Floors); i++)
                    run.Apply(bot.Choose(run));
                if (!run.BeatTheHouse) continue;

                Assert.That(run.Phase, Is.Not.EqualTo(RunPhase.Won), "Endless runs never end in a win");
                Assert.That(run.Floor, Is.EqualTo(Balance.Floors + 1));
                Assert.That(run.FloorBoss.Name, Is.Not.EqualTo("The House"));
                Assert.That(run.CurrentTarget, Is.GreaterThan(Balance.Target(Balance.Floors, TableKind.Low)));
                return;
            }
            Assert.Fail("the bot never beat The House in 200 seeds");
        }

        [Test]
        public void ANormalRun_EndsWhenTheHouseFalls()
        {
            var bot = new BalanceBot();
            for (int seed = 1; seed < 200; seed++)
            {
                var run = new Run(seed);
                bot.Play(run);
                if (!run.BeatTheHouse) continue;
                Assert.That(run.Phase, Is.EqualTo(RunPhase.Won));
                Assert.That(run.Floor, Is.EqualTo(Balance.Floors));
                return;
            }
            Assert.Fail("the bot never beat The House in 200 seeds");
        }
    }
}
