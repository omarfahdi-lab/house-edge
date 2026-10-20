using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    public sealed class ShopOffer
    {
        public ViceDefinition Vice { get; }
        public int Price { get; }
        public bool Sold { get; internal set; }

        public ShopOffer(ViceDefinition vice, int price)
        {
            Vice = vice;
            Price = price;
        }
    }

    // opens after every table you beat
    public sealed class Shop
    {
        private readonly List<ShopOffer> _offers = new List<ShopOffer>();

        public IReadOnlyList<ShopOffer> Offers => _offers;
        public int RerollCost { get; private set; } = Balance.RerollBaseCost;

        internal void Stock(Gambler gambler, GameRandom random)
        {
            _offers.Clear();
            var taken = new HashSet<string>(gambler.Vices.Select(v => v.Definition.Id));
            for (int i = 0; i < Balance.ShopViceOffers; i++)
            {
                var vice = RollVice(random, taken);
                if (vice == null) break;
                taken.Add(vice.Id);
                _offers.Add(new ShopOffer(vice, Balance.VicePrice(vice.Rarity)));
            }
        }

        internal void Rerolled() => RerollCost++;

        // each shop visit starts at the base reroll cost
        internal void Reset() => RerollCost = Balance.RerollBaseCost;

        // a rarity from the shop odds, then a Vice of that rarity you don't have yet.
        // falls back to the other rarities if that pool is empty
        internal static ViceDefinition RollVice(GameRandom random, ICollection<string> exclude)
        {
            var rarity = Balance.RollShopRarity(random);
            var order = new[] { rarity, Rarity.Common, Rarity.Uncommon, Rarity.Rare };
            foreach (var r in order)
            {
                // catalog order, not a hash set, so the pick is the same for a given seed
                var pool = ViceCatalog.OfRarity(r).Where(d => !exclude.Contains(d.Id)).ToList();
                if (pool.Count > 0) return pool[random.Next(pool.Count)];
            }
            return null;
        }
    }
}
