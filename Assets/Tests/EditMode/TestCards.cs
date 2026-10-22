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

        public static Card C(string label, Enhancement enhancement, Stamp stamp = Stamp.None)
        {
            var card = C(label);
            card.Enhancement = enhancement;
            card.Stamp = stamp;
            return card;
        }

        public static Card C(string label, Stamp stamp) => C(label, Enhancement.None, stamp);

        // mix of plain labels and prepared cards (enhanced, stamped, cursed)
        public static Shoe StackOf(params object[] topFirst) =>
            Shoe.Stacked(topFirst.Select((o, i) => o as Card ?? Mixed((string)o, i)), new GameRandom(1));

        // a stacked deck mixes suits by position (spades, hearts, clubs, diamonds), so a test hand only makes a
        // Flush or Suited combo when the test asks for it with "7h"-style labels
        private static Card Mixed(string label, int index)
        {
            if ("shdc".IndexOf(label[label.Length - 1]) >= 0) return C(label);
            return new Card(C(label).Rank, new[] { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds }[index % 4]);
        }

        public static Table TableOf(params object[] topFirst) => new Table(StackOf(topFirst), long.MaxValue);

        public static HandValue Value(params string[] labels) => HandValue.Of(Cards(labels));

        // deal order is player, dealer up, player, dealer hole, then whatever is drawn next
        public static Shoe Stack(params string[] topFirst) => Shoe.Stacked(topFirst.Select(Mixed), new GameRandom(1));

        public static Table StackedTable(params string[] topFirst) => new Table(Stack(topFirst), Balance.FirstTableTarget);

        // a target nobody reaches, so the table keeps going
        public static Table StackedTableNoTarget(params string[] topFirst) => new Table(Stack(topFirst), long.MaxValue);

        public static void Play(Table table, params PlayerAction[] actions)
        {
            foreach (var action in actions) table.Apply(action);
        }
    }
}
