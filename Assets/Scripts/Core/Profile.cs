using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HouseEdge.Core
{
    // what carries between runs: unlocked decks and stakes, and a few records.
    // the game saves it as text (see Serialize), Core never touches files
    public sealed class Profile
    {
        private readonly List<StartingDeck> _decks = new List<StartingDeck> { StartingDeck.Standard };

        public IReadOnlyList<StartingDeck> UnlockedDecks => _decks;
        public Stake HighestStake { get; private set; } = Stake.White;
        public int Runs { get; private set; }
        public int Wins { get; private set; }
        public long BestScore { get; private set; }
        public int BestFloor { get; private set; }

        public bool IsUnlocked(StartingDeck deck) => _decks.Contains(deck);
        public bool IsUnlocked(Stake stake) => stake <= HighestStake;

        // what this run unlocked, for the run-over screen
        public List<string> Record(Run run)
        {
            var unlocked = new List<string>();
            if (!run.IsOver) return unlocked;
            Runs++;
            BestScore = Math.Max(BestScore, run.TotalScore);
            BestFloor = Math.Max(BestFloor, run.Floor);

            void Unlock(StartingDeck deck, bool earned)
            {
                if (!earned || _decks.Contains(deck)) return;
                _decks.Add(deck);
                unlocked.Add($"{Decks.Name(deck)} deck");
            }

            bool won = run.Phase == RunPhase.Won || run.BeatTheHouse;
            if (won)
            {
                Wins++;
                if (run.Options.Stake == HighestStake && HighestStake < Stake.Gold)
                {
                    HighestStake++;
                    unlocked.Add($"{Stakes.Name(HighestStake)} stake");
                }
            }
            Unlock(StartingDeck.ShortDeck, won);
            Unlock(StartingDeck.DealersDeck, run.BeatTheHouse);
            Unlock(StartingDeck.AceHigh, run.BestBlackjackMult >= 20);
            Unlock(StartingDeck.Lowball, run.Floor >= 5);
            Unlock(StartingDeck.MarkedDeck, run.WasBusted);
            return unlocked;
        }

        public string Serialize()
        {
            var text = new StringBuilder();
            text.Append("decks=").Append(string.Join(",", _decks.Select(d => (int)d))).Append('\n');
            text.Append("stake=").Append((int)HighestStake).Append('\n');
            text.Append("runs=").Append(Runs).Append('\n');
            text.Append("wins=").Append(Wins).Append('\n');
            text.Append("best=").Append(BestScore.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("floor=").Append(BestFloor).Append('\n');
            return text.ToString();
        }

        // bad or missing lines fall back to a fresh profile's values
        public static Profile Parse(string text)
        {
            var profile = new Profile();
            if (string.IsNullOrEmpty(text)) return profile;
            foreach (var line in text.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "decks":
                        foreach (var part in value.Split(','))
                            if (int.TryParse(part, out int d) && d >= 0 && d < Decks.All.Count && !profile._decks.Contains((StartingDeck)d))
                                profile._decks.Add((StartingDeck)d);
                        break;
                    case "stake":
                        if (int.TryParse(value, out int s) && s >= 0 && s < Stakes.All.Count) profile.HighestStake = (Stake)s;
                        break;
                    case "runs": if (int.TryParse(value, out int r)) profile.Runs = r; break;
                    case "wins": if (int.TryParse(value, out int w)) profile.Wins = w; break;
                    case "best": if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long b)) profile.BestScore = b; break;
                    case "floor": if (int.TryParse(value, out int f)) profile.BestFloor = f; break;
                }
            }
            return profile;
        }
    }
}
