using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    // everything the player carries from table to table in a run
    public sealed class Gambler
    {
        public int Gold { get; private set; }
        public List<Card> Deck { get; }

        public Gambler(int gold = Balance.StartingGold, IEnumerable<Card> deck = null)
        {
            Gold = gold;
            Deck = new List<Card>(deck ?? Shoe.StandardDeck());
        }

        public bool CanAfford(int cost) => Gold >= cost;

        public void GainGold(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Gold += amount;
        }

        // never goes below 0
        public int LoseGold(int amount)
        {
            int lost = Math.Min(Math.Max(amount, 0), Gold);
            Gold -= lost;
            return lost;
        }
    }
}
