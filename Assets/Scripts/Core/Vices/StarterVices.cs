using System.Linq;

namespace HouseEdge.Core.Vices
{
    // the first 20 Vices (MVP step 3). each one only overrides the hooks it needs

    public sealed class LuckyPenny : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome.IsWin()) score.AddMult(5);
        }
    }

    public sealed class HotStreak : Vice
    {
        private int _streak;

        public override string Status => _streak > 0 ? $"+{_streak * 3} Mult" : null;

        public override void OnTableStart(Table table) => _streak = 0;

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome.IsWin())
            {
                _streak++;
                score.AddMult(3 * _streak);
            }
            else if (score.Outcome != Outcome.Push && score.Outcome != Outcome.Surrender) _streak = 0;
        }
    }

    public sealed class LowRoller : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (!score.ScoresCards) return;
            int low = score.Hand.Cards.Count(c => c.Rank >= Rank.Two && c.Rank <= Rank.Five);
            score.AddChips(8 * low);
        }
    }

    public sealed class HighRoller : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (!score.ScoresCards) return;
            int tens = score.Hand.Cards.Count(c => c.Rank >= Rank.Ten && c.Rank <= Rank.King);
            score.AddMult(2 * tens);
        }
    }

    public sealed class CardSharp : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards && score.Hand.Cards.Count >= 4) score.AddChips(25);
        }
    }

    public sealed class PhotoFinish : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (!score.Outcome.IsWin() || table.DealerValue.IsBust) return;
            if (score.Hand.Value.Total - table.DealerValue.Total == 1) score.AddMult(10);
        }
    }

    public sealed class SoreLoser : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome == Outcome.Lose || score.Outcome == Outcome.Bust) table.Gambler.GainGold(3);
        }
    }

    public sealed class DealersFriend : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.DealerBusted) score.AddChips(30);
        }
    }

    public sealed class PocketPair : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            var cards = score.Hand.Cards;
            if (score.ScoresCards && cards.Count >= 2 && cards[0].Rank == cards[1].Rank) score.AddMult(8);
        }
    }

    public sealed class SuitUp : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            var cards = score.Hand.Cards;
            if (score.ScoresCards && cards.All(c => c.Suit == cards[0].Suit)) score.AddMult(6);
        }
    }

    public sealed class PennyPincher : Vice
    {
        public override void OnTableEnd(Table table, bool won) => table.Gambler.GainGold(table.BurnsLeft);
    }

    public sealed class Espresso : Vice
    {
        public override void OnTableStart(Table table) => table.AddHands(1);
    }

    public sealed class SteadyHand : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome.IsWin() && score.Hand.StoodOn is int total && total <= 17) score.AddChips(20);
        }
    }

    public sealed class EvenMoney : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards && score.Hand.Value.Total % 2 == 0) score.AddMult(8);
        }
    }

    public sealed class CoinFlip : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards && table.Roll(1, 2)) score.AddMult(12);
        }
    }

    public sealed class IronWill : Vice
    {
        private int _lowStands;

        public override string Status => _lowStands > 0 ? $"+{_lowStands * 3} Mult" : null;

        public override void OnTableStart(Table table) => _lowStands = 0;

        public override void OnStand(Table table, PlayerHand hand)
        {
            if (hand.Value.Total <= 16) _lowStands++;
        }

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.AddMult(3 * _lowStands);
        }
    }

    public sealed class SoftTouch : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards && score.Hand.Value.IsSoft) score.MultiplyMult(2);
        }
    }

    public sealed class Bouncer : Vice
    {
        private bool _used;

        public override string Status => _used ? "used" : "ready";

        public override void OnTableStart(Table table) => _used = false;

        public override void OnBust(Table table, PlayerHand hand)
        {
            if (_used) return;
            _used = true;
            hand.RescueAsPush();
        }
    }

    public sealed class FreshFelt : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards && table.DealsPlayed == 1) score.MultiplyMult(3);
        }
    }

    public sealed class FaceValue : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (!score.ScoresCards) return;
            int faces = score.Hand.Cards.Count(c => c.Rank >= Rank.Jack && c.Rank <= Rank.King);
            score.AddChips(10 * faces);
            score.AddMult(2 * faces);
        }
    }
}
