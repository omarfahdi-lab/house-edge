using System.Linq;

namespace HouseEdge.Core.Vices
{
    // Vices that build on combos: they only pay on a winning hand that made the right pattern

    public sealed class Matchmaker : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Combos.Contains(Combo.Pair)) score.AddMult(4);
            if (score.Combos.Contains(Combo.TwoPair)) score.AddMult(8);
            if (score.Combos.Contains(Combo.ThreeOfAKind)) score.AddMult(12);
        }
    }

    public sealed class StraightShooter : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Combos.Contains(Combo.Run)) score.MultiplyMult(2);
        }
    }

    public sealed class CourtJester : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Combos.Contains(Combo.RoyalCourt)) score.MultiplyMult(3);
        }
    }

    public sealed class FlushRoyale : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Combos.Contains(Combo.Flush)) score.MultiplyMult(2.5);
            if (score.Combos.Contains(Combo.Suited)) score.AddMult(6);
        }
    }

    public sealed class ComboKing : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            for (int i = 0; i < score.Combos.Count; i++) score.MultiplyMult(1.5);
        }
    }

    // suit Vices: count the cards of their suit in a winning hand. a Wild card counts as every suit
    public abstract class SuitVice : Vice
    {
        protected abstract Suit Suit { get; }

        protected int Count(HandScore score) =>
            score.Outcome.IsWin() ? score.Hand.Cards.Count(c => !c.IsCursed && (c.Suit == Suit || c.Enhancement == Enhancement.Wild)) : 0;
    }

    public sealed class CloverClub : SuitVice
    {
        protected override Suit Suit => Suit.Clubs;
        public override void OnHandScored(Table table, HandScore score) => score.AddMult(3 * Count(score));
    }

    public sealed class Heartbreaker : SuitVice
    {
        protected override Suit Suit => Suit.Hearts;
        public override void OnHandScored(Table table, HandScore score) => score.AddChips(15 * Count(score));
    }

    public sealed class DiamondMine : SuitVice
    {
        protected override Suit Suit => Suit.Diamonds;
        public override void OnHandScored(Table table, HandScore score) => table.Gambler.GainGold(Count(score));
    }

    public sealed class AceOfSpades : SuitVice
    {
        protected override Suit Suit => Suit.Spades;

        public override void OnHandScored(Table table, HandScore score)
        {
            for (int i = 0; i < Count(score); i++) score.MultiplyMult(1.2);
        }
    }

    // Pair, Two Pair and Three of a Kind pay their combo bonus a second time
    public sealed class DoubleVision : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            foreach (var combo in score.Combos)
            {
                if (combo != Combo.Pair && combo != Combo.TwoPair && combo != Combo.ThreeOfAKind) continue;
                var (chips, mult) = Combos.Base(combo);
                score.AddChips(chips);
                score.AddMult(mult);
            }
        }
    }

    public sealed class FullHouse : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            bool matched = score.Combos.Any(c => c == Combo.Pair || c == Combo.TwoPair || c == Combo.ThreeOfAKind);
            bool suited = score.Combos.Any(c => c == Combo.Suited || c == Combo.Flush);
            if (matched && suited) score.MultiplyMult(3);
        }
    }

    public sealed class Rainbow : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (!score.Outcome.IsWin()) return;
            if (score.Hand.Cards.Where(c => !c.IsCursed).Select(c => c.Suit).Distinct().Count() >= 3) score.AddMult(8);
        }
    }

    public sealed class CardTricks : Vice
    {
        public override void OnHandScored(Table table, HandScore score) => score.AddMult(3 * score.Combos.Count);
    }
}
