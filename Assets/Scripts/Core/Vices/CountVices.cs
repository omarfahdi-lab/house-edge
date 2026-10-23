namespace HouseEdge.Core.Vices
{
    // Vices for the count and raising, and the Legendaries that break a rule of blackjack outright

    public sealed class HiLo : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome.IsWin() && table.Count > 0) score.AddMult(3 * table.Count);
        }
    }

    public sealed class ColdDeck : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome.IsWin() && table.Count <= -2) score.AddChips(30);
        }
    }

    public sealed class BetSpreader : Vice
    {
        public override int RaiseRefund => 1;

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome.IsWin() && table.BetLevel > 1) score.MultiplyMult(1.5);
        }
    }

    public sealed class Tiebreaker : Vice
    {
        public override void OnTableStart(Table table) => table.Rules.Pushes = PushRule.PlayerWins;
    }

    public sealed class BottomlessBurns : Vice
    {
        public override void OnTableStart(Table table) => table.Rules.UnlimitedBurns = true;
        public override void OnBurn(Table table, PlayerHand hand) => table.AddHeat(4);
    }

    public sealed class FourCardCharlie : Vice
    {
        public override void OnTableStart(Table table) => table.Rules.CharlieCards = 4;
    }
}
