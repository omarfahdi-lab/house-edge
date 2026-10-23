using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    // the guided first run: the first three hands of the first table are stacked so each one teaches something.
    // cards come out you, dealer up, you, dealer hole, then any hits
    public static class Tutorial
    {
        public static readonly IReadOnlyList<(Rank rank, Suit suit)> Script = new[]
        {
            // 1. a plain win: 19 against the dealer's 17. Stand
            (Rank.Ten, Suit.Clubs), (Rank.Ten, Suit.Diamonds), (Rank.Nine, Suit.Spades), (Rank.Seven, Suit.Hearts),
            // 2. a Burn: 16 against 18, Hit and bust on a King, Burn it for a 4 and Stand on 20
            (Rank.Ten, Suit.Spades), (Rank.Nine, Suit.Diamonds), (Rank.Six, Suit.Diamonds), (Rank.Nine, Suit.Clubs),
            (Rank.King, Suit.Clubs), (Rank.Four, Suit.Hearts),
            // 3. a combo: two Hearts, Hit a third Heart for a Flush and 20 against 17. it clears the table on its own
            (Rank.Five, Suit.Hearts), (Rank.Jack, Suit.Spades), (Rank.Six, Suit.Hearts), (Rank.Seven, Suit.Clubs),
            (Rank.Nine, Suit.Hearts),
        };

        // the scripted cards on top, taken out of your own deck, then the rest shuffled under them
        public static Shoe Shoe(IReadOnlyList<Card> deck, GameRandom random)
        {
            var rest = deck.ToList();
            var top = new List<Card>();
            foreach (var (rank, suit) in Script)
            {
                var card = rest.FirstOrDefault(c => !c.IsCursed && c.Rank == rank && c.Suit == suit) ?? new Card(rank, suit);
                rest.Remove(card);
                top.Add(card);
            }
            return Core.Shoe.StackedOnShuffled(top, rest, random);
        }
    }
}
