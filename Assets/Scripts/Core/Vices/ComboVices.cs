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
}
