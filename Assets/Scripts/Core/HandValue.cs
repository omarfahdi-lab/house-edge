using System.Collections.Generic;

namespace HouseEdge.Core
{
    public readonly struct HandValue
    {
        public int Total { get; }
        // an ace is still counted as 11
        public bool IsSoft { get; }
        public int CardCount { get; }

        private HandValue(int total, bool isSoft, int cardCount)
        {
            Total = total;
            IsSoft = isSoft;
            CardCount = cardCount;
        }

        public bool IsBust => Total > Balance.BustLimit;
        public bool IsCharlie => CardCount >= Balance.CharlieCards && !IsBust;

        // a natural: ace + 10-value as the first two cards. split hands never count, see Table
        public bool IsTwoCard21 => CardCount == 2 && Total == Balance.BustLimit;

        // a hand an effect set to a fixed total, e.g. a bust saved as 21
        public static HandValue Forced(int total, int cardCount) => new HandValue(total, false, cardCount);

        public static HandValue Of(IReadOnlyList<Card> cards)
        {
            int total = 0;
            int softAces = 0;
            foreach (var card in cards)
            {
                total += card.Value;
                if (card.IsAce) softAces++;
            }
            // drop aces from 11 to 1 one at a time until the hand fits
            while (total > Balance.BustLimit && softAces > 0)
            {
                total -= 10;
                softAces--;
            }
            return new HandValue(total, softAces > 0, cards.Count);
        }

        public override string ToString() => IsSoft ? $"soft {Total}" : Total.ToString();
    }
}
