namespace HouseEdge.Core
{
    public enum RunInputKind
    {
        // a blackjack action at the current table
        Table,
        BuyVice,
        SellVice,
        // move the Vice at Index to position Index2
        MoveVice,
        Reroll,
        LeaveShop,
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
        public static RunInput BuyVice(int offer) => new RunInput(RunInputKind.BuyVice, index: offer);
        public static RunInput SellVice(int slot) => new RunInput(RunInputKind.SellVice, index: slot);
        public static RunInput MoveVice(int from, int to) => new RunInput(RunInputKind.MoveVice, index: from, index2: to);
        public static RunInput Reroll() => new RunInput(RunInputKind.Reroll);
        public static RunInput LeaveShop() => new RunInput(RunInputKind.LeaveShop);

        public override string ToString() => Kind == RunInputKind.Table ? Action.ToString()
            : Index2 >= 0 ? $"{Kind}({Index},{Index2})"
            : Index >= 0 ? $"{Kind}({Index})"
            : Kind.ToString();
    }
}
