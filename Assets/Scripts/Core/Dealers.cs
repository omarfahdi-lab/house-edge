using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    // the dealer at a Low or High Table: a style that bends the rules, and a tell about the hole card
    public abstract class Dealer : TableEffect
    {
        public abstract string Name { get; }
        public abstract string Style { get; }
        // what a sharp eye notices, or null when there's nothing to see right now
        public abstract string Tell(Table table);
        // what the tell means, spelled out by the Tell Reader Vice
        public abstract string TellMeaning(Table table);

        protected static Card HoleCard(Table table) => table.DealerCards.Count > 1 ? table.DealerCards[1] : null;
    }

    public static class Dealers
    {
        public static readonly IReadOnlyList<Func<Dealer>> All = new Func<Dealer>[]
        {
            () => new RookieRay(),
            () => new SilentSofia(),
            () => new ChattyChen(),
            () => new OldPete(),
            () => new VegasVal(),
            () => new LuckyLou(),
        };

        public static Dealer Random(GameRandom random) => All[random.Next(All.Count)]();
    }

    public sealed class RookieRay : Dealer
    {
        public override string Name => "Rookie Ray";
        public override string Style => "Nervous. 1 in 5 chance he hits on 17 by mistake.";
        public override void OnTableStart(Table table) => table.Rules.DealerMistakeOneIn = 5;
        public override string Tell(Table table) => HoleCard(table)?.IsTenValue == true ? "Ray is fidgeting." : "Ray looks calm.";
        public override string TellMeaning(Table table) => HoleCard(table)?.IsTenValue == true ? "His hole card is a 10." : "His hole card isn't a 10.";
    }

    public sealed class SilentSofia : Dealer
    {
        public override string Name => "Silent Sofia";
        public override string Style => "Hits on soft 17.";
        public override void OnTableStart(Table table) => table.Rules.DealerHitsSoft17 = true;
        public override string Tell(Table table) => "Sofia gives nothing away.";
        public override string TellMeaning(Table table) => "No tell. Ever.";
    }

    public sealed class ChattyChen : Dealer
    {
        public override string Name => "Chatty Chen";
        public override string Style => "Talks nonstop.";
        public override string Tell(Table table) => HoleCard(table) is Card hole
            ? hole.IsRed ? "Chen won't stop talking about roses." : "Chen won't stop talking about the night sky."
            : null;
        public override string TellMeaning(Table table) => HoleCard(table) is Card hole
            ? hole.IsRed ? "His hole card is red." : "His hole card is black."
            : null;
    }

    public sealed class OldPete : Dealer
    {
        public override string Name => "Old Pete";
        public override string Style => "Slow and friendly. You get +1 Burn at his table.";
        public override void OnTableStart(Table table) => table.AddBurns(1);
        private static bool Low(Table table) => HoleCard(table) is Card hole && hole.LowValue <= 6 && !hole.IsAce;
        public override string Tell(Table table) => Low(table) ? "Pete sighs." : "Pete hums along.";
        public override string TellMeaning(Table table) => Low(table) ? "His hole card is low (6 or under)." : "His hole card isn't low.";
    }

    public sealed class VegasVal : Dealer
    {
        public override string Name => "Vegas Val";
        public override string Style => "Generous. You win all pushes.";
        public override void OnTableStart(Table table) => table.Rules.Pushes = PushRule.PlayerWins;
        private static bool Close(Table table)
        {
            int total = table.DealerValue.Total;
            return total >= 12 && total <= 16;
        }
        public override string Tell(Table table) => Close(table) ? "Val is smiling." : "Val looks bored.";
        public override string TellMeaning(Table table) => Close(table) ? "She has 12 to 16 and could bust." : "She isn't close to busting.";
    }

    public sealed class LuckyLou : Dealer
    {
        public override string Name => "Lucky Lou";
        public override string Style => "Reshuffles after every dealer bust.";
        public override void OnTableStart(Table table) => table.Rules.ReshuffleAfterDealerBust = true;
        public override string Tell(Table table) => HoleCard(table)?.IsAce == true ? "Lou taps the table." : "Lou keeps his hands still.";
        public override string TellMeaning(Table table) => HoleCard(table)?.IsAce == true ? "His hole card is an Ace." : "His hole card isn't an Ace.";
    }
}
