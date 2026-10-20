using System.Collections.Generic;

namespace HouseEdge.Core
{
    // a table is a seed plus the player's inputs, so replaying them rebuilds it exactly
    public static class Replay
    {
        public static Table Table(int seed, long target, IEnumerable<PlayerAction> inputs)
        {
            var table = new Table(seed, target);
            foreach (var action in inputs) table.Apply(action);
            return table;
        }
    }
}
