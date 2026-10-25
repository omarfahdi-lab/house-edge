using System.Linq;
using HouseEdge.Core;
using UnityEngine;

namespace HouseEdge.Game
{
    // the rival's voice: an entrance before every meeting that remembers the last one,
    // a line after every hand, and a parting shot when you beat the table
    public static class RivalLines
    {
        // the entrance card, in the same frame the bosses walk into
        public static BossDefinition Intro(Run run, Rival rival)
        {
            string first = First(rival);
            string last = LastMeeting(run, rival);
            var (title, quote, twist, tip) = rival.Meeting switch
            {
                1 => ("Has been watching you since the Lobby",
                    "So you're the new money. I've owned this floor for nine years. Let's see you beat a hand you can see.",
                    $"Both hands are face up, and {first} draws until beating yours, even past 17.",
                    $"A low Stand won't hold up. Stand on 18 or more, or make {first} chase you into a bust."),
                2 => ("Back for more",
                    $"{last} Lucky. I've thought about nothing else since.",
                    $"Draws until beating you, and now hits on ties too.",
                    "A tie isn't safe anymore. Win outright, or make the chase go over."),
                3 => ("Has learned a trick",
                    $"{last} Twice! So I've been practicing something.",
                    "Chases you, hits on ties, and palms the first card that would bust.",
                    $"The first bust doesn't count. {first} has to go over twice."),
                _ => ("One last hand before The House",
                    $"{last} Only one of us walks into The House, and it isn't going to be you.",
                    $"Every trick so far, and you get {Balance.RivalFinalHandsLess} fewer Hand.",
                    $"This is the last meeting. Beat {first} here and your rival is done for good."),
            };
            return new BossDefinition(rival.Name, title, "Plays against your hand", twist, quote,
                tip + $" Beating the table pays +{Balance.RivalBeatenGold} gold.", null);
        }

        public static string Kind(Rival rival) => rival.Meeting >= 4 ? "YOUR RIVAL  ·  FINAL MEETING" : $"YOUR RIVAL  ·  MEETING {rival.Meeting}";

        // the score you beat them with last time, so the grudge has a number on it
        private static string LastMeeting(Run run, Rival rival)
        {
            int floor = run.Floor - Balance.RivalEveryFloors;
            var met = run.History.Where(r => r.Floor == floor && r.Stage == TableKind.High && !r.Elite).ToList();
            if (met.Count == 0) return "We've met.";
            var record = met[met.Count - 1];
            return $"Floor {floor}. {record.Score:N0} against my {record.Target:N0}.";
        }

        // after every hand at the rival's table. the rival always has something to say
        public static string AfterHand(Run run, Rival rival)
        {
            var table = run.Table;
            var scores = table.LastScores;
            string[] lines;
            if (table.Phase == TablePhase.Won) lines = Beaten(run, rival);
            else if (table.Phase == TablePhase.Lost) lines = new[] { "Should've stayed in the Lobby.", "Nine years. Nobody beats me on my floor." };
            else if (scores.Any(s => s.Outcome == Outcome.Blackjack)) lines = new[] { "A natural? Against me?", "Fine. Enjoy it while it lasts." };
            else if (scores.Any(s => s.Outcome.IsWin()) && table.DealerBusted) lines = new[] { "Too many. You'd have chased it too.", "I had to try.", "Don't look at me like that." };
            else if (scores.Any(s => s.Outcome.IsWin())) lines = new[] { "Hmph.", "Don't get used to it.", "One hand. That's all that was." };
            else if (scores.All(s => s.Outcome == Outcome.Push)) lines = new[] { "Even. For now.", "Nobody blinks." };
            else if (scores.All(s => s.Outcome == Outcome.Bust)) lines = new[] { "Greedy. I like it. Do it again.", "You did that to yourself." };
            else if (scores.All(s => s.Outcome == Outcome.Surrender)) lines = new[] { "Running already?", "Walk away. It suits you." };
            else lines = new[] { "Stand on that? Really?", "Saw that coming from across the room.", "That's how it's done.", "I can see your cards, remember." };
            return $"<b>{rival.Name}:</b> {lines[Random.Range(0, lines.Length)]}";
        }

        private static string[] Beaten(Run run, Rival rival) => rival.Meeting switch
        {
            1 => new[] { $"Fine. Floor {run.Floor + Balance.RivalEveryFloors}. Don't be late." },
            2 => new[] { "That's two. Nobody gets three." },
            3 => new[] { "Three... I'll be waiting at the top." },
            _ => new[] { "Go on, then. Tell The House I said hello." },
        };

        private static string First(Rival rival) => rival.Name.Split(' ')[0];
    }
}
