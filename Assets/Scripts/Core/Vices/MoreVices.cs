using System;
using System.Linq;

namespace HouseEdge.Core.Vices
{
    // the rest of the Vices from DESIGN.md (MVP step 6)

    // commons

    public sealed class PokerFace : Vice
    {
        public override double HeatFactor => 0.6;
    }

    public sealed class SideHustle : Vice
    {
        public override int SideBetDiscount => 1;
    }

    // uncommons

    public sealed class CardCounter : Vice
    {
        public override void OnTableStart(Table table) => table.Rules.SeeNextCard = true;
        public override void OnDeal(Table table) => table.AddHeat(2);
    }

    public sealed class XRaySpecs : Vice
    {
        public override void OnTableStart(Table table) => table.Rules.HoleCardFaceUp = true;
        public override void OnDeal(Table table) => table.AddHeat(3);
    }

    public sealed class SplitPersonality : Vice
    {
        public override void OnTableStart(Table table) => table.Rules.SplitAnyTwo = true;
    }

    public sealed class StretchLimo : Vice
    {
        public override void OnTableStart(Table table) => table.Rules.PlayerBustLimit++;
    }

    public sealed class InsuranceAgent : Vice
    {
        public override void OnDeal(Table table)
        {
            if (table.DealerUpCard.IsAce) table.Gambler.GainGold(4);
        }
    }

