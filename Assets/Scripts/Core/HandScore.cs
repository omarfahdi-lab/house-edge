using System;

namespace HouseEdge.Core
{
    // Chips x Mult for one player hand. effects change it in OnHandScored, in Vice order
    public sealed class HandScore
    {
        public PlayerHand Hand { get; }
        public Outcome Outcome { get; }
        public bool DealerBusted { get; }

        public int Chips { get; private set; }
        public double Mult { get; private set; }

        public long Total => (long)Math.Floor(Chips * Mult);

        internal HandScore(PlayerHand hand, Outcome outcome, bool dealerBusted)
        {
            Hand = hand;
            Outcome = outcome;
            DealerBusted = dealerBusted;
        }

        public void AddChips(int chips) => Chips += chips;
        public void AddMult(double mult) => Mult += mult;
        public void MultiplyMult(double factor) => Mult *= factor;

        public override string ToString() => $"{Outcome.Label()}: {Chips} Chips x {Mult:0.##} Mult = {Total}";
    }
}
