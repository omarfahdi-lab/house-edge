using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core.Vices;

namespace HouseEdge.Core
{
    // every Vice in the game, in the order DESIGN.md lists them
    public static class ViceCatalog
    {
        public static readonly IReadOnlyList<ViceDefinition> All = new List<ViceDefinition>
        {
            new ViceDefinition("Lucky Penny", Rarity.Common, Archetype.Lucky, "+4 Mult on any win.", () => new LuckyPenny()),
            new ViceDefinition("Hot Streak", Rarity.Common, Archetype.Combo, "+2 Mult per consecutive win this table. Resets on a loss.", () => new HotStreak()),
            new ViceDefinition("Low Roller", Rarity.Common, Archetype.Steady, "+8 Chips for each 2 through 5 in your hand.", () => new LowRoller()),
            new ViceDefinition("High Roller", Rarity.Common, Archetype.Risky, "+2 Mult for each 10-value card in your hand.", () => new HighRoller()),
            new ViceDefinition("Card Sharp", Rarity.Common, Archetype.Risky, "+25 Chips if your hand has 4 or more cards.", () => new CardSharp()),
            new ViceDefinition("Photo Finish", Rarity.Common, Archetype.Risky, "+10 Mult if you beat the dealer by exactly 1.", () => new PhotoFinish()),
            new ViceDefinition("Sore Loser", Rarity.Common, Archetype.Greedy, "Gain 3 gold whenever you lose a hand.", () => new SoreLoser()),
            new ViceDefinition("Dealer's Friend", Rarity.Common, Archetype.Steady, "+30 Chips when the dealer busts.", () => new DealersFriend()),
            new ViceDefinition("Pocket Pair", Rarity.Common, Archetype.Lucky, "+6 Mult if your first two cards are a pair.", () => new PocketPair()),
            new ViceDefinition("Suit Up", Rarity.Common, Archetype.Lucky, "+5 Mult if every card in your hand shares a suit.", () => new SuitUp()),
            new ViceDefinition("Penny Pincher", Rarity.Common, Archetype.Greedy, "+1 gold for each unused Burn at the end of a table.", () => new PennyPincher()),
            new ViceDefinition("Espresso", Rarity.Common, Archetype.Steady, "+1 Hand at every table.", () => new Espresso()),
            new ViceDefinition("Steady Hand", Rarity.Common, Archetype.Steady, "+20 Chips if you stand on 17 or lower and still win.", () => new SteadyHand()),
            new ViceDefinition("Even Money", Rarity.Common, Archetype.Lucky, "+8 Mult if your final total is even.", () => new EvenMoney()),
            new ViceDefinition("Coin Flip", Rarity.Common, Archetype.Lucky, "1 in 2 chance for +10 Mult.", () => new CoinFlip()),
            new ViceDefinition("Iron Will", Rarity.Common, Archetype.Steady, "+3 Mult for each hand this table where you stood on 16 or less.", () => new IronWill()),
            new ViceDefinition("Soft Touch", Rarity.Uncommon, Archetype.Combo, "Soft hands (an Ace counted as 11) get x2 Mult.", () => new SoftTouch()),
            new ViceDefinition("Bouncer", Rarity.Uncommon, Archetype.Steady, "Your first bust at each table counts as a push instead.", () => new Bouncer()),
            new ViceDefinition("Face Value", Rarity.Uncommon, Archetype.Combo, "J, Q and K each give +10 Chips and +1 Mult.", () => new FaceValue()),
            new ViceDefinition("Fresh Felt", Rarity.Uncommon, Archetype.Steady, "The first hand at each table gets x3 Mult.", () => new FreshFelt()),

            // cursed
            new ViceDefinition("Dirty Money", Rarity.Cursed, Archetype.Greedy, "+12 Mult on every win, but +5 Heat per win.", () => new DirtyMoney()),
            new ViceDefinition("Devil's Deal", Rarity.Cursed, Archetype.Risky, "x3 Mult on every hand, but you start each table with 1 fewer Hand.", () => new DevilsDeal()),
            new ViceDefinition("Bad Habit", Rarity.Cursed, Archetype.Risky, "+50 Chips, but you must hit at least once every hand.", () => new BadHabit()),
            new ViceDefinition("Borrowed Time", Rarity.Cursed, Archetype.Steady, "+2 Hands per table, but each one costs 2 gold.", () => new BorrowedTime()),
        };

        public static ViceDefinition Get(string id) => All.First(d => d.Id == id);

        public static IEnumerable<ViceDefinition> OfRarity(Rarity rarity) => All.Where(d => d.Rarity == rarity);
    }
}
