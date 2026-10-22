using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    // one stop on a floor's map: a Shop or a room
    public sealed class PathStop
    {
        public RoomKind Kind { get; }
        // 0 between the Low and High Tables, 1 between the High Table and the Pit Boss
        public int Section { get; }
        public int Row { get; }
        public int Lane { get; }
        public bool Visited { get; internal set; }

        internal PathStop(RoomKind kind, int section, int row, int lane)
        {
            Kind = kind;
            Section = section;
            Row = row;
            Lane = lane;
        }

        public override string ToString() => $"{Rooms.Name(Kind)} ({Section}, {Row}, {Lane})";
    }

    // a floor laid out like Slay the Spire: between each pair of tables, rows of stops in lanes joined by paths.
    // you can start in any lane, then follow a path forward. every stop leads to the next table at the end
    public sealed class FloorMap
    {
        public const int Sections = 2, Rows = 2, Lanes = 3;

        private readonly PathStop[,,] _stops = new PathStop[Sections, Rows, Lanes];
        // a path from (section, row, lane) to (section, row + 1, lane + offset), offset -1, 0 or 1
        private readonly HashSet<(int section, int row, int from, int to)> _paths = new HashSet<(int, int, int, int)>();

        public int Floor { get; }

        public PathStop Stop(int section, int row, int lane) => _stops[section, row, lane];

        public IEnumerable<PathStop> All()
        {
            for (int s = 0; s < Sections; s++)
                for (int r = 0; r < Rows; r++)
                    for (int l = 0; l < Lanes; l++)
                        yield return _stops[s, r, l];
        }

        public bool Linked(int section, int row, int from, int to) => _paths.Contains((section, row, from, to));

        // where you can go next: any first stop after a table, otherwise the stops your lane leads to
        public List<PathStop> NextFrom(int section, int row, int lane)
        {
            if (row < 0) return Enumerable.Range(0, Lanes).Select(l => _stops[section, 0, l]).ToList();
            if (row >= Rows - 1) return new List<PathStop>();
            return Enumerable.Range(0, Lanes).Where(l => Linked(section, row, lane, l)).Select(l => _stops[section, row + 1, l]).ToList();
        }

        internal FloorMap(int floor, GameRandom random, bool loungeBeforeBoss)
        {
            Floor = floor;
            for (int section = 0; section < Sections; section++)
            {
                for (int row = 0; row < Rows; row++)
                {
                    // the last row comes right before a table: only there can the lounge skip it
                    bool beforeTable = row == Rows - 1;
                    bool loungeAllowed = beforeTable && (section == 0 || loungeBeforeBoss);
                    var kinds = Roll(random, loungeAllowed, needShop: row == 0);
                    for (int lane = 0; lane < Lanes; lane++) _stops[section, row, lane] = new PathStop(kinds[lane], section, row, lane);
                }
                for (int row = 0; row < Rows - 1; row++) Link(section, row, random);
            }
        }

        // three different stops for one row. the first row after a table always has a Shop somewhere
        private static List<RoomKind> Roll(GameRandom random, bool loungeAllowed, bool needShop)
        {
            var pool = Rooms.All.Where(k => loungeAllowed || k != RoomKind.HighLimitLounge).ToList();
            var row = new List<RoomKind>();
            while (row.Count < Lanes)
            {
                // a Shop about one time in three, otherwise a room nobody else in the row has
                bool shop = !row.Contains(RoomKind.Shop) && random.Next(100) < Balance.PathShopChance;
                if (shop)
                {
                    row.Add(RoomKind.Shop);
                    continue;
                }
                var room = pool[random.Next(pool.Count)];
                pool.Remove(room);
                row.Add(room);
            }
            if (needShop && !row.Contains(RoomKind.Shop)) row[random.Next(Lanes)] = RoomKind.Shop;
            return row;
        }

        // every stop goes straight on; between neighbouring lanes there's sometimes a diagonal too, never two that cross
        private void Link(int section, int row, GameRandom random)
        {
            for (int lane = 0; lane < Lanes; lane++) _paths.Add((section, row, lane, lane));
            for (int lane = 0; lane < Lanes - 1; lane++)
            {
                switch (random.Next(3))
                {
                    case 1: _paths.Add((section, row, lane, lane + 1)); break;
                    case 2: _paths.Add((section, row, lane + 1, lane)); break;
                }
            }
        }
    }
}
