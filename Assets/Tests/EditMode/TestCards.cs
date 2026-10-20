using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;

namespace HouseEdge.Tests
{
    internal static class TestCards
    {
        // "A", "10", "K", "7h"... suit defaults to spades
        public static Card C(string label)
        {
            char last = label[label.Length - 1];
            var suit = Suit.Spades;
            if ("shdc".IndexOf(last) >= 0)
            {
                suit = last switch { 'h' => Suit.Hearts, 'd' => Suit.Diamonds, 'c' => Suit.Clubs, _ => Suit.Spades };
                label = label.Substring(0, label.Length - 1);
            }
            var rank = label switch
            {
                "A" => Rank.Ace,
                "K" => Rank.King,
                "Q" => Rank.Queen,
                "J" => Rank.Jack,
                _ => (Rank)int.Parse(label),
            };
            return new Card(rank, suit);
        }

        public static List<Card> Cards(params string[] labels) => labels.Select(C).ToList();

        public static HandValue Value(params string[] labels) => HandValue.Of(Cards(labels));

        // deal order is player, dealer up, player, dealer hole, then whatever is drawn next
        public static Shoe Stack(params string[] topFirst) => Shoe.Stacked(Cards(topFirst), new GameRandom(1));

        public static Table StackedTable(params string[] topFirst) => new Table(Stack(topFirst), Balance.FirstTableTarget);

        // a target nobody reaches, so the table keeps going
        public static Table StackedTableNoTarget(params string[] topFirst) => new Table(Stack(topFirst), long.MaxValue);

        public static void Play(Table table, params PlayerAction[] actions)
        {
            foreach (var action in actions) table.Apply(action);
        }
    }
}
