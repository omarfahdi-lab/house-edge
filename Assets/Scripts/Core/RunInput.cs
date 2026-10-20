namespace HouseEdge.Core
{
    public enum RunInputKind
    {
        // a blackjack action at the current table
        Table,
        // leave a finished table and go on
        Continue,
    }

    // one player input in a run. a run is its seed plus a list of these
    public readonly struct RunInput
    {
        public RunInputKind Kind { get; }
        public PlayerAction Action { get; }
        public int Index { get; }
        public int Index2 { get; }

        private RunInput(RunInputKind kind, PlayerAction action = default, int index = -1, int index2 = -1)
        {
            Kind = kind;
            Action = action;
            Index = index;
            Index2 = index2;
        }

        public static RunInput Table(PlayerAction action) => new RunInput(RunInputKind.Table, action);
        public static RunInput Continue() => new RunInput(RunInputKind.Continue);

        public override string ToString() => Kind == RunInputKind.Table ? Action.ToString()
            : Index2 >= 0 ? $"{Kind}({Index},{Index2})"
            : Index >= 0 ? $"{Kind}({Index})"
            : Kind.ToString();
    }
}
