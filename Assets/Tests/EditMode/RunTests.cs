using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;

namespace HouseEdge.Tests
{
    public class RunTests
    {
        [Test]
        public void Targets_FollowTheDesignTable()
        {
            Assert.That(Balance.Target(1, TableKind.Low), Is.EqualTo(150));
            Assert.That(Balance.Target(1, TableKind.High), Is.EqualTo(225));
            Assert.That(Balance.Target(1, TableKind.Boss), Is.EqualTo(300));
            Assert.That(Balance.Target(8, TableKind.Boss), Is.EqualTo(60000));
        }

        [Test]
        public void ARun_StartsAtFloor1sLowTable()
        {
            var run = new Run(1);
            Assert.That(run.Floor, Is.EqualTo(1));
            Assert.That(run.Stage, Is.EqualTo(TableKind.Low));
            Assert.That(run.Phase, Is.EqualTo(RunPhase.AtTable));
            Assert.That(run.Table.Target, Is.EqualTo(150));
            Assert.That(run.Table.HandsLeft, Is.EqualTo(Balance.StartingHands));
        }

        [Test]
        public void TableActions_GoThroughTheRun_AndAreRecorded()
        {
            var run = new Run(1);
            run.Apply(RunInput.Table(PlayerAction.Deal));
            Assert.That(run.Inputs.Count, Is.EqualTo(1));
            Assert.That(run.Table.Inputs.Count, Is.EqualTo(1));
        }

        [Test]
        public void LosingATable_EndsTheRun()
        {
            var run = PlayUntil(5, r => r.IsOver || r.Phase == RunPhase.TableCleared, alwaysStand: true);
            // standing on everything rarely reaches 150 in 4 Hands, but if it does, keep going until a loss
            while (!run.IsOver)
            {
                if (run.Phase == RunPhase.TableCleared) run.Apply(RunInput.Continue());
                else PlayOne(run, alwaysStand: true);
            }
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Lost));
        }

        [Test]
        public void BeatingATable_WaitsForContinue_ThenMovesToTheNextStage()
        {
            var run = FindRunThatBeatsFirstTable();
            Assert.That(run.Phase, Is.EqualTo(RunPhase.TableCleared));
            Assert.That(run.TablesBeaten, Is.EqualTo(1));

            run.Apply(RunInput.Continue());
            Assert.That(run.Stage, Is.EqualTo(TableKind.High));
            Assert.That(run.Table.Target, Is.EqualTo(225));
            Assert.That(run.Table.HandsLeft, Is.EqualTo(Balance.StartingHands), "Hands reset each table");
            Assert.That(run.Table.BurnsLeft, Is.EqualTo(Balance.StartingBurns), "Burns reset each table");
        }

        [Test]
        public void TheGamblersDeck_CarriesIntoEveryTable()
        {
            var run = FindRunThatBeatsFirstTable();
            var deck = run.Gambler.Deck;
            run.Apply(RunInput.Continue());
            Assert.That(run.Table.Gambler.Deck, Is.SameAs(deck));
            Assert.That(run.Table.CardsInShoe, Is.EqualTo(deck.Count));
        }

        [Test]
        public void SameSeedAndInputs_ReplayTheSameRun()
        {
            var run = FindRunThatBeatsFirstTable();
            run.Apply(RunInput.Continue());
            PlayOne(run, alwaysStand: false);

            var replay = Run.Replay(run.Seed, run.Inputs);
            Assert.That(replay.Floor, Is.EqualTo(run.Floor));
            Assert.That(replay.Stage, Is.EqualTo(run.Stage));
            Assert.That(replay.Table.Score, Is.EqualTo(run.Table.Score));
            Assert.That(replay.Gambler.Gold, Is.EqualTo(run.Gambler.Gold));
            Assert.That(replay.Table.PlayerHands.Select(h => string.Join(",", h.Cards)),
                Is.EqualTo(run.Table.PlayerHands.Select(h => string.Join(",", h.Cards))));
        }

        internal static Run FindRunThatBeatsFirstTable()
        {
            for (int seed = 1; seed < 500; seed++)
            {
                var run = PlayUntil(seed, r => r.IsOver || r.Phase == RunPhase.TableCleared, alwaysStand: false);
                if (run.Phase == RunPhase.TableCleared) return run;
            }
            Assert.Fail("no seed in 1-499 beat the first table");
            return null;
        }

        internal static Run PlayUntil(int seed, System.Func<Run, bool> stop, bool alwaysStand)
        {
            var run = new Run(seed);
            while (!stop(run)) PlayOne(run, alwaysStand);
            return run;
        }

        // one input with a simple fixed strategy
        internal static void PlayOne(Run run, bool alwaysStand)
        {
            var t = run.Table;
            var legal = t.LegalActions();
            PlayerAction action;
            if (legal.Contains(PlayerAction.Deal)) action = PlayerAction.Deal;
            else if (legal.Contains(PlayerAction.DeclineInsurance)) action = PlayerAction.DeclineInsurance;
            else if (alwaysStand) action = PlayerAction.Stand;
            else if (legal.Contains(PlayerAction.Burn) && t.ActiveHand.Value.IsBust) action = PlayerAction.Burn;
            else if (t.ActiveHand.Value.Total < 17 && legal.Contains(PlayerAction.Hit)) action = PlayerAction.Hit;
            else action = PlayerAction.Stand;
            run.Apply(RunInput.Table(action));
        }
    }
}
