using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    public readonly struct HandValue
    {
        public int Total { get; }
        // an ace is still counted as 11
        public bool IsSoft { get; }
        public int CardCount { get; }
        public int BustLimit { get; }
        // how many cards make a Charlie (Four-Card Charlie lowers it)
        public int CharlieCards { get; }

        private HandValue(int total, bool isSoft, int cardCount, int bustLimit, int charlieCards = Balance.CharlieCards)
        {
            Total = total;
            IsSoft = isSoft;
            CardCount = cardCount;
            BustLimit = bustLimit;
            CharlieCards = charlieCards;
        }

        public bool IsBust => Total > BustLimit;
        public bool IsCharlie => CardCount >= CharlieCards && !IsBust;

        // a natural: two cards making 21. split hands never count, see PlayerHand.IsBlackjack
        public bool IsTwoCard21 => CardCount == 2 && Total == Balance.BustLimit;

        // a hand an effect set to a fixed total, e.g. a bust saved as 21
        public static HandValue Forced(int total, int cardCount) => new HandValue(total, false, cardCount, Math.Max(total, Balance.BustLimit));

        public static HandValue Of(IReadOnlyList<Card> cards) => Of(cards, Balance.BustLimit, false);

        // aces count 11 while that fits under the limit, then Flex cards fill the gap up to it
        public static HandValue Of(IReadOnlyList<Card> cards, int bustLimit, bool acesAlwaysOne, int charlieCards = Balance.CharlieCards)
        {
            int total = 0;
            int aces = 0;
            int flex = 0;
            foreach (var card in cards)
            {
                total += card.LowValue;
                if (card.IsAce) aces++;
                if (card.IsFlex) flex++;
            }

            int softAces = 0;
            if (!acesAlwaysOne)
                for (int i = 0; i < aces && total + 10 <= bustLimit; i++)
                {
                    total += 10;
                    softAces++;
                }
            for (int i = 0; i < flex && total < bustLimit; i++) total += Math.Min(10, bustLimit - total);

            return new HandValue(total, softAces > 0, cards.Count, bustLimit, charlieCards);
        }

        public override string ToString() => IsSoft ? $"soft {Total}" : Total.ToString();
    }
}
