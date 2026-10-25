using System.Collections.Generic;

namespace HouseEdge.Core
{
    // the run's rival: one gambler with a grudge who takes the dealer's seat at the High Table every other floor.
    // both hands are face up and the rival draws until beating you. every win against them brings back a new trick
    public sealed class Rival : Dealer
    {
        public static readonly IReadOnlyList<string> Names = new[]
        {
            "Silas Crow", "Vincent Ash", "Jack Marlowe", "Felix Vane", "August Rook", "Dorian Slate",
        };

        // picked from the seed, so it costs no random draws and every run of a seed meets the same rival
        public static string NameFor(int seed) => Names[(int)((uint)seed % (uint)Names.Count)];

        public static bool SitsAt(int floor, TableKind stage) => stage == TableKind.High && floor % Balance.RivalEveryFloors == 0;

        private readonly string _name;

        // 1 the first time you meet, then 2, 3, 4
        public int Meeting { get; }

        public Rival(string name, int meeting)
        {
            _name = name;
            Meeting = meeting;
        }

        public override string Name => _name;

        private string First => _name.Split(' ')[0];

        public override string Style => Meeting switch
        {
            1 => $"“So you're the new money.” Both hands face up, and {First} draws until beating yours.",
            2 => $"“The first one was luck.” Draws until beating you, and now hits on ties too.",
            3 => $"“I've learned a trick or two.” Chases you, hits on ties, and palms the first card that would bust.",
            _ => $"“Only one of us walks into The House.” Every trick so far, and you get {Balance.RivalFinalHandsLess} fewer Hand.",
        };

        public override void OnTableStart(Table table)
        {
            table.Rules.HoleCardFaceUp = true;
            table.Rules.DealerChasesPlayer = true;
            if (Meeting >= 2) table.Rules.DealerHitsOnTies = true;
            if (Meeting >= 3) table.Rules.DealerRedrawsFirstBust = true;
            if (Meeting >= 4) table.AddHands(-Balance.RivalFinalHandsLess);
        }

        public override void OnTableEnd(Table table, bool won)
        {
            if (won) table.Gambler.GainGold(Balance.RivalBeatenGold);
        }

        // everything's face up: there's nothing to read
        public override string Tell(Table table) => null;
        public override string TellMeaning(Table table) => null;
    }
}
