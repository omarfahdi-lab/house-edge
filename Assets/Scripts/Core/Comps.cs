using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    // a Comp levels up an outcome for the rest of the run, like upgrading a poker hand
    public enum Comp { Win, TwentyOne, Blackjack, Charlie, DoubleDown, SplitWin, DealerBust }

    public static class Comps
    {
        public static readonly IReadOnlyList<Comp> All = (Comp[])Enum.GetValues(typeof(Comp));

        public static string Name(Comp comp) => comp switch
        {
            Comp.TwentyOne => "21",
            Comp.DoubleDown => "Double Down",
            Comp.SplitWin => "Split Win",
            Comp.DealerBust => "Dealer Bust",
            _ => comp.ToString(),
        };

        public static (int chips, int mult) PerLevel(Comp comp) => comp switch
        {
            Comp.Win => (10, 1),
            Comp.TwentyOne => (15, 1),
            Comp.Blackjack => (20, 2),
            Comp.Charlie => (25, 2),
            Comp.DoubleDown => (10, 1),
            Comp.SplitWin => (15, 1),
            _ => (15, 1),
        };

        public static string Describe(Comp comp)
        {
            var (chips, mult) = PerLevel(comp);
            string when = comp switch
            {
                Comp.DoubleDown => " on doubled wins",
                Comp.SplitWin => " on each split hand that wins",
                Comp.DealerBust => " when the dealer busts",
                _ => $" on a {Name(comp)}",
            };
            return $"+{chips} Chips, +{mult} Mult{when}.";
        }

        // which Comps apply to a scored hand
        public static IEnumerable<Comp> AppliesTo(HandScore score)
        {
            if (!score.Outcome.IsWin()) yield break;
            switch (score.Outcome)
            {
                case Outcome.Win: yield return Comp.Win; break;
                case Outcome.TwentyOne: yield return Comp.TwentyOne; break;
                case Outcome.Blackjack: yield return Comp.Blackjack; break;
                case Outcome.Charlie: yield return Comp.Charlie; break;
            }
            if (score.Hand.IsDoubled) yield return Comp.DoubleDown;
            if (score.Hand.IsFromSplit) yield return Comp.SplitWin;
            if (score.DealerBusted) yield return Comp.DealerBust;
        }
    }

    // adds the levels of every Comp that applies, after the cards and before the Vices
    internal sealed class CompRules : TableEffect
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            foreach (var comp in Comps.AppliesTo(score))
            {
                int level = table.Gambler.CompLevel(comp);
                if (level == 0) continue;
                var (chips, mult) = Comps.PerLevel(comp);
                score.AddChips(chips * level);
                score.AddMult(mult * level);
            }
        }
    }
}