    public sealed class Bankroll : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.AddMult(table.Gambler.Gold / 5);
        }
    }

    public sealed class Burnout : Vice
    {
        private int _mult;

        public override string Status => _mult > 0 ? $"+{_mult} Mult" : null;
        public override void OnTableStart(Table table) => _mult = 0;
        public override void OnBurn(Table table, PlayerHand hand) => _mult += 4 * Growth(table);

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.AddMult(_mult);
        }
    }

    public sealed class Pickpocket : Vice
    {
        public override void OnDealerBust(Table table)
        {
            var copy = table.DealerCards[table.DealerCards.Count - 1].Copy();
            copy.Enhancement = Enhancement.Gilded;
            table.Gambler.Deck.Add(copy);
            table.AddHeat(3);
        }
    }

    public sealed class DoublingCube : Vice
    {
        private int _mult;

        public override string Status => _mult > 0 ? $"+{_mult} Mult" : null;

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.AddMult(_mult);
            if (score.Hand.IsDoubled && score.Outcome.IsWin()) _mult += Growth(table);
        }
    }

    public sealed class SleightOfHand : Vice
    {
        public override void OnBurn(Table table, PlayerHand hand)
        {
            if (hand.BurnsUsed != 1) return;
            table.AddBurns(1);
            table.AddHeat(2);
        }
    }

    public sealed class TellReader : Vice
    {
        public override bool RevealsTells => true;
    }

    public sealed class Bookie : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (table.SideBetResult is SideBetResult bet && bet.Won && score.ScoresCards) score.AddMult(5);
        }
    }

    public sealed class Daredevil : Vice
    {
        private PlayerHand _hand;
        private int _riskyHits;

        public override void OnHit(Table table, PlayerHand hand)
        {
            if (hand != _hand)
            {
                _hand = hand;
                _riskyHits = 0;
            }
            var before = HandValue.Of(hand.Cards.Take(hand.Cards.Count - 1).ToList(), table.Rules.PlayerBustLimit, table.Rules.AcesAlwaysOne);
            if (before.Total >= 15) _riskyHits++;
        }

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Hand == _hand && score.ScoresCards) score.AddMult(4 * _riskyHits);
        }
    }

    public sealed class Horseshoe : Vice
    {
        public override void OnBust(Table table, PlayerHand hand)
        {
            if (table.Roll(1, 4)) hand.RescueAs21();
        }
    }

    public sealed class Snowball : Vice
    {
        private int _chips;

        public override string Status => _chips > 0 ? $"+{_chips} Chips" : null;

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome == Outcome.Bust)
            {
                _chips = 0;
                return;
            }
            if (score.ScoresCards) score.AddChips(_chips);
            if (score.Outcome.IsWin()) _chips += 7 * Growth(table);
        }
    }

    public sealed class Patience : Vice
    {
        private int _mult;

        public override string Status => _mult > 0 ? $"+{_mult} Mult" : null;

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.AddMult(_mult);
            if (score.Outcome.IsWin() && !score.Hand.HasActed) _mult += Growth(table);
        }
    }

    // rares

    public sealed class RiggedShoe : Vice
    {
        public override void OnTableStart(Table table) => table.Rules.DealerStandsOn = Math.Max(table.Rules.DealerStandsOn, 18);
        public override void OnDeal(Table table) => table.AddHeat(3);
    }

    public sealed class LuckySevens : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (!score.ScoresCards) return;
            int sevens = score.Hand.Cards.Count(c => !c.IsCursed && c.Rank == Rank.Seven);
            if (sevens >= 3) score.MultiplyMult(7);
            else for (int i = 0; i < sevens; i++) score.MultiplyMult(1.5);
        }
    }

    // copies the Vice to its right
    public sealed class Mirror : Vice
    {
        private Vice Right(Table table)
        {
            var vices = table.Gambler.Vices;
            int index = vices.IndexOf(this);
            if (index < 0 || index + 1 >= vices.Count) return null;
            var right = vices[index + 1];
            return right is Mirror || right.Disabled ? null : right;
        }

        public override void OnTableStart(Table table) => Right(table)?.OnTableStart(table);
        public override void OnDeal(Table table) => Right(table)?.OnDeal(table);
        public override void OnHit(Table table, PlayerHand hand) => Right(table)?.OnHit(table, hand);
        public override void OnStand(Table table, PlayerHand hand) => Right(table)?.OnStand(table, hand);
        public override void OnBurn(Table table, PlayerHand hand) => Right(table)?.OnBurn(table, hand);
        public override void OnBust(Table table, PlayerHand hand) => Right(table)?.OnBust(table, hand);
        public override void OnDealerBust(Table table) => Right(table)?.OnDealerBust(table);
        public override void OnHandScored(Table table, HandScore score) => Right(table)?.OnHandScored(table, score);
        public override void OnTableEnd(Table table, bool won) => Right(table)?.OnTableEnd(table, won);
    }

    public sealed class BlackCat : Vice
    {
        private bool _armed;
        private bool _bustThisDeal;

        public override string Status => _armed ? "x3 ready" : null;

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome == Outcome.Bust) _bustThisDeal = true;
            if (_armed && score.ScoresCards) score.MultiplyMult(3);
        }

        public override void OnDealEnd(Table table)
        {
            _armed = _bustThisDeal;
            _bustThisDeal = false;
        }
    }

    public sealed class AllIn : Vice
    {
        public override bool BlocksInterest => true;

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.MultiplyMult(3);
        }
    }

    public sealed class LastCall : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards && table.HandsLeft == 1) score.MultiplyMult(3);
        }
    }

    public sealed class TwinsVice : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards && score.Hand.IsFromSplit) score.MultiplyMult(2);
        }
    }

    public sealed class ColdRead : Vice
    {
        public override DealerControl DealerControl => DealerControl.ColdRead;
    }

    public sealed class CardMechanicVice : Vice
    {
        public override bool GrantsSwap => true;
    }

    public sealed class SmoothTalker : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.AddMult(table.Gambler.Heat / 3);
        }
    }

    public sealed class KnifesEdge : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (!score.ScoresCards) return;
            int total = score.Hand.Value.Total;
            if (total == Balance.BustLimit) score.MultiplyMult(3);
            else if (total <= 18) score.MultiplyMult(0.5);
        }
    }

    public sealed class DoubleOrBust : Vice
    {
        public override void OnTableStart(Table table) => table.Rules.DoubleAfterHit = true;
    }

    public sealed class RabbitsFoot : Vice
    {
        public override bool RollsTwice => true;
    }

    public sealed class LoanOfficer : Vice
    {
        public override bool FreezesDebt => true;
    }

    public sealed class Cashier : Vice
    {
        public override int GoldPerUnusedHand => 3;
    }

    public sealed class Echo : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Hand.Cards.Count > 0) CardRules.ScoreCardAgain(table, score, score.Hand.Cards[0]);
        }
    }

    // legendaries

    public sealed class Midas : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            if (!score.Outcome.IsWin()) return;
            foreach (var card in score.Hand.Cards)
                if (!card.IsCursed) card.Enhancement = Enhancement.Gilded;
        }
    }

    public sealed class NaturalBorn : Vice
    {
        public override bool BlackjacksCantBeTied => true;

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.Outcome == Outcome.Blackjack) score.MultiplyMult(5);
        }
    }

    public sealed class LadyLuck : Vice
    {
        public override int ModifyChances(int chances) => chances * 2;
        public override bool LuckyShop => true;
    }

    public sealed class HighStakes : Vice
    {
        private double _x = 1;

        public override string Status => _x > 1 ? $"x{_x:0.##} Mult" : null;

        public override void OnHit(Table table, PlayerHand hand)
        {
            var before = HandValue.Of(hand.Cards.Take(hand.Cards.Count - 1).ToList(), table.Rules.PlayerBustLimit, table.Rules.AcesAlwaysOne);
            if (before.Total >= 17 && !hand.Value.IsBust) _x += 0.25 * Growth(table);
        }

        public override void OnHandScored(Table table, HandScore score)
        {
            if (score.ScoresCards) score.MultiplyMult(_x);
        }
    }

    public sealed class TheDealerVice : Vice
    {
        public override DealerControl DealerControl => DealerControl.Full;
    }

    public sealed class Whale : Vice
    {
        public override void OnHandScored(Table table, HandScore score)
        {
            int x = table.Gambler.Gold / 10;
            if (score.ScoresCards && x > 1) score.MultiplyMult(x);
        }
    }

    public sealed class ChainReaction : Vice
    {
        public override void OnAfterScoring(Table table, HandScore score)
        {
            if (score.ScoresCards && score.ViceTriggers > 0) score.MultiplyMult(Math.Pow(1.1, score.ViceTriggers));
        }
    }
}
