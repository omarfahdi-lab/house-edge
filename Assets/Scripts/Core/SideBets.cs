using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    public enum SideBet { None, PerfectPair, TwentyOnePlus3, BustIt, LuckyLadies, HotHand }

    public readonly struct SideBetResult
    {
        public SideBet Bet { get; }
        public int Payout { get; }
        public string Label { get; }
        public bool Won => Payout > 0;

        public SideBetResult(SideBet bet, int payout, string label)
        {
            Bet = bet;
            Payout = payout;
            Label = label;
        }
    }

    // one side bet before each hand. it pays out whether you win the hand or not
    public static class SideBets
    {
        public static readonly IReadOnlyList<SideBet> All = new[]
        {
            SideBet.PerfectPair, SideBet.TwentyOnePlus3, SideBet.BustIt, SideBet.LuckyLadies, SideBet.HotHand,
        };

        public static string Name(SideBet bet) => bet switch
        {
            SideBet.PerfectPair => "Perfect Pair",
            SideBet.TwentyOnePlus3 => "21+3",
            SideBet.BustIt => "Bust It",
            SideBet.LuckyLadies => "Lucky Ladies",
            SideBet.HotHand => "Hot Hand",
            _ => "",
        };

        public static string Description(SideBet bet) => bet switch
        {
            SideBet.PerfectPair => "Your first two cards are a pair: +8g. Same suit: +20g.",
            SideBet.TwentyOnePlus3 => "Your two cards and the dealer's up card make a flush (+6g), straight (+8g) or three of a kind (+15g).",
            SideBet.BustIt => "The dealer busts: +5g. Busts with 6+ cards: +25g.",
            SideBet.LuckyLadies => "Your first two cards total 20: +10g. Two Queens of Hearts: +100g.",
            SideBet.HotHand => "You win the hand: +2g and +5 Mult.",
            _ => "",
        };

        public static int Cost(SideBet bet) => bet == SideBet.LuckyLadies ? 3 : bet == SideBet.HotHand ? 1 : 2;

        public static PlayerAction ActionFor(SideBet bet) => bet switch
        {
            SideBet.PerfectPair => PlayerAction.BetPerfectPair,
            SideBet.TwentyOnePlus3 => PlayerAction.Bet21Plus3,
            SideBet.BustIt => PlayerAction.BetBustIt,
            SideBet.LuckyLadies => PlayerAction.BetLuckyLadies,
            _ => PlayerAction.BetHotHand,
        };

        public static SideBet BetFor(PlayerAction action) => action switch
        {
            PlayerAction.BetPerfectPair => SideBet.PerfectPair,
            PlayerAction.Bet21Plus3 => SideBet.TwentyOnePlus3,
            PlayerAction.BetBustIt => SideBet.BustIt,
            PlayerAction.BetLuckyLadies => SideBet.LuckyLadies,
            PlayerAction.BetHotHand => SideBet.HotHand,
            _ => SideBet.None,
        };

        // bets settled by the opening cards. Bust It and Hot Hand wait for the end of the hand
        public static SideBetResult SettleOnDeal(SideBet bet, IReadOnlyList<Card> player, Card dealerUp)
        {
            switch (bet)
            {
                case SideBet.PerfectPair:
                {
                    var a = player[0];
                    var b = player[1];
                    if (a.IsCursed || b.IsCursed || a.Rank != b.Rank) return new SideBetResult(bet, 0, "No pair");
                    return a.Suit == b.Suit ? new SideBetResult(bet, 20, "Perfect pair!") : new SideBetResult(bet, 8, "Pair");
                }
                case SideBet.TwentyOnePlus3:
                {
                    var three = new[] { player[0], player[1], dealerUp };
                    if (three.Any(c => c.IsCursed)) return new SideBetResult(bet, 0, "Nothing");
                    if (three.All(c => c.Rank == three[0].Rank)) return new SideBetResult(bet, 15, "Three of a kind!");
                    if (IsStraight(three)) return new SideBetResult(bet, 8, "Straight!");
                    if (three.All(c => c.Suit == three[0].Suit)) return new SideBetResult(bet, 6, "Flush!");
                    return new SideBetResult(bet, 0, "Nothing");
                }
                case SideBet.LuckyLadies:
                {
                    bool queens = player.Take(2).All(c => !c.IsCursed && c.Rank == Rank.Queen && c.Suit == Suit.Hearts);
                    if (queens) return new SideBetResult(bet, 100, "Two Queens of Hearts!!");
                    return HandValue.Of(player).Total == 20 ? new SideBetResult(bet, 10, "Twenty!") : new SideBetResult(bet, 0, "Not 20");
                }
            }
            return new SideBetResult(bet, 0, "");
        }

        public static SideBetResult SettleBustIt(bool dealerBusted, int dealerCards)
        {
            if (!dealerBusted) return new SideBetResult(SideBet.BustIt, 0, "Dealer didn't bust");
            return dealerCards >= 6 ? new SideBetResult(SideBet.BustIt, 25, "Huge bust!") : new SideBetResult(SideBet.BustIt, 5, "Dealer bust");
        }

        // aces can be high or low
        private static bool IsStraight(Card[] cards)
        {
            var ranks = cards.Select(c => (int)c.Rank).OrderBy(r => r).ToArray();
            if (ranks[1] == ranks[0] + 1 && ranks[2] == ranks[1] + 1) return true;
            // A-2-3
            return ranks[0] == 2 && ranks[1] == 3 && ranks[2] == (int)Rank.Ace;
        }
    }
}
