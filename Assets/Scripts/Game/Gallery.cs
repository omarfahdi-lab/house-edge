using System.Collections.Generic;
using UnityEngine;

namespace HouseEdge.Game
{
    // hundreds of famous public-domain portraits (Resources/Gallery, listed in index.txt). each run deals them out
    // to the dealers, hosts and you, paintings you haven't seen yet first, so faces almost never repeat.
    // the Pit Bosses keep their own signature paintings (Resources/Portraits) so you always know who you're facing
    public static class Gallery
    {
        public readonly struct Painting
        {
            public readonly string Id, Title, Artist, Year;

            public Painting(string id, string title, string artist, string year)
            {
                Id = id;
                Title = title;
                Artist = artist;
                Year = year;
            }

            public string Label => Year.Length > 0 ? $"{Title}  ·  {Artist}, {Year}" : $"{Title}  ·  {Artist}";
        }

        private const string SeenKey = "gallerySeen";

        private static List<Painting> _all;
        private static readonly Dictionary<string, Painting> Assigned = new Dictionary<string, Painting>();
        private static readonly Queue<Painting> Deck = new Queue<Painting>();
        private static HashSet<string> _seen;

        // the bosses (and other set characters) that keep their own painting
        private static readonly HashSet<string> Signature = new HashSet<string>
        {
            "thetwins", "theiceman", "thecheat", "themime", "thetaxman", "thepurist", "theceiling", "thesweeper", "thesilencer",
            "thecardmechanic", "thehoarder", "thepitboss", "theeyeinthesky", "theenforcer", "thehouse",
        };

        public static int Count => All.Count;

        private static List<Painting> All
        {
            get
            {
                if (_all != null) return _all;
                _all = new List<Painting>();
                var index = Resources.Load<TextAsset>("Gallery/index");
                if (index == null) return _all;
                foreach (var line in index.text.Split('\n'))
                {
                    var f = line.TrimEnd('\r').Split('|');
                    if (f.Length >= 4) _all.Add(new Painting(f[0], f[1], f[2], f[3]));
                }
                return _all;
            }
        }

        // a new run: shuffle the gallery with the run's seed, unseen paintings on top
        public static void Deal(int seed)
        {
            Assigned.Clear();
            Deck.Clear();
            LoadSeen();
            var random = new System.Random(seed);
            var fresh = new List<Painting>();
            var old = new List<Painting>();
            foreach (var p in All) (_seen.Contains(p.Id) ? old : fresh).Add(p);
            // everything seen: start the cycle over
            if (fresh.Count < 40)
            {
                _seen.Clear();
                fresh.AddRange(old);
                old.Clear();
            }
            Shuffle(fresh, random);
            Shuffle(old, random);
            foreach (var p in fresh) Deck.Enqueue(p);
            foreach (var p in old) Deck.Enqueue(p);
        }

        // the painting that plays this character in this run, or null if they keep a signature portrait
        public static Painting? For(string name)
        {
            string id = Puppet.Id(name);
            if (Signature.Contains(id) || All.Count == 0) return null;
            if (Assigned.TryGetValue(id, out var painting)) return painting;
            if (Deck.Count == 0) Deal(id.GetHashCode());
            painting = Deck.Dequeue();
            Assigned[id] = painting;
            _seen.Add(painting.Id);
            PlayerPrefs.SetString(SeenKey, string.Join(",", _seen));
            return painting;
        }

        public static Texture2D Texture(Painting painting) => Resources.Load<Texture2D>("Gallery/" + painting.Id);

        private static void LoadSeen()
        {
            if (_seen != null) return;
            _seen = new HashSet<string>(PlayerPrefs.GetString(SeenKey, "").Split(','));
            _seen.Remove("");
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
