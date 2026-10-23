using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    // a twist on the whole run. the Daily Run picks one from the date, so every day plays differently
    public enum Modifier { None, WildRoyals, HotNight, ShortShoe, HighRollers, TightTable }

    public static class Modifiers
    {
        public static readonly IReadOnlyList<Modifier> All = (Modifier[])Enum.GetValues(typeof(Modifier));

        public static string Name(Modifier modifier) => modifier switch
        {
            Modifier.WildRoyals => "Wild Royals",
            Modifier.HotNight => "Hot Night",
            Modifier.ShortShoe => "Short Shoe",
            Modifier.HighRollers => "High Rollers",
            Modifier.TightTable => "Tight Table",
            _ => "No twist",
        };

        public static string Describe(Modifier modifier) => modifier switch
        {
            Modifier.WildRoyals => "Every Jack, Queen and King starts Wild, so it counts as any suit for Flushes.",
            Modifier.HotNight => "All Heat counts double, and every table pays double gold.",
            Modifier.ShortShoe => "No 2s, 3s, 4s or 5s in your deck. Big cards, big busts.",
            Modifier.HighRollers => "+3 Mult on every win, but every target is 30% higher.",
            Modifier.TightTable => "1 fewer Hand at every table, but 2 more Burns.",
            _ => "",
        };

        // today's twist for a Daily seed. never None
        public static Modifier ForSeed(int seed) => All[1 + (int)((uint)seed % (uint)(All.Count - 1))];

        public static double TargetFactor(Modifier modifier) => modifier == Modifier.HighRollers ? 1.3 : 1;
        public static int PayoutFactor(Modifier modifier) => modifier == Modifier.HotNight ? 2 : 1;
    }

    // a modifier's rules at each table
    internal sealed class ModifierRules : TableEffect
    {
        private readonly Modifier _modifier;

        public ModifierRules(Modifier modifier) => _modifier = modifier;

        public override void OnTableStart(Table table)
        {
            if (_modifier != Modifier.TightTable) return;
            table.AddHands(-1);
            table.AddBurns(2);
        }

        public override void OnHandScored(Table table, HandScore score)
        {
            if (_modifier == Modifier.HighRollers && score.Outcome.IsWin()) score.AddMult(3);
        }
    }
}
