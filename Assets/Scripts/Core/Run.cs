using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    public enum TableKind { Low, High, Boss }

    public enum RunPhase
    {
        // playing blackjack at Run.Table
        AtTable,
        // the table was beaten, waiting for Continue
        TableCleared,
        Won,
        Lost,
    }

    // a full climb: 8 floors of Low Table, High Table and Pit Boss. one seed drives all of it
    public sealed class Run
    {
        private readonly List<RunInput> _inputs = new List<RunInput>();

        public int Seed { get; }
        public GameRandom Random { get; }
        public Gambler Gambler { get; }

        public int Floor { get; private set; } = 1;
        public TableKind Stage { get; private set; } = TableKind.Low;
        public Table Table { get; private set; }
        public RunPhase Phase { get; private set; }
        public int TablesBeaten { get; private set; }

        public IReadOnlyList<RunInput> Inputs => _inputs;
        public bool IsOver => Phase == RunPhase.Won || Phase == RunPhase.Lost;

        public Run(int seed)
        {
            Seed = seed;
            Random = new GameRandom(seed);
            Gambler = new Gambler();
            StartTable();
        }

        public static Run Replay(int seed, IEnumerable<RunInput> inputs)
        {
            var run = new Run(seed);
            foreach (var input in inputs) run.Apply(input);
            return run;
        }

        public long CurrentTarget => Balance.Target(Floor, Stage);

        public bool IsLegal(RunInput input) => input.Kind switch
        {
            RunInputKind.Table => Phase == RunPhase.AtTable && Table.IsLegal(input.Action),
            RunInputKind.Continue => Phase == RunPhase.TableCleared,
            _ => false,
        };

        public void Apply(RunInput input)
        {
            if (!IsLegal(input)) throw new InvalidOperationException($"{input} isn't allowed during {Phase}.");
            _inputs.Add(input);

            switch (input.Kind)
            {
                case RunInputKind.Table:
                    Table.Apply(input.Action);
                    if (Table.IsOver) FinishTable();
                    break;
                case RunInputKind.Continue:
                    NextTable();
                    break;
            }
        }

        private void StartTable()
        {
            Table = new Table(Gambler, Random, CurrentTarget);
            Phase = RunPhase.AtTable;
        }

        private void FinishTable()
        {
            if (Table.Phase == TablePhase.Lost)
            {
                Phase = RunPhase.Lost;
                return;
            }
            TablesBeaten++;
            Phase = Floor == Balance.Floors && Stage == TableKind.Boss ? RunPhase.Won : RunPhase.TableCleared;
        }

        private void NextTable()
        {
            if (Stage == TableKind.Boss)
            {
                Floor++;
                Stage = TableKind.Low;
            }
            else Stage++;
            StartTable();
        }
    }
}
