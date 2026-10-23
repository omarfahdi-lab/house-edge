using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    public enum OfferKind { Vice, Cocktail, Comp, Pack }

    public sealed class ShopOffer
    {
        public OfferKind Kind { get; }
        public ViceDefinition Vice { get; }
        public CocktailKind Cocktail { get; }
        public Comp Comp { get; }
        public PackKind Pack { get; }
        // before Heat. see Run.PriceOf
        public int BasePrice { get; }
        public bool Sold { get; internal set; }

        private ShopOffer(OfferKind kind, int basePrice, ViceDefinition vice = null, CocktailKind cocktail = default, Comp comp = default, PackKind pack = default)
        {
            Pack = pack;
            Kind = kind;
            BasePrice = basePrice;
            Vice = vice;
            Cocktail = cocktail;
            Comp = comp;
        }

        public static ShopOffer ForVice(ViceDefinition vice) => new ShopOffer(OfferKind.Vice, Balance.VicePrice(vice.Rarity), vice: vice);
        public static ShopOffer ForCocktail(CocktailKind cocktail) => new ShopOffer(OfferKind.Cocktail, Balance.CocktailPrice, cocktail: cocktail);
        public static ShopOffer ForComp(Comp comp) => new ShopOffer(OfferKind.Comp, Balance.CompPrice, comp: comp);
        public static ShopOffer ForPack(PackKind pack) => new ShopOffer(OfferKind.Pack, Packs.Price(pack), pack: pack);

        public string Name => Kind switch
        {
            OfferKind.Vice => Vice.Name,
            OfferKind.Cocktail => Cocktails.Name(Cocktail),
            OfferKind.Pack => Packs.Name(Pack),
            _ => Comps.Name(Comp) + " Comp",
        };

        public string Description => Kind switch
        {
            OfferKind.Vice => Vice.Description,
            OfferKind.Cocktail => Cocktails.Describe(Cocktail),
            OfferKind.Pack => Packs.Describe(Pack),
            _ => Comps.Describe(Comp),
        };
    }

    // opens after every table you beat: Vices, Cocktails and a Comp
    public sealed class Shop
    {
        private readonly List<ShopOffer> _offers = new List<ShopOffer>();

        public IReadOnlyList<ShopOffer> Offers => _offers;
        public int RerollCost { get; private set; } = Balance.RerollBaseCost;

        internal void Stock(Gambler gambler, GameRandom random, IEnumerable<string> locked = null)
        {
            _offers.Clear();
            var taken = new HashSet<string>(gambler.Vices.Select(v => v.Definition.Id));
            if (locked != null) taken.UnionWith(locked);
            for (int i = 0; i < Balance.ShopViceOffers; i++)
            {
                var rarity = Balance.RollShopRarity(random);
                if (gambler.Vices.Any(v => !v.Disabled && v.LuckyShop))
                {
                    var second = Balance.RollShopRarity(random);
                    if (second > rarity) rarity = second;
                }
                var vice = RollVice(random, taken, rarity);
                if (vice == null) break;
                taken.Add(vice.Id);
                _offers.Add(ShopOffer.ForVice(vice));
            }
            var cocktails = Cocktails.All.ToList();
            for (int i = 0; i < Balance.ShopCocktailOffers && cocktails.Count > 0; i++)
            {
                var pick = cocktails[random.Next(cocktails.Count)];
                cocktails.Remove(pick);
                _offers.Add(ShopOffer.ForCocktail(pick));
            }
            for (int i = 0; i < Balance.ShopCompOffers; i++)
                _offers.Add(ShopOffer.ForComp(Comps.All[random.Next(Comps.All.Count)]));
            for (int i = 0; i < Balance.ShopPackOffers; i++)
                _offers.Add(ShopOffer.ForPack(Packs.All[random.Next(Packs.All.Count)]));
        }

        internal void Rerolled() => RerollCost++;

        // each shop visit starts at the base reroll cost
        internal void Reset() => RerollCost = Balance.RerollBaseCost;

        // a Vice of that rarity you don't have yet, falling back to the next rarity down, then up
        internal static ViceDefinition RollVice(GameRandom random, ICollection<string> exclude, Rarity rarity)
        {
            var order = new List<Rarity> { rarity };
            for (var r = rarity - 1; r >= Rarity.Common; r--) order.Add(r);
            for (var r = rarity + 1; r <= Rarity.Rare; r++) order.Add(r);
            foreach (var r in order)
            {
                // catalog order, not a hash set, so the pick is the same for a given seed
                var pool = ViceCatalog.OfRarity(r).Where(d => !exclude.Contains(d.Id)).ToList();
                if (pool.Count > 0) return pool[random.Next(pool.Count)];
            }
            return null;
        }

        public static int HeatAdjusted(int price, Gambler gambler) =>
            gambler.HeatLevel >= HeatLevel.Suspected ? (int)Math.Ceiling(price * Balance.SuspectedPriceFactor) : price;
    }
}
