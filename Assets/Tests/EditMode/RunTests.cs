using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;

namespace HouseEdge.Tests
{
    public class RunTests
    {
        [Test]
        public void Targets_RiseEveryTable()
        {
            // harder within a floor, and every kind of table harder than on the floor before
            for (int floor = 1; floor <= Balance.Floors; floor++)
            {
                Assert.That(Balance.Target(floor, TableKind.High), Is.GreaterThan(Balance.Target(floor, TableKind.Low)));
                Assert.That(Balance.Target(floor, TableKind.Boss), Is.GreaterThan(Balance.Target(floor, TableKind.High)));
                if (floor > 1)
                    foreach (TableKind kind in new[] { TableKind.Low, TableKind.High, TableKind.Boss })
                        Assert.That(Balance.Target(floor, kind), Is.GreaterThan(Balance.Target(floor - 1, kind)), $"floor {floor} {kind}");
            }
            Assert.That(Balance.Target(1, TableKind.Low), Is.EqualTo(Balance.FirstTableTarget));
        }

        [Test]
        public void ARun_StartsAtFloor1sLowTable()
        {
            var run = new Run(1);
            Assert.That(run.Floor, Is.EqualTo(1));
            Assert.That(run.Stage, Is.EqualTo(TableKind.Low));
            Assert.That(run.Phase, Is.EqualTo(RunPhase.AtTable));
            Assert.That(run.Table.Target, Is.EqualTo(Balance.Target(1, TableKind.Low)));
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
            var run = PlayUntil(5, r => r.IsOver || r.Phase != RunPhase.AtTable, alwaysStand: true);
            // standing on everything rarely reaches 150 in 4 Hands, but if it does, keep going until a loss
            while (!run.IsOver)
            {
                if (run.Phase != RunPhase.AtTable) AutoPlay.Advance(run);
                else PlayOne(run, alwaysStand: true);
            }
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Lost));
        }

        [Test]
        public void BeatingATable_OpensTheMap_ThenMovesToTheNextStage()
        {
            var run = FindRunOnTheMapFrom(1);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.RoomChoice));
            Assert.That(run.TablesBeaten, Is.EqualTo(1));

            AutoPlay.ToNextTable(run);
            Assert.That(run.Stage, Is.EqualTo(TableKind.High));
            Assert.That(run.Table.Target, Is.EqualTo(Balance.Target(1, TableKind.High)));
            Assert.That(run.Table.HandsLeft, Is.EqualTo(Balance.StartingHands), "Hands reset each table");
            // Old Pete adds a Burn at his table
            int burns = Balance.StartingBurns + (run.Dealer?.Name == "Old Pete" ? 1 : 0);
            Assert.That(run.Table.BurnsLeft, Is.EqualTo(burns), "Burns reset each table");
        }

        [Test]
        public void TheGamblersDeck_CarriesIntoEveryTable()
        {
            var run = FindRunThatBeatsFirstTable();
            var deck = run.Gambler.Deck;
            AutoPlay.ToNextTable(run);
            Assert.That(run.Table.Gambler.Deck, Is.SameAs(deck));
            Assert.That(run.Table.CardsInShoe, Is.EqualTo(deck.Count));
        }

        [Test]
        public void SameSeedAndInputs_ReplayTheSameRun()
        {
            var run = FindRunThatBeatsFirstTable();
            AutoPlay.ToNextTable(run);
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
            var run = FindRunThatBeatsFirstTableFrom(1);
            if (run == null) Assert.Fail("no seed in 1-499 beat the first table");
            return run;
        }

        // a run that just beat the Low Table and walked into the Shop on the map (the first row always has one)
        internal static Run FindRunThatBeatsFirstTableFrom(int firstSeed)
        {
            var run = FindRunOnTheMapFrom(firstSeed);
            if (run == null) return null;
            run.Apply(RunInput.ChooseRoom(run.RoomChoices.ToList().IndexOf(RoomKind.Shop)));
            return run;
        }

        // a run that just beat the Low Table, choosing its first stop on the map
        internal static Run FindRunOnTheMapFrom(int firstSeed)
        {
            for (int seed = firstSeed; seed < firstSeed + 500; seed++)
            {
                var run = PlayUntil(seed, r => r.IsOver || r.Phase != RunPhase.AtTable, alwaysStand: false);
                if (run.Phase == RunPhase.RoomChoice) return run;
            }
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
