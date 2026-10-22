using System;
using System.Collections.Generic;

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

        // wins and pushes score their cards. losses, busts and surrenders score nothing
        public bool ScoresCards => Balance.ScoresCards(Outcome);

        internal HandScore(PlayerHand hand, Outcome outcome, bool dealerBusted)
        {
            Hand = hand;
            Outcome = outcome;
            DealerBusted = dealerBusted;
        }

        // how many Vices changed this score
        public int ViceTriggers { get; internal set; }

        public void AddChips(int chips) => Chips += chips;
        public void AddMult(double mult) => Mult += mult;

        public void MultiplyMult(double factor)
        {
            Mult *= factor;
            _factor *= factor;
        }

        // every change to Chips or Mult, in the order it happened, so the game can play the scoring back step by step
        public IReadOnlyList<ScoreStep> Steps => _steps;

        private readonly List<ScoreStep> _steps = new List<ScoreStep>();
        private int _markedChips;
        private double _markedMult;
        private double _factor = 1;

        // records what changed since the last mark, if anything, and credits it to source
        internal void Mark(ScoreSource kind, string name, object source = null, int card = -1)
        {
            if (Chips == _markedChips && Mult == _markedMult) return;
            _steps.Add(new ScoreStep(kind, name, source, card, Chips - _markedChips, Mult - _markedMult, _factor, Chips, Mult));
            _markedChips = Chips;
            _markedMult = Mult;
            _factor = 1;
        }

        public override string ToString() => $"{Outcome.Label()}: {Chips} Chips x {Mult:0.##} Mult = {Total}";
    }

    public enum ScoreSource { Hand, Bonus, Card, Comp, Boss, Dealer, Rule, Vice, Double, Ante }

    // one beat of scoring: who changed the hand and how
    public readonly struct ScoreStep
    {
        public readonly ScoreSource Kind;
        public readonly string Name;
        // the Vice, boss or dealer that did it, if any
        public readonly object Source;
        // which card of the hand, for card effects. -1 otherwise
        public readonly int Card;
        public readonly int ChipsAdded;
        public readonly double MultAdded;
        // anything that multiplied Mult in this step, 1 if nothing did
        public readonly double Factor;
        public readonly int Chips;
        public readonly double Mult;

        public ScoreStep(ScoreSource kind, string name, object source, int card, int chipsAdded, double multAdded, double factor, int chips, double mult)
        {
            Kind = kind;
            Name = name;
            Source = source;
            Card = card;
            ChipsAdded = chipsAdded;
            MultAdded = multAdded;
            Factor = factor;
            Chips = chips;
            Mult = mult;
        }

        public bool Multiplied => Math.Abs(Factor - 1) > 1e-9;
    }
}
