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

        // the collection: every Vice you've been offered or held, every boss you've faced, every combo you've won with
        private readonly HashSet<string> _seenVices = new HashSet<string>();
        private readonly HashSet<string> _bossesFaced = new HashSet<string>();
        private readonly long[] _combos = new long[Combos.All.Count];
        private readonly long[] _feats = new long[Feats.All.Count];
        public long FeatsHit(Feat feat) => _feats[(int)feat];
        private readonly HashSet<string> _unlockedVices = new HashSet<string>();

        public bool HasSeen(ViceDefinition vice) => _seenVices.Contains(vice.Id);
        public bool HasFaced(string boss) => _bossesFaced.Contains(boss);
        public long CombosWon(Combo combo) => _combos[(int)combo];

        // Vices you earn by playing: what unlocks each one
        public static readonly IReadOnlyList<(string id, string how, Func<Profile, bool> earned)> ViceUnlocks =
            new List<(string, string, Func<Profile, bool>)>
            {
                ("Matchmaker", "Win a hand with Three of a Kind", p => p.CombosWon(Combo.ThreeOfAKind) > 0),
                ("StraightShooter", "Win a hand with a Run", p => p.CombosWon(Combo.Run) > 0),
                ("CourtJester", "Win a hand with a Royal Court", p => p.CombosWon(Combo.RoyalCourt) > 0),
                ("FlushRoyale", "Win a hand with a Flush", p => p.CombosWon(Combo.Flush) > 0),
                ("ComboKing", "Win 50 combos in total", p => p._combos.Sum() >= 50),
            };

        public bool IsUnlocked(ViceDefinition vice) => ViceUnlocks.All(u => u.id != vice.Id) || _unlockedVices.Contains(vice.Id);

        public static string UnlockHint(ViceDefinition vice) => ViceUnlocks.FirstOrDefault(u => u.id == vice.Id).how;

        // the Vices a new run shouldn't offer yet
        public List<string> LockedVices => ViceUnlocks.Where(u => !_unlockedVices.Contains(u.id)).Select(u => u.id).ToList();

        public bool IsUnlocked(StartingDeck deck) => _decks.Contains(deck);
        public bool IsUnlocked(Stake stake) => stake <= HighestStake;

        // what this run unlocked, for the run-over screen
        public List<string> Record(Run run)
        {
            var unlocked = new List<string>();
            if (!run.IsOver) return unlocked;
            Runs++;
            _seenVices.UnionWith(run.SeenVices);
            _seenVices.UnionWith(run.Gambler.Vices.Select(v => v.Definition.Id));
            _bossesFaced.UnionWith(run.BossesFaced);
            for (int i = 0; i < _combos.Length; i++) _combos[i] += run.CombosWon[i];
            for (int i = 0; i < _feats.Length; i++) _feats[i] += run.FeatsHit[i];
            foreach (var (id, _, earned) in ViceUnlocks)
            {
                if (_unlockedVices.Contains(id) || !earned(this)) continue;
                _unlockedVices.Add(id);
                unlocked.Add($"{ViceCatalog.Get(id).Name} (Vice)");
            }
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
                if (run.Options.Stake == HighestStake && HighestStake < Stakes.All[Stakes.All.Count - 1])
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
            text.Append("seen=").Append(string.Join(",", _seenVices.OrderBy(v => v, StringComparer.Ordinal))).Append('\n');
            text.Append("bosses=").Append(string.Join(",", _bossesFaced.OrderBy(b => b, StringComparer.Ordinal))).Append('\n');
            text.Append("combos=").Append(string.Join(",", _combos.Select(c => c.ToString(CultureInfo.InvariantCulture)))).Append('\n');
            text.Append("feats=").Append(string.Join(",", _feats.Select(c => c.ToString(CultureInfo.InvariantCulture)))).Append('\n');
            text.Append("vices=").Append(string.Join(",", _unlockedVices.OrderBy(v => v, StringComparer.Ordinal))).Append('\n');
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
                    case "seen": profile._seenVices.UnionWith(value.Split(',').Where(v => v.Length > 0)); break;
                    case "bosses": profile._bossesFaced.UnionWith(value.Split(',').Where(v => v.Length > 0)); break;
                    case "vices": profile._unlockedVices.UnionWith(value.Split(',').Where(v => v.Length > 0)); break;
                    case "combos":
                        var counts = value.Split(',');
                        for (int i = 0; i < Math.Min(counts.Length, profile._combos.Length); i++)
                            if (long.TryParse(counts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out long c)) profile._combos[i] = c;
                        break;
                    case "feats":
                        var feats = value.Split(',');
                        for (int i = 0; i < Math.Min(feats.Length, profile._feats.Length); i++)
                            if (long.TryParse(feats[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out long made)) profile._feats[i] = made;
                        break;
                }
            }
            return profile;
        }
    }
}
