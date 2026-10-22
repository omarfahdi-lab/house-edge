using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    // poker-style patterns inside a blackjack hand. they only pay on a hand that beats the dealer, and they stack
    public enum Combo { Pair, TwoPair, ThreeOfAKind, Suited, Flush, Run, RoyalCourt, LowBall }

    public static class Combos
    {
        public static readonly IReadOnlyList<Combo> All = (Combo[])System.Enum.GetValues(typeof(Combo));

        public static string Name(Combo combo) => combo switch
        {
            Combo.TwoPair => "Two Pair",
            Combo.ThreeOfAKind => "Three of a Kind",
            Combo.RoyalCourt => "Royal Court",
            Combo.LowBall => "Low Ball",
            _ => combo.ToString(),
        };

        public static string Describe(Combo combo) => combo switch
        {
            Combo.Pair => "Two cards of the same rank.",
            Combo.TwoPair => "Two different pairs.",
            Combo.ThreeOfAKind => "Three cards of the same rank.",
            Combo.Suited => "A two-card hand of one suit.",
            Combo.Flush => "Three or more cards, all one suit.",
            Combo.Run => "Three or more cards in a row, like 5-6-7. Aces go high or low.",
            Combo.RoyalCourt => "A Jack, a Queen and a King.",
            Combo.LowBall => "Four or more cards, all 5 or lower.",
            _ => "",
        };

        // what a combo adds before any Comps level it up
        public static (int chips, int mult) Base(Combo combo) => combo switch
        {
            Combo.Pair => (10, 1),
            Combo.TwoPair => (20, 2),
            Combo.ThreeOfAKind => (35, 3),
            Combo.Suited => (10, 1),
            Combo.Flush => (30, 3),
            Combo.Run => (30, 3),
            Combo.RoyalCourt => (40, 4),
            _ => (40, 4),
        };

        // every combo the cards make. the bigger version of a pattern replaces the smaller one
        // (Three of a Kind over Pair, Flush over Suited). curses don't count toward anything
        public static List<Combo> Find(IReadOnlyList<Card> hand)
        {
            var found = new List<Combo>();
            var cards = hand.Where(c => !c.IsCursed).ToList();
            if (cards.Count < 2) return found;

            var groups = cards.GroupBy(c => c.Rank).Select(g => g.Count()).ToList();
            int pairs = groups.Count(n => n == 2);
            if (groups.Any(n => n >= 3)) found.Add(Combo.ThreeOfAKind);
            else if (pairs >= 2) found.Add(Combo.TwoPair);
            else if (pairs == 1) found.Add(Combo.Pair);

            if (cards.All(c => c.Suit == cards[0].Suit)) found.Add(cards.Count >= 3 ? Combo.Flush : Combo.Suited);

            if (LongestRun(cards) >= 3) found.Add(Combo.Run);
            if (cards.Any(c => c.Rank == Rank.Jack) && cards.Any(c => c.Rank == Rank.Queen) && cards.Any(c => c.Rank == Rank.King))
                found.Add(Combo.RoyalCourt);
            if (cards.Count >= 4 && cards.All(c => c.Rank <= Rank.Five || c.Rank == Rank.Ace)) found.Add(Combo.LowBall);
            return found;
        }

        // the longest stretch of ranks in a row, with Aces either low (A-2-3) or high (Q-K-A)
        private static int LongestRun(List<Card> cards)
        {
            var ranks = new HashSet<int>(cards.Select(c => (int)c.Rank));
            if (ranks.Contains((int)Rank.Ace)) ranks.Add(1);
            int best = 0;
            foreach (int start in ranks)
            {
                if (ranks.Contains(start - 1)) continue;
                int length = 1;
                while (ranks.Contains(start + length)) length++;
                if (length > best) best = length;
            }
            return best;
        }
    }
}
