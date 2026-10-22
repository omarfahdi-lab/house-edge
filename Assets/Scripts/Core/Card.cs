namespace HouseEdge.Core
{
    public enum Suit { Spades, Hearts, Diamonds, Clubs }

    public enum Rank
    {
        Two = 2, Three, Four, Five, Six, Seven, Eight, Nine, Ten,
        Jack, Queen, King, Ace
    }

    public enum Enhancement { None, Gilded, Flex, Fragile, Marked, Ghost, Hot, Heavy }

    public enum Stamp { None, Red, Blue, Gold, Black }

    // bad cards bosses and cursed effects shuffle into your deck
    public enum Curse { None, DeadCard, SnakeEyes, TaxCard, Jinx }

    // a class, not a struct: each physical card carries its own enhancement, stamp and counters
    public sealed class Card
    {
        public Rank Rank { get; internal set; }
        public Suit Suit { get; }
        public Curse Curse { get; }
        public Enhancement Enhancement { get; set; }
        public Stamp Stamp { get; set; }

        // winning hands a Fragile card has been in
        public int FragileWins { get; internal set; }
        // for Hot cards, reset when a table starts
        public int TimesDrawnThisTable { get; internal set; }
        // broke or was removed mid-table; it won't go back into the shoe
        public bool Destroyed { get; internal set; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        private Card(Curse curse)
        {
            Curse = curse;
            Rank = Rank.Two;
            Suit = Suit.Spades;
        }

        public static Card Cursed(Curse curse) => new Card(curse);

        // a fresh copy for Martini or Pickpocket. counters start over
        public Card Copy()
        {
            var copy = Curse != Curse.None ? new Card(Curse) : new Card(Rank, Suit);
            copy.Enhancement = Enhancement;
            copy.Stamp = Stamp;
            return copy;
        }

        public bool IsCursed => Curse != Curse.None;
        public bool IsAce => !IsCursed && Rank == Rank.Ace && Enhancement != Enhancement.Flex && Enhancement != Enhancement.Ghost;
        public bool IsFlex => !IsCursed && Enhancement == Enhancement.Flex;
        public bool IsRed => !IsCursed && (Suit == Suit.Hearts || Suit == Suit.Diamonds);
        public bool IsFace => !IsCursed && Rank >= Rank.Jack && Rank <= Rank.King;
        public bool IsTenValue => !IsCursed && Rank >= Rank.Ten && Rank <= Rank.King;

        // the lowest this card can count: aces and Flex cards start at 1 and HandValue raises them
        public int LowValue
        {
            get
            {
                switch (Curse)
                {
                    case Curse.DeadCard: return 10;
                    case Curse.SnakeEyes: return 1;
                    case Curse.TaxCard:
                    case Curse.Jinx: return 2;
                }
                if (Enhancement == Enhancement.Ghost) return 0;
                if (Enhancement == Enhancement.Flex) return 1;
                int value = Rank == Rank.Ace ? 1 : Rank >= Rank.Ten ? 10 : (int)Rank;
                return Enhancement == Enhancement.Heavy ? value + 1 : value;
            }
        }

        // what the card is worth on its own, aces as 11. what a dealer's up card "shows"
        public int Value => IsAce ? LowValue + 10 : IsFlex ? 11 : LowValue;

        public string RankLabel
        {
            get
            {
                switch (Curse)
                {
                    case Curse.DeadCard: return "DEAD";
                    case Curse.SnakeEyes: return "1";
                    case Curse.TaxCard: return "TAX";
                    case Curse.Jinx: return "JINX";
                }
                return Rank switch
                {
                    Rank.Jack => "J",
                    Rank.Queen => "Q",
                    Rank.King => "K",
                    Rank.Ace => "A",
                    _ => ((int)Rank).ToString(),
                };
            }
        }

        // for logs and tests. the game draws suits as shapes (no font has them on WebGL)
        public string SuitSymbol => IsCursed ? "☠" : Suit switch
        {
            Suit.Spades => "♠",
            Suit.Hearts => "♥",
            Suit.Diamonds => "♦",
            _ => "♣",
        };

        public string CurseName => Curse switch
        {
            Curse.DeadCard => "Dead Card",
            Curse.SnakeEyes => "Snake Eyes",
            Curse.TaxCard => "Tax Card",
            Curse.Jinx => "Jinx",
            _ => "",
        };

        public override string ToString() => IsCursed ? CurseName : RankLabel + SuitSymbol;
    }
}
