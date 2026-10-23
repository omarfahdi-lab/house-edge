using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;

namespace HouseEdge.Game
{
    // what the dealer says after a hand. each dealer has their own voice; bosses speak for themselves
    public static class DealerQuips
    {
        public enum Moment { Blackjack, Big, Win, DealerBust, Push, Lose, Bust, Surrender }

        private static readonly Dictionary<Moment, string[]> Generic = new Dictionary<Moment, string[]>
        {
            [Moment.Blackjack] = new[] { "Blackjack. Lovely.", "Natural twenty-one. Nicely done.", "Well, look at that." },
            [Moment.Big] = new[] { "That's a big one.", "The pit boss is watching you now.", "Somebody's on a heater." },
            [Moment.Win] = new[] { "Winner.", "Pays you.", "Your hand." },
            [Moment.DealerBust] = new[] { "Too many. Dealer busts.", "Over I go.", "Twenty-two. Ouch." },
            [Moment.Push] = new[] { "Push. Nobody wins.", "A tie. Try again." },
            [Moment.Lose] = new[] { "House wins that one.", "Better luck next hand.", "Dealer takes it." },
            [Moment.Bust] = new[] { "Bust! Better luck next time.", "Too many, friend.", "That's over." },
            [Moment.Surrender] = new[] { "Smart fold.", "Live to play another hand." },
        };

        private static readonly Dictionary<string, Dictionary<Moment, string[]>> Voices = new Dictionary<string, Dictionary<Moment, string[]>>
        {
            ["Rookie Ray"] = new Dictionary<Moment, string[]>
            {
                [Moment.Blackjack] = new[] { "Oh! Oh, that's a blackjack. Um, congrats!" },
                [Moment.Big] = new[] { "Is... is that allowed? I'd better count it twice.", "My manager is going to ask about this." },
                [Moment.DealerBust] = new[] { "Oops. Sorry. I mean, good for you!", "I'm still getting the hang of this." },
                [Moment.Bust] = new[] { "Oh no, that's twenty-two. Sorry!" },
                [Moment.Lose] = new[] { "I won? I won! Sorry. Ahem." },
            },
            ["Silent Sofia"] = new Dictionary<Moment, string[]>
            {
                [Moment.Blackjack] = new[] { "...", "(a slight nod)" },
                [Moment.Big] = new[] { "(raises one eyebrow)" },
                [Moment.Win] = new[] { "..." },
                [Moment.DealerBust] = new[] { "(sighs)" },
                [Moment.Lose] = new[] { "(slides your chips away)" },
                [Moment.Bust] = new[] { "(taps the felt)" },
            },
            ["Chatty Chen"] = new Dictionary<Moment, string[]>
            {
                [Moment.Blackjack] = new[] { "Blackjack! You know my cousin got three in a row once? True story." },
                [Moment.Big] = new[] { "Wow wow wow. Are you counting cards? Kidding! Unless?" },
                [Moment.Win] = new[] { "Nice! You're having a night, huh?", "Ooh, a winner. Tell your friends." },
                [Moment.DealerBust] = new[] { "Ahh, I always bust on a sixteen. Always!" },
                [Moment.Lose] = new[] { "Don't sweat it, the next one's yours. Probably.", "Close! Well. Not that close." },
                [Moment.Bust] = new[] { "Bust! Ooh, I felt that one.", "Hit on nineteen? Bold. Very bold." },
            },
            ["Old Pete"] = new Dictionary<Moment, string[]>
            {
                [Moment.Blackjack] = new[] { "Forty years, still pretty to see." },
                [Moment.Big] = new[] { "Haven't seen a hand like that since '79." },
                [Moment.DealerBust] = new[] { "Hmph. Cards were cold." },
                [Moment.Lose] = new[] { "House always wins, kid. Eventually." },
                [Moment.Bust] = new[] { "Should've stood, kid.", "Greedy hands go home empty." },
            },
            ["Vegas Val"] = new Dictionary<Moment, string[]>
            {
                [Moment.Blackjack] = new[] { "Blackjack, baby! Lights! Cameras!" },
                [Moment.Big] = new[] { "Now THAT is a show!", "Somebody get this player a spotlight." },
                [Moment.Win] = new[] { "Winner winner!", "Ooh, a crowd-pleaser." },
                [Moment.DealerBust] = new[] { "And the dealer takes a tumble!" },
                [Moment.Lose] = new[] { "Tough crowd, tough cards.", "The house thanks you, darling." },
                [Moment.Bust] = new[] { "Bust! The crowd goes 'ooooh'." },
            },
            ["Lucky Lou"] = new Dictionary<Moment, string[]>
            {
                [Moment.Blackjack] = new[] { "Told you this seat was lucky." },
                [Moment.Big] = new[] { "Rub my rabbit's foot for good measure." },
                [Moment.DealerBust] = new[] { "Must've been the black cat this morning." },
                [Moment.Lose] = new[] { "Luck's a lady, and she's with me tonight." },
                [Moment.Bust] = new[] { "Never hit on a Tuesday. Everyone knows that." },
            },
        };

        // what to say about the hand just settled, or null to stay quiet
        public static string For(Run run)
        {
            var table = run.ActiveTable;
            if (run.Boss != null || table != run.Table || table.LastScores.Count == 0) return null;
            var scores = table.LastScores;
            long total = scores.Sum(s => s.Total);
            Moment moment;
            if (scores.Any(s => s.Outcome == Outcome.Blackjack)) moment = Moment.Blackjack;
            else if (scores.Any(s => s.Outcome.IsWin()) && total >= table.Target * 0.4) moment = Moment.Big;
            else if (scores.All(s => s.Outcome == Outcome.Bust)) moment = Moment.Bust;
            else if (scores.All(s => s.Outcome == Outcome.Surrender)) moment = Moment.Surrender;
            else if (scores.Any(s => s.Outcome.IsWin()) && scores[0].DealerBusted) moment = Moment.DealerBust;
            else if (scores.Any(s => s.Outcome.IsWin())) moment = Moment.Win;
            else if (scores.All(s => s.Outcome == Outcome.Push)) moment = Moment.Push;
            else moment = Moment.Lose;

            // the big moments always get a line, the ordinary ones only sometimes
            bool big = moment == Moment.Blackjack || moment == Moment.Big;
            if (!big && Random.value > 0.4f) return null;

            string name = run.Dealer?.Name ?? "Dealer";
            string[] lines = Voices.TryGetValue(name, out var voice) && voice.TryGetValue(moment, out var own) ? own : Generic[moment];
            return $"<b>{name}:</b> {lines[Random.Range(0, lines.Length)]}";
        }
    }
}
