using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    // cards the casino shuffles into the shoe for one table. each one is good in one hand and bad in the other,
    // so who ends up holding it decides which way it cuts
    public static class HouseCards
    {
        public static readonly IReadOnlyList<HouseCard> All =
            ((HouseCard[])Enum.GetValues(typeof(HouseCard))).Where(h => h != HouseCard.None).ToList();

        public static string Name(HouseCard card) => card switch
        {
            HouseCard.LoadedDie => "Loaded Die",
            _ => card.ToString(),
        };

        public static string Describe(HouseCard card) => card switch
        {
            HouseCard.Bounty => $"Yours: win and get +{Balance.BountyGold} gold and x{Balance.BountyMult} Mult, lose and it costs {Balance.BountyHandsLost} extra Hand. " +
                                $"The dealer's: +{Balance.BountyDealerGold} gold for every hand that beats him.",
            HouseCard.Mirror => "Yours: copies the dealer's face-up card. The dealer's: copies your first card.",
            HouseCard.Fuse => "Yours: you can take only 1 more card after it. The dealer's: he stops drawing, even below 17.",
            HouseCard.LoadedDie => "Yours: your total counts 1 higher at the showdown, never past 21. The dealer's: his counts 1 higher, and 21 becomes a bust.",
            HouseCard.Snitch => $"Yours: +{Balance.SnitchHeat} Heat. The dealer's: his hole card is turned face up.",
            HouseCard.Leech => $"Yours: +{Balance.LeechMult} Mult on a win. The dealer's: it steals 1 of your Burns.",
            _ => "",
        };

        public static bool Has(IEnumerable<Card> cards, HouseCard kind) => cards.Any(c => c.House == kind);

        // a Fuse in your hand allows one more card, then no more
        public static bool FuseSpent(IReadOnlyList<Card> cards)
        {
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].House == HouseCard.Fuse) return cards.Count - 1 - i >= 1;
            return false;
        }

        // a hand's total at the showdown, with a Loaded Die in it
        public static int ShowdownTotal(IReadOnlyList<Card> cards, int total, int limit) =>
            Has(cards, HouseCard.LoadedDie) && total + 1 <= limit ? total + 1 : total;
    }

    // shuffles this table's House cards into the shoe and plays their effects
    internal sealed class HouseRules : TableEffect
    {
        private readonly int _count;

        public HouseRules(int count) => _count = count;

        public override void OnTableStart(Table table)
        {
            for (int i = 0; i < _count; i++)
            {
                var kind = HouseCards.All[table.Random.Next(HouseCards.All.Count)];
                var rank = (Rank)(2 + table.Random.Next(13));
                var suit = (Suit)table.Random.Next(4);
                table.ShuffleIntoShoe(Card.OfHouse(rank, suit, kind), addToDeck: false);
            }
        }

        public override void OnCardDrawn(Table table, Card card, bool toDealer)
        {
            switch (card.House)
            {
                case HouseCard.Mirror:
                    Reflect(table, card, toDealer);
                    break;
                case HouseCard.Snitch when toDealer:
                    table.RevealHoleCard();
                    table.Note("The Snitch talks: the dealer's hole card is face up.");
                    break;
                case HouseCard.Snitch:
                    table.AddHeat(Balance.SnitchHeat);
                    table.Note($"You drew the Snitch: +{Balance.SnitchHeat} Heat.");
                    break;
                case HouseCard.Leech when toDealer && table.BurnsLeft > 0:
                    table.AddBurns(-1);
                    table.Note("The dealer's Leech steals a Burn.");
                    break;
            }
        }

        // the opening cards are all out now, so a Mirror dealt before its target can copy it
        public override void OnDeal(Table table)
        {
            foreach (var card in table.DealerCards)
                if (card.House == HouseCard.Mirror) Reflect(table, card, true);
            foreach (var hand in table.PlayerHands)
                foreach (var card in hand.Cards)
                    if (card.House == HouseCard.Mirror) Reflect(table, card, false);
        }

        private static void Reflect(Table table, Card mirror, bool toDealer)
        {
            var hands = table.PlayerHands;
            var target = toDealer
                ? hands.Count > 0 && hands[0].Cards.Count > 0 ? hands[0].Cards[0] : null
                : table.DealerUpCard;
            if (target == null || target == mirror || target.IsCursed) return;
            mirror.Rank = target.Rank;
        }

        public override void OnHandScored(Table table, HandScore score)
        {
            var cards = score.Hand.Cards;
            bool win = score.Outcome.IsWin();
            bool lost = score.Outcome == Outcome.Lose || score.Outcome == Outcome.Bust;
            if (HouseCards.Has(cards, HouseCard.Bounty))
            {
                if (win)
                {
                    table.Gambler.GainGold(Balance.BountyGold);
                    score.MultiplyMult(Balance.BountyMult);
                }
                else if (lost)
                {
                    table.AddHands(-Balance.BountyHandsLost);
                    table.Note($"The Bounty was on your head: -{Balance.BountyHandsLost} Hand.");
                }
            }
            if (win && HouseCards.Has(table.DealerCards, HouseCard.Bounty))
            {
                table.Gambler.GainGold(Balance.BountyDealerGold);
                table.Note($"You collected the dealer's Bounty: +{Balance.BountyDealerGold} gold.");
            }
            if (win && HouseCards.Has(cards, HouseCard.Leech)) score.AddMult(Balance.LeechMult);
        }
    }
}
