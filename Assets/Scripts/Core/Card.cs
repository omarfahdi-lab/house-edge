namespace HouseEdge.Core
{
    public enum Suit { Spades, Hearts, Diamonds, Clubs }

    public enum Rank
    {
        Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
        Jack, Queen, King, Ace
    }

    // a class, not a struct: each physical card will carry its own enhancements and stamps later
    public sealed class Card
    {
        public Rank Rank { get; }
        public Suit Suit { get; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public bool IsAce => Rank == Rank.Ace;
        public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;

        // aces count 11 here; HandValue drops them to 1 when the hand would bust
        public int Value => IsAce ? 11 : Rank >= Rank.Ten ? 10 : (int)Rank;

        public string RankLabel => Rank switch
        {
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)Rank).ToString(),
        };

        public string SuitSymbol => Suit switch
        {
            Suit.Spades => "♠",
            Suit.Hearts => "♥",
            Suit.Diamonds => "♦",
            _ => "♣",
        };

        public override string ToString() => RankLabel + SuitSymbol;
    }
}
