using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    public enum StartingDeck { Standard, ShortDeck, AceHigh, Lowball, MarkedDeck, DealersDeck }

    // difficulty. each stake keeps every rule of the ones before it
    public enum Stake { White, Red, Green, Black, Purple, Gold, Blue, Orange, Silver, Platinum }

    // everything that sets a run up besides its seed
    public sealed class RunOptions
    {
        public StartingDeck Deck = StartingDeck.Standard;
        public Stake Stake = Stake.White;
        // keep climbing after The House with growing targets
        public bool Endless;
        // the same seed for everyone today; only the label changes
        public bool Daily;
        // the guided first run: the first table's opening hands are stacked to teach the game
        public bool Tutorial;
        // a twist on the whole run (the Daily Run's changes every day)
        public Modifier Modifier = Modifier.None;
        // Vices this player hasn't unlocked yet: they never show up in this run
        public List<string> LockedVices = new List<string>();
        // a prototype rule: every deal gives you three cards, and the one you give away is the dealer's hole card
        public bool KeepTwoOfThree;
        // a prototype rule: the casino shuffles House cards into the shoe, more on every floor
        public bool HouseCards;
        // a prototype rule: a rival gambler takes the dealer's seat at the High Table every other floor
        public bool Rival;

        public RunOptions Clone()
        {
            var copy = (RunOptions)MemberwiseClone();
            copy.LockedVices = new List<string>(LockedVices);
            return copy;
        }
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
            Stake.Green => "Targets are 1.1x.",
            Stake.Black => "You start the run with 2 Dead Cards in your deck.",
            Stake.Purple => "Shop prices are 1 gold higher, and Heat drops half as fast between tables.",
            Stake.Gold => "The dealer hits on soft 17.",
            Stake.Blue => "1 fewer Burn at every table.",
            Stake.Orange => "Targets are 1.1x higher again.",
            Stake.Silver => "Pit Bosses and Elite tables have 1.2x targets.",
            Stake.Platinum => "1 fewer Hand at every table, and interest is capped at 2 gold.",
            _ => "Base game.",
        };

        public static double TargetFactor(Stake stake) => (stake >= Stake.Green ? 1.1 : 1) * (stake >= Stake.Orange ? 1.1 : 1);

        // Purple and up: everything in shops and rooms costs 1 more
        public static int PriceBump(Stake stake) => stake >= Stake.Purple ? 1 : 0;

        // Silver and up: bosses and Elites ask for more
        public static double BossFactor(Stake stake) => stake >= Stake.Silver ? 1.2 : 1;

        public const int PlatinumInterestCap = 2;
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
            if (_options.Stake >= Stake.Blue) table.AddBurns(-1);
            if (_options.Stake >= Stake.Platinum) table.AddHands(-1);
            if (_options.KeepTwoOfThree)
            {
                table.Rules.KeepTwoOfThree = true;
                table.AddHands(-Balance.KeepTwoOfThreeHandsLess);
            }
        }
    }

    // a short code that sets up the same run on any machine: seed, deck, stake and the rules toggled on
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
            return $"{new string(chars.ToArray())}-{(int)options.Deck}{(int)options.Stake}{(options.Endless ? "E" : "")}{(options.Tutorial ? "T" : "")}{(options.Modifier != Modifier.None ? "M" + (int)options.Modifier : "")}{(options.KeepTwoOfThree ? "K" : "")}{(options.HouseCards ? "H" : "")}{(options.Rival ? "R" : "")}";
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
            options = new RunOptions { Deck = (StartingDeck)deck, Stake = (Stake)stake, Endless = parts[1].Contains("E"), Tutorial = parts[1].Contains("T"),
                KeepTwoOfThree = parts[1].Contains("K"), HouseCards = parts[1].Contains("H"), Rival = parts[1].Contains("R") };
            int m = parts[1].IndexOf('M');
            if (m >= 0)
            {
                if (m + 1 >= parts[1].Length) return false;
                int modifier = parts[1][m + 1] - '0';
                if (modifier <= 0 || modifier >= Modifiers.All.Count) return false;
                options.Modifier = (Modifier)modifier;
            }
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
