using System.Collections.Generic;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // deal order: player, dealer up, player, dealer hole, then whatever is drawn next
    public class RivalTests
    {
        private static Table RivalTable(int meeting, params object[] topFirst) =>
            new Table(StackOf(topFirst), long.MaxValue, new TableEffect[] { new Rival("Silas Crow", meeting) });

        [Test]
        public void BothHandsAreFaceUp()
        {
            var t = RivalTable(1, "10", "9", "8", "7");
            t.Apply(Deal);
            Assert.That(t.HoleCardHidden, Is.False);
        }

        [Test]
        public void HeDrawsPast17UntilHeBeatsYou()
        {
            // you stand on 20; he has 17 and keeps drawing: 17 + 2 = 19, + 2 = 21
            var t = RivalTable(1, "10", "10", "Q", "7", "2", "2");
            Play(t, Deal, Stand);
            Assert.That(t.DealerValue.Total, Is.EqualTo(21));
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Lose));
        }

        [Test]
        public void HeStandsAsSoonAsHesAhead_EvenBelow17()
        {
            // you stand on 12; he has 13 and stops
            var t = RivalTable(1, "10", "10", "2", "3", "5");
            Play(t, Deal, Stand);
            Assert.That(t.DealerCards.Count, Is.EqualTo(2));
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Lose));
        }

        [Test]
        public void ChasingCanBustHim()
        {
            // you stand on 19; he has 17, draws a 10 and busts
            var t = RivalTable(1, "10", "10", "9", "7", "K");
            Play(t, Deal, Stand);
            Assert.That(t.DealerBusted, Is.True);
            Assert.That(t.LastScores[0].Outcome, Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void FirstMeeting_ATieIsAPush_SecondMeeting_HeHitsOnIt()
        {
            var first = RivalTable(1, "10", "10", "8", "8", "2");
            Play(first, Deal, Stand);
            Assert.That(first.LastScores[0].Outcome, Is.EqualTo(Outcome.Push));

            var second = RivalTable(2, "10", "10", "8", "8", "2");
            Play(second, Deal, Stand);
            Assert.That(second.DealerValue.Total, Is.EqualTo(20));
            Assert.That(second.LastScores[0].Outcome, Is.EqualTo(Outcome.Lose));
        }

        [Test]
        public void LaterMeetings_AddHisTricks()
        {
            Assert.That(RivalTable(2, "10").Rules.DealerRedrawsFirstBust, Is.False);
            Assert.That(RivalTable(3, "10").Rules.DealerRedrawsFirstBust, Is.True);
            int hands = RivalTable(3, "10").HandsLeft;
            Assert.That(RivalTable(4, "10").HandsLeft, Is.EqualTo(hands - Balance.RivalFinalHandsLess));
        }

        [Test]
        public void BeatingHimPaysGold()
        {
            var t = new Table(StackOf("10", "10", "9", "7", "K"), 1, new TableEffect[] { new Rival("Silas Crow", 1) });
            int gold = t.Gold;
            Play(t, Deal, Stand);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.Won));
            Assert.That(t.Gold, Is.EqualTo(gold + Balance.RivalBeatenGold));
        }

        [Test]
        public void TheRunOption_SeatsHimAtTheHighTable_EveryOtherFloor()
        {
            Assert.That(Rival.SitsAt(2, TableKind.High), Is.True);
            Assert.That(Rival.SitsAt(4, TableKind.High), Is.True);
            Assert.That(Rival.SitsAt(1, TableKind.High), Is.False);
            Assert.That(Rival.SitsAt(2, TableKind.Low), Is.False);
            Assert.That(new Run(7, new RunOptions { Rival = true }).RivalName, Is.EqualTo(new Run(7).RivalName), "the seed picks him");
        }

        [Test]
        public void TheBot_MeetsHimInWholeRuns_AndTheyReplayTheSame()
        {
            var options = new RunOptions { Rival = true };
            int met = 0;
            for (int seed = 1; seed <= 10; seed++)
            {
                var run = new Run(seed, options);
                var inputs = new List<RunInput>();
                var bot = new BalanceBot();
                for (int i = 0; i < 6000 && !run.IsOver; i++)
                {
                    if (run.Phase == RunPhase.AtTable && run.Dealer is Rival rival)
                    {
                        Assert.That(rival.Meeting, Is.EqualTo(run.RivalMeetings));
                        Assert.That(run.Stage, Is.EqualTo(TableKind.High));
                    }
                    var input = bot.Choose(run);
                    inputs.Add(input);
                    run.Apply(input);
                }
                met = System.Math.Max(met, run.RivalMeetings);
                var replayed = Run.Replay(seed, inputs, options);
                Assert.That(replayed.Gambler.Gold, Is.EqualTo(run.Gambler.Gold), $"seed {seed}");
                Assert.That(replayed.RivalMeetings, Is.EqualTo(run.RivalMeetings), $"seed {seed}");
            }
            Assert.That(met, Is.GreaterThan(0), "some run reached floor 2's High Table");
        }

        [Test]
        public void AnEntranceWithNoBossBehindIt_HasNoPhaseTwo()
        {
            var entrance = new BossDefinition("Silas Crow", "title", "ability", "twist", "quote", "tip", null);
            Assert.That(entrance.Rage, Is.Null);
        }

        [Test]
        public void RunCodes_RememberHim()
        {
            string code = RunCodes.Encode(5, new RunOptions { Rival = true });
            Assert.That(RunCodes.TryDecode(code, out _, out var back), Is.True, code);
            Assert.That(back.Rival, Is.True);
        }
    }
}
