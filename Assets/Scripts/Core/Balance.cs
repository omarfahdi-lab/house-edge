namespace HouseEdge.Core
{
    // all the tuning numbers live here. starting values come from DESIGN.md
    public static class Balance
    {
        // blackjack rules
        public const int BustLimit = 21;
        public const int DealerStandsOn = 17;
        public const int CharlieCards = 5;
        public const int MaxSplitsPerHand = 1;

        // per table
        public const int StartingHands = 4;
        public const int StartingBurns = 3;
        public const long FirstTableTarget = 150;

        // run
        public const int StartingGold = 4;
        public const int Floors = 8;

        // [floor - 1] = Low, High, Pit Boss. floor 8's boss is The House
        private static readonly long[][] TableTargets =
        {
            new long[] { 150, 225, 300 },
            new long[] { 300, 450, 600 },
            new long[] { 600, 900, 1200 },
            new long[] { 1200, 1800, 2400 },
            new long[] { 2500, 3750, 5000 },
            new long[] { 5000, 7500, 10000 },
            new long[] { 10000, 15000, 20000 },
            new long[] { 20000, 30000, 60000 },
        };

        public static long Target(int floor, TableKind kind) => TableTargets[floor - 1][(int)kind];

        // actions
        public const int SurrenderCost = 3;
        public const int InsuranceCost = 2;
        public const double DoubleDownMult = 2;

        // scoring: Chips = card total + base Chips, Mult = base Mult
        public const int DealerBustBonusChips = 15;
        public const int DealerBustBonusMult = 0;

        public static int BaseChips(Outcome outcome) => outcome switch
        {
            Outcome.Win => 10,
            Outcome.TwentyOne => 20,
            Outcome.Blackjack => 30,
            Outcome.Charlie => 40,
            _ => 0,
        };

        public static double BaseMult(Outcome outcome) => outcome switch
        {
            Outcome.Win => 2,
            Outcome.TwentyOne => 3,
            Outcome.Blackjack => 4,
            Outcome.Charlie => 4,
            Outcome.Push => 1,
            _ => 0,
        };

        // a push scores its cards, a loss or bust scores nothing
        public static bool ScoresCards(Outcome outcome) => outcome != Outcome.Lose && outcome != Outcome.Bust && outcome != Outcome.Surrender;
    }
}
