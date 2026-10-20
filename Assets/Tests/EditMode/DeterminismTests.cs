using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;

namespace HouseEdge.Tests
{
    public class DeterminismTests
    {
        // plays a whole table with a simple fixed strategy and records everything visible
        private static (List<PlayerAction> inputs, string log) PlayTable(int seed)
        {
            var t = new Table(seed, Balance.FirstTableTarget);
            var log = new List<string>();
            while (!t.IsOver)
            {
                var legal = t.LegalActions();
                PlayerAction action;
                if (legal.Contains(PlayerAction.Deal)) action = PlayerAction.Deal;
                else if (legal.Contains(PlayerAction.DeclineInsurance)) action = PlayerAction.TakeInsurance;
                else if (legal.Contains(PlayerAction.Split)) action = PlayerAction.Split;
                else if (legal.Contains(PlayerAction.Burn) && t.ActiveHand.Value.IsBust) action = PlayerAction.Burn;
                else if (legal.Contains(PlayerAction.DoubleDown) && t.ActiveHand.Value.Total == 11) action = PlayerAction.DoubleDown;
                else if (t.ActiveHand.Value.Total < 17 && legal.Contains(PlayerAction.Hit)) action = PlayerAction.Hit;
                else action = PlayerAction.Stand;
                t.Apply(action);
                log.Add(Describe(t));
            }
            return (t.Inputs.ToList(), string.Join("\n", log));
        }

        private static string Describe(Table t) =>
            $"{t.Phase} score={t.Score} hands={t.HandsLeft} burns={t.BurnsLeft} gold={t.Gold} " +
            $"player={string.Join("|", t.PlayerHands.Select(h => string.Join(",", h.Cards)))} " +
            $"dealer={string.Join(",", t.DealerCards)} " +
            $"scores={string.Join(";", t.LastScores)}";

        [Test]
        public void SameSeedAndInputs_ReplayToTheSameTable()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var (inputs, log) = PlayTable(seed);
                var replayed = Replay.Table(seed, Balance.FirstTableTarget, inputs);
                var original = PlayTable(seed);

                Assert.That(original.log, Is.EqualTo(log), $"seed {seed} played twice");
                Assert.That(Describe(replayed), Is.EqualTo(log.Split('\n').Last()), $"seed {seed} replayed from inputs");
            }
        }

        [Test]
        public void DifferentSeeds_PlayDifferently()
        {
            Assert.That(PlayTable(1).log, Is.Not.EqualTo(PlayTable(2).log));
        }
    }
}
