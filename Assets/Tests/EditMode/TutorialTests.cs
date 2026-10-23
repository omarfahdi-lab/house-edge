using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;

namespace HouseEdge.Tests
{
    public class TutorialTests
    {
        private static Run Start(int seed = 5) => new Run(seed, new RunOptions { Tutorial = true });

        private static void Play(Run run, params PlayerAction[] actions)
        {
            foreach (var action in actions)
            {
                Assert.That(run.IsLegal(RunInput.Table(action)), Is.True, $"{action} should be legal in {run.Phase}/{run.Table.Phase}, hand {run.Table.DealsPlayed}: {string.Join(" ", run.Table.ActiveHand?.Cards.Select(c => c.RankLabel + c.Suit) ?? new string[0])} vs {string.Join(" ", run.Table.DealerCards.Select(c => c.RankLabel + c.Suit))}");
                run.Apply(RunInput.Table(action));
            }
        }

        [Test]
        public void TheScriptedHands_TeachAPlainWinAFlushAndABurn([Values(1, 2, 3, 99)] int seed)
        {
            var run = Start(seed);
            Assert.That(run.IsTutorialTable, Is.True);
            Assert.That(run.Dealer.Name, Is.EqualTo("Chatty Chen"));

            Play(run, Deal);
            Assert.That(run.Table.ActiveHand.Value.Total, Is.EqualTo(19));
            Play(run, Stand);
            var first = run.Table.LastScores.Single();
            Assert.That(first.Outcome, Is.EqualTo(Outcome.Win));
            Assert.That(first.Combos, Is.Empty);

            Play(run, Deal, Hit);
            Assert.That(run.Table.ActiveHand.Value.IsBust, Is.True);
            Play(run, Burn);
            Assert.That(run.Table.ActiveHand.Value.Total, Is.EqualTo(20));
            Play(run, Stand);
            var second = run.Table.LastScores.Single();
            Assert.That(second.Outcome.IsWin(), Is.True);
            Assert.That(run.Table.Score, Is.LessThan(run.Table.Target), "the combo hand should be the one that clears the table");

            Play(run, Deal, Hit);
            Assert.That(run.Table.ActiveHand.Value.Total, Is.EqualTo(20));
            Play(run, Stand);
            var third = run.Table.LastScores.Single();
            Assert.That(third.Combos, Has.Member(Combo.Flush));
            Assert.That(third.Total, Is.GreaterThan((first.Total + second.Total) * 2), "the combo should dwarf the plain wins");
            Assert.That(run.Table.Phase, Is.EqualTo(TablePhase.Won));
        }

        [Test]
        public void OnlyTheFirstTableIsStacked()
        {
            var run = Start();
            var bot = new BalanceBot();
            for (int i = 0; i < 3000 && run.TablesBeaten == 0 && !run.IsOver; i++) run.Apply(bot.Choose(run));
            Assert.That(run.IsTutorialTable, Is.False);
        }

        [Test]
        public void TutorialRuns_KeepTheirFlagInTheRunCode()
        {
            var code = RunCodes.Encode(1234, new RunOptions { Tutorial = true });
            Assert.That(RunCodes.TryDecode(code, out int seed, out var options), Is.True);
            Assert.That(seed, Is.EqualTo(1234));
            Assert.That(options.Tutorial, Is.True);
            Assert.That(options.Endless, Is.False);
        }
    }
}
