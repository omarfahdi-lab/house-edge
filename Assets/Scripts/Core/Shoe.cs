using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    // the one deck the player and the dealer both draw from. shuffled when a table starts, not each hand
    public sealed class Shoe
    {
        // top of the deck is the end of the list
        private readonly List<Card> _drawPile;
        private readonly List<Card> _discards = new List<Card>();
        private readonly GameRandom _random;

        private Shoe(List<Card> drawPile, GameRandom random)
        {
            _drawPile = drawPile;
            _random = random;
        }

        public int Count => _drawPile.Count;
        public int DiscardCount => _discards.Count;

        public static List<Card> StandardDeck()
        {
            var cards = new List<Card>(52);
            foreach (Suit suit in new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs })
                for (var rank = Rank.Two; rank <= Rank.Ace; rank++)
                    cards.Add(new Card(rank, suit));
            return cards;
        }

        public static Shoe Shuffled(IEnumerable<Card> deck, GameRandom random)
        {
            var shoe = new Shoe(new List<Card>(deck), random);
            shoe.Shuffle();
            return shoe;
        }

        // cards come out in the given order, first card first. for tests and scripted tables
        public static Shoe Stacked(IEnumerable<Card> topFirst, GameRandom random)
        {
            var cards = new List<Card>(topFirst);
            cards.Reverse();
            return new Shoe(cards, random);
        }

        public Card Draw()
        {
            if (_drawPile.Count == 0)
            {
                if (_discards.Count == 0) throw new InvalidOperationException("The shoe and the discards are both empty.");
                _drawPile.AddRange(_discards);
                _discards.Clear();
                Shuffle();
            }
            var card = _drawPile[_drawPile.Count - 1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            return card;
        }

        public Card PeekTop() => _drawPile.Count > 0 ? _drawPile[_drawPile.Count - 1] : null;

        public void Discard(Card card) => _discards.Add(card);

        public void Discard(IEnumerable<Card> cards) => _discards.AddRange(cards);

        // fisher-yates
        private void Shuffle()
        {
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }
    }
}
