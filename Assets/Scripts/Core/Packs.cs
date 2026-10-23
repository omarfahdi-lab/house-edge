using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    // booster packs from the shop: open one, keep 1 of its 3 cards for your deck
    public enum PackKind { Card, Suit, Court, Lucky }

    public static class Packs
    {
        public static readonly IReadOnlyList<PackKind> All = (PackKind[])System.Enum.GetValues(typeof(PackKind));

        public static string Name(PackKind pack) => pack switch
        {
            PackKind.Suit => "Suit Pack",
            PackKind.Court => "Court Pack",
            PackKind.Lucky => "Lucky Pack",
            _ => "Card Pack",
        };

        public static string Describe(PackKind pack) => pack switch
        {
            PackKind.Suit => "3 cards of one suit, one of them Wild. Builds Flushes.",
            PackKind.Court => "3 face cards, one of them upgraded. Builds Royal Courts and Pairs.",
            PackKind.Lucky => "3 cards that are all upgraded or stamped.",
            _ => "3 random cards. Some are upgraded or stamped.",
        };

        public static int Price(PackKind pack) => pack switch
        {
            PackKind.Lucky => 5,
            PackKind.Card => 3,
            _ => 4,
        };

        private static readonly Enhancement[] Upgrades =
            { Enhancement.Gilded, Enhancement.Flex, Enhancement.Fragile, Enhancement.Hot, Enhancement.Heavy, Enhancement.Wild };

        private static readonly Stamp[] Stamps = { Stamp.Red, Stamp.Blue, Stamp.Gold };

        // the three cards inside, rolled from the run's random so a seed always opens the same pack
        internal static List<Card> Open(PackKind pack, GameRandom random)
        {
            var cards = new List<Card>();
            var suit = (Suit)random.Next(4);
            for (int i = 0; i < Balance.PackCards; i++)
            {
                var card = pack switch
                {
                    PackKind.Suit => new Card((Rank)(2 + random.Next(13)), suit),
                    PackKind.Court => new Card(Rank.Jack + random.Next(3), (Suit)random.Next(4)),
                    _ => new Card((Rank)(2 + random.Next(13)), (Suit)random.Next(4)),
                };
                bool special = pack switch
                {
                    PackKind.Lucky => true,
                    PackKind.Suit or PackKind.Court => i == 0,
                    _ => random.Next(100) < 35,
                };
                if (special)
                {
                    if (pack == PackKind.Suit) card.Enhancement = Enhancement.Wild;
                    else if (random.Next(3) == 0) card.Stamp = Stamps[random.Next(Stamps.Length)];
                    else card.Enhancement = Upgrades[random.Next(Upgrades.Length)];
                }
                cards.Add(card);
            }
            return cards;
        }
    }
}
