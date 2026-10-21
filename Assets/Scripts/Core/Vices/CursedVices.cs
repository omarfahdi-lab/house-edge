namespace HouseEdge.Core.Vices
{
    // from the Pawn Shop and Lost and Found: stronger than normal, with a nasty downside. sell for 1 gold

    public sealed class DirtyMoney : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (!score.Outcome.IsWin()) return;
            score.AddMult(12);
            table.AddHeat(5);
        }
    }

    public sealed class DevilsDeal : Vice
    {
        public override void OnTableStart(Table table) => table.AddHands(-1);

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.MultiplyMult(3);
        }
    }

    public sealed class BadHabit : Vice
    {
        public override bool MustHitEveryHand => true;

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.AddChips(50);
        }
    }

    public sealed class BorrowedTime : Vice
    {
        public override void OnTableStart(Table table) => table.AddHands(2);

        // the two borrowed Hands are the last two you play
        public override void OnBeforeDeal(Table table)
        {
            if (table.HandsLeft <= 2) table.Gambler.LoseGold(2);
        }
    }
}
