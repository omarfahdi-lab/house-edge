using System.Collections.Generic;

namespace HouseEdge.Core
{
    public sealed class PlayerHand
    {
        private readonly List<Card> _cards = new List<Card>();

        public IReadOnlyList<Card> Cards => _cards;
        public HandValue Value => _rescuedAs21 ? HandValue.Forced(Balance.BustLimit, _cards.Count) : HandValue.Of(_cards);
        private bool _rescuedAs21;

        public bool IsFromSplit { get; internal set; }
        public bool IsDoubled { get; internal set; }
        // stood, doubled, charlied or accepted a bust. no more actions on it
        public bool IsDone { get; internal set; }
        // the last card came from Hit (or Burn), so Burn can replace it
        public bool LastCardWasHit { get; internal set; }
        public bool HasActed { get; internal set; }
        // the total the player stood on, or null if they didn't stand
        public int? StoodOn { get; internal set; }
        // a bust that an effect turned into a push
        public bool RescuedAsPush { get; private set; }

        // set when the deal is resolved
        public HandScore Score { get; internal set; }

        internal PlayerHand(bool isFromSplit)
        {
            IsFromSplit = isFromSplit;
        }

        // split hands that make 21 with two cards score as 21, not Blackjack
        public bool IsBlackjack => !IsFromSplit && Value.IsTwoCard21;

        // for effects in OnBust
        public void RescueAs21() => _rescuedAs21 = true;
        public void RescueAsPush() => RescuedAsPush = true;

        internal void Add(Card card) => _cards.Add(card);

        internal Card RemoveLast()
        {
            var card = _cards[_cards.Count - 1];
            _cards.RemoveAt(_cards.Count - 1);
            return card;
        }
    }
}
