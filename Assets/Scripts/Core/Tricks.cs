using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    // one-use plays you make in the middle of a hand. you hold up to 2
    public enum TrickKind { Peek, TakeBack, Steal, SecondThoughts, AceUpTheSleeve, Nudge }

    public static class Tricks
    {
        public static readonly IReadOnlyList<TrickKind> All = (TrickKind[])Enum.GetValues(typeof(TrickKind));

        public static string Name(TrickKind kind) => kind switch
        {
            TrickKind.TakeBack => "Take Back",
            TrickKind.SecondThoughts => "Second Thoughts",
            TrickKind.AceUpTheSleeve => "Ace Up the Sleeve",
            _ => kind.ToString(),
        };

        public static string Describe(TrickKind kind) => kind switch
        {
            TrickKind.Peek => "Turn the dealer's hole card face up for this hand.",
            TrickKind.TakeBack => "Put the card you just hit back under the shoe. Works even after a bust.",
            TrickKind.Steal => "Pick one of your cards and swap it with the dealer's face-up card.",
            TrickKind.Nudge => "Pick one of your number cards (3 to 10): it counts 1 less for this hand.",
            TrickKind.SecondThoughts => "Double Down after you've already hit: this hand's win scores x2, and you stand.",
            _ => $"Slip an Ace of Spades into your hand. It vanishes after the hand. +{Balance.AceUpSleeveHeat} Heat.",
        };
    }

    // rare hands with a name, a fanfare and a Mult bonus. found ones go in the collection
    public enum Feat { TheBlackJack, TripleSevens, PerfectFlush, Ladder, SixCardCharlie, RoyalCouple }

    public static class Feats
    {
        public static readonly IReadOnlyList<Feat> All = (Feat[])Enum.GetValues(typeof(Feat));

        public static string Name(Feat feat) => feat switch
        {
            Feat.TheBlackJack => "The Black Jack",
            Feat.TripleSevens => "Triple Sevens",
            Feat.PerfectFlush => "Perfect Flush",
            Feat.SixCardCharlie => "Six-Card Charlie",
            Feat.RoyalCouple => "Royal Couple",
            _ => feat.ToString(),
        };

        public static string Describe(Feat feat) => feat switch
        {
            Feat.TheBlackJack => "A blackjack made of the Ace of Spades and a black Jack.",
            Feat.TripleSevens => "Three 7s for 21.",
            Feat.PerfectFlush => "Exactly 21 with three or more cards, all one suit.",
            Feat.Ladder => "Five or more cards in a row, like A-2-3-4-5.",
            Feat.SixCardCharlie => "Six cards without busting.",
            _ => "A King and a Queen of the same suit.",
        };

        public static double Factor(Feat feat) => feat switch
        {
            Feat.TheBlackJack => 3,
            Feat.TripleSevens => 3,
            Feat.PerfectFlush => 2.5,
            Feat.Ladder => 2.5,
            Feat.SixCardCharlie => 2,
            _ => 2,
        };

        // every Feat a winning hand makes. curses don't count
        public static List<Feat> Find(PlayerHand hand)
        {
            var found = new List<Feat>();
            var cards = new List<Card>();
            foreach (var card in hand.Cards)
                if (!card.IsCursed) cards.Add(card);
            var value = hand.Value;
            if (hand.IsBlackjack && cards.Count == 2)
            {
                bool aceOfSpades = cards.Exists(c => c.Rank == Rank.Ace && c.Suit == Suit.Spades);
                bool blackJack = cards.Exists(c => c.Rank == Rank.Jack && (c.Suit == Suit.Spades || c.Suit == Suit.Clubs));
                if (aceOfSpades && blackJack) found.Add(Feat.TheBlackJack);
            }
            if (value.Total == 21 && cards.Count == 3 && cards.TrueForAll(c => c.Rank == Rank.Seven)) found.Add(Feat.TripleSevens);
            if (value.Total == 21 && cards.Count >= 3 && cards.TrueForAll(c => c.Suit == cards[0].Suit)) found.Add(Feat.PerfectFlush);
            if (Combos.LongestRun(cards) >= 5) found.Add(Feat.Ladder);
            if (cards.Count >= 6 && !value.IsBust) found.Add(Feat.SixCardCharlie);
            foreach (var king in cards)
                if (king.Rank == Rank.King && cards.Exists(q => q.Rank == Rank.Queen && q.Suit == king.Suit))
                {
                    found.Add(Feat.RoyalCouple);
                    break;
                }
            return found;
        }
    }
}
