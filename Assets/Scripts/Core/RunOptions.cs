using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    public enum StartingDeck { Standard, ShortDeck, AceHigh, Lowball, MarkedDeck, DealersDeck }

    // difficulty. each stake includes every rule before it
    public enum Stake { White, Red, Green, Black, Purple, Gold }

    // everything that sets a run up besides its seed
    public sealed class RunOptions
    {
        public StartingDeck Deck = StartingDeck.Standard;
        public Stake Stake = Stake.White;
        // keep climbing after The House with growing targets
        public bool Endless;
        // the same seed for everyone today; only the label changes
        public bool Daily;

        public RunOptions Clone() => (RunOptions)MemberwiseClone();
    }

    public static class Decks
    {
        public static readonly IReadOnlyList<StartingDeck> All = (StartingDeck[])Enum.GetValues(typeof(StartingDeck));

        public static string Name(StartingDeck deck) => deck switch
        {
            StartingDeck.ShortDeck => "Short Deck",
            StartingDeck.AceHigh => "Ace High",
            StartingDeck.MarkedDeck => "Marked Deck",
            StartingDeck.DealersDeck => "Dealer's Deck",
            _ => deck.ToString(),
        };

        public static string Describe(StartingDeck deck) => deck switch
        {
            StartingDeck.ShortDeck => "No 2s through 5s. More busts for both sides, bigger Chip totals.",
            StartingDeck.AceHigh => "4 extra Aces.",
            StartingDeck.Lowball => "No face cards. Safer hands, smaller Chips.",
            StartingDeck.MarkedDeck => "Every card starts Marked. You start with 30 Heat.",
            StartingDeck.DealersDeck => "The dealer's hole card is always visible, but you get 1 fewer Hand.",
            _ => "A normal 52-card deck.",
        };

        public static string Unlock(StartingDeck deck) => deck switch
        {
            StartingDeck.ShortDeck => "Win a run",
            StartingDeck.AceHigh => "Score a Blackjack with x20+ Mult",
            StartingDeck.Lowball => "Reach floor 5",
            StartingDeck.MarkedDeck => "Get Busted by security",
            StartingDeck.DealersDeck => "Beat The House",
            _ => "Start",
        };

        public static List<Card> Build(StartingDeck deck)
        {
            var cards = Shoe.StandardDeck();
            switch (deck)
            {
                case StartingDeck.ShortDeck:
                    cards.RemoveAll(c => c.Rank <= Rank.Five);
                    break;
                case StartingDeck.AceHigh:
                    foreach (Suit suit in new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs })
                        cards.Add(new Card(Rank.Ace, suit));
                    break;
                case StartingDeck.Lowball:
                    cards.RemoveAll(c => c.IsFace);
                    break;
                case StartingDeck.MarkedDeck:
                    foreach (var card in cards) card.Enhancement = Enhancement.Marked;
                    break;
            }
            return cards;
        }
    }

    public static class Stakes
    {
        public static readonly IReadOnlyList<Stake> All = (Stake[])Enum.GetValues(typeof(Stake));

        public static string Name(Stake stake) => stake + " Chip";

        public static string Describe(Stake stake) => stake switch
        {
            Stake.Red => "Low Tables pay no gold.",
            Stake.Green => "Targets are 1.25x.",
            Stake.Black => "You start the run with a Dead Card in your deck.",
            Stake.Purple => "Heat drops half as fast between tables.",
            Stake.Gold => "The dealer hits on soft 17.",
            _ => "Base game.",
        };

        public static double TargetFactor(Stake stake) => stake >= Stake.Green ? 1.25 : 1;
    }

    // run-wide rules from the deck and stake, applied to every table
    internal sealed class RunRules : TableEffect
    {
        private readonly RunOptions _options;

        public RunRules(RunOptions options) => _options = options;

        public override void OnTableStart(Table table)
        {
            if (_options.Deck == StartingDeck.DealersDeck)
            {
                table.Rules.HoleCardFaceUp = true;
                table.AddHands(-1);
            }
            if (_options.Stake >= Stake.Gold) table.Rules.DealerHitsSoft17 = true;
        }
    }

    // a short code that sets up the same run on any machine: seed, deck and stake
    public static class RunCodes
    {
        private const string Digits = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";

        public static string Encode(int seed, RunOptions options)
        {
            uint value = unchecked((uint)seed);
            var chars = new List<char>();
            do
            {
                chars.Add(Digits[(int)(value % (uint)Digits.Length)]);
                value /= (uint)Digits.Length;
            } while (value > 0);
            chars.Reverse();
            return $"{new string(chars.ToArray())}-{(int)options.Deck}{(int)options.Stake}{(options.Endless ? "E" : "")}";
        }

        public static bool TryDecode(string code, out int seed, out RunOptions options)
        {
            seed = 0;
            options = null;
            if (string.IsNullOrWhiteSpace(code)) return false;
            var parts = code.Trim().ToUpperInvariant().Split('-');
            if (parts.Length != 2 || parts[1].Length < 2 || parts[0].Length == 0) return false;

            ulong value = 0;
            foreach (char c in parts[0])
            {
                int digit = Digits.IndexOf(c);
                if (digit < 0) return false;
                value = value * (ulong)Digits.Length + (ulong)digit;
                if (value > uint.MaxValue) return false;
            }
            int deck = parts[1][0] - '0';
            int stake = parts[1][1] - '0';
            if (deck < 0 || deck >= Decks.All.Count || stake < 0 || stake >= Stakes.All.Count) return false;

            seed = unchecked((int)(uint)value);
            options = new RunOptions { Deck = (StartingDeck)deck, Stake = (Stake)stake, Endless = parts[1].EndsWith("E") };
            return true;
        }

        // the Daily Run: everyone gets the same seed on the same date
        public static int DailySeed(int year, int month, int day)
        {
            unchecked
            {
                int seed = year * 10000 + month * 100 + day;
                seed ^= seed << 13;
                seed ^= (int)((uint)seed >> 17);
                seed ^= seed << 5;
                return seed;
            }
        }
    }
}
