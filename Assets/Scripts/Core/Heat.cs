namespace HouseEdge.Core
{
    public enum HeatLevel
    {
        // 0-24: no effect
        Clean,
        // 25-49: the dealer reshuffles every 2 hands, which wrecks card counting
        Watched,
        // 50-74: shop prices go up 25%
        Suspected,
        // 75-99: a random Cheater Vice is disabled at each table
        Flagged,
        // 100: security drags you to The Eye in the Sky
        Busted,
    }

    public static class HeatLevels
    {
        public static string Describe(HeatLevel level) => level switch
        {
            HeatLevel.Watched => "Watched: the dealer reshuffles every 2 hands.",
            HeatLevel.Suspected => "Suspected: shop prices are up 25%.",
            HeatLevel.Flagged => "Flagged: a random Cheater Vice is disabled each table.",
            HeatLevel.Busted => "Busted! Security is coming for you.",
            _ => "Clean: nobody is watching.",
        };
    }

    // holding 3 Vices of one archetype unlocks its set bonus
    public static class ArchetypeBonuses
    {
        public static bool Has(Gambler gambler, Archetype archetype) => gambler.ArchetypeCount(archetype) >= Balance.ArchetypeSetSize;

        public static string Describe(Archetype archetype) => archetype switch
        {
            Archetype.Lucky => "All chance effects get +1 (1 in 4 becomes 2 in 4).",
            Archetype.Risky => "Your bust limit goes up by 1.",
            Archetype.Cheater => "All Heat you gain is halved.",
            Archetype.Greedy => "Interest cap goes up by 3.",
            Archetype.Steady => "+1 Hand at every table.",
            _ => "Scaling Vices grow twice as fast.",
        };

        public static double HeatFactor(Gambler gambler) => Has(gambler, Archetype.Cheater) ? 0.5 : 1;
        public static int ScalingRate(Gambler gambler) => Has(gambler, Archetype.Combo) ? 2 : 1;
        public static int ExtraChances(Gambler gambler) => Has(gambler, Archetype.Lucky) ? 1 : 0;

        public static int InterestCap(Gambler gambler) =>
            Balance.InterestCap + (Has(gambler, Archetype.Greedy) ? Balance.GreedyInterestCapBonus : 0);

        // applied when a table starts
        public static void Apply(Table table)
        {
            if (Has(table.Gambler, Archetype.Risky)) table.Rules.PlayerBustLimit++;
            if (Has(table.Gambler, Archetype.Steady)) table.AddHands(1);
        }
    }
}
