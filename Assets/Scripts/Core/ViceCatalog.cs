using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core.Vices;

namespace HouseEdge.Core
{
    // every Vice in the game, in the order DESIGN.md lists them
    public static class ViceCatalog
    {
        private static ViceDefinition V(string name, Rarity rarity, Archetype type, string text, System.Func<Vice> create) =>
            new ViceDefinition(name, rarity, type, text, create);

        public static readonly IReadOnlyList<ViceDefinition> All = new List<ViceDefinition>
        {
            V("Lucky Penny", Rarity.Common, Archetype.Lucky, "+4 Mult on any win.", () => new LuckyPenny()),
            V("Hot Streak", Rarity.Common, Archetype.Combo, "+2 Mult per consecutive win this table. Resets on a loss.", () => new HotStreak()),
            V("Low Roller", Rarity.Common, Archetype.Steady, "+8 Chips for each 2 through 5 in your hand.", () => new LowRoller()),
            V("High Roller", Rarity.Common, Archetype.Risky, "+2 Mult for each 10-value card in your hand.", () => new HighRoller()),
            V("Card Sharp", Rarity.Common, Archetype.Risky, "+25 Chips if your hand has 4 or more cards.", () => new CardSharp()),
            V("Photo Finish", Rarity.Common, Archetype.Risky, "+10 Mult if you beat the dealer by exactly 1.", () => new PhotoFinish()),
            V("Sore Loser", Rarity.Common, Archetype.Greedy, "Gain 3 gold whenever you lose a hand.", () => new SoreLoser()),
            V("Dealer's Friend", Rarity.Common, Archetype.Steady, "+30 Chips when the dealer busts.", () => new DealersFriend()),
            V("Pocket Pair", Rarity.Common, Archetype.Lucky, "+6 Mult if your first two cards are a pair.", () => new PocketPair()),
            V("Suit Up", Rarity.Common, Archetype.Lucky, "+5 Mult if every card in your hand shares a suit.", () => new SuitUp()),
            V("Penny Pincher", Rarity.Common, Archetype.Greedy, "+1 gold for each unused Burn at the end of a table.", () => new PennyPincher()),
            V("Espresso", Rarity.Common, Archetype.Steady, "+1 Hand at every table.", () => new Espresso()),
            V("Steady Hand", Rarity.Common, Archetype.Steady, "+20 Chips if you stand on 17 or lower and still win.", () => new SteadyHand()),
            V("Even Money", Rarity.Common, Archetype.Lucky, "+8 Mult if your final total is even.", () => new EvenMoney()),
            V("Poker Face", Rarity.Common, Archetype.Cheater, "All Heat you gain is reduced by 30%.", () => new PokerFace()),
            V("Side Hustle", Rarity.Common, Archetype.Greedy, "Side bets cost 1 less gold.", () => new SideHustle()),
            V("Coin Flip", Rarity.Common, Archetype.Lucky, "1 in 2 chance for +10 Mult.", () => new CoinFlip()),
            V("Iron Will", Rarity.Common, Archetype.Steady, "+3 Mult for each hand this table where you stood on 16 or less.", () => new IronWill()),

            V("Card Counter", Rarity.Uncommon, Archetype.Cheater, "Always see the next card in the deck. +3 Heat per hand.", () => new CardCounter()),
            V("X-Ray Specs", Rarity.Uncommon, Archetype.Cheater, "The dealer's hole card is dealt face up. +5 Heat per hand.", () => new XRaySpecs()),
            V("Soft Touch", Rarity.Uncommon, Archetype.Combo, "Soft hands (an Ace counted as 11) get x2 Mult.", () => new SoftTouch()),
            V("Split Personality", Rarity.Uncommon, Archetype.Combo, "You can split any two starting cards, not just pairs.", () => new SplitPersonality()),
            V("Bouncer", Rarity.Uncommon, Archetype.Steady, "Your first bust at each table counts as a push instead.", () => new Bouncer()),
            V("Stretch Limo", Rarity.Uncommon, Archetype.Risky, "Your bust limit is 22. The dealer's stays 21.", () => new StretchLimo()),
            V("Insurance Agent", Rarity.Uncommon, Archetype.Greedy, "Gain 4 gold whenever the dealer shows an Ace.", () => new InsuranceAgent()),
            V("Bankroll", Rarity.Uncommon, Archetype.Greedy, "+1 Mult for every 5 gold you hold.", () => new Bankroll()),
            V("Face Value", Rarity.Uncommon, Archetype.Combo, "J, Q and K each give +10 Chips and +1 Mult.", () => new FaceValue()),
            V("Fresh Felt", Rarity.Uncommon, Archetype.Steady, "The first hand at each table gets x3 Mult.", () => new FreshFelt()),
            V("Burnout", Rarity.Uncommon, Archetype.Combo, "Each Burn you use gives +3 Mult for the rest of the table.", () => new Burnout()),
            V("Pickpocket", Rarity.Uncommon, Archetype.Cheater, "When the dealer busts, their last card joins your deck as a Gilded copy. +5 Heat.", () => new Pickpocket()),
            V("Doubling Cube", Rarity.Uncommon, Archetype.Risky, "Gains +1 Mult permanently every time you win a double down.", () => new DoublingCube()),
            V("Sleight of Hand", Rarity.Uncommon, Archetype.Cheater, "Your first Burn each hand is free. +3 Heat when used.", () => new SleightOfHand()),
            V("Tell Reader", Rarity.Uncommon, Archetype.Cheater, "See the dealer's tell every hand.", () => new TellReader()),
            V("Bookie", Rarity.Uncommon, Archetype.Greedy, "Winning a side bet also gives +5 Mult to that hand.", () => new Bookie()),
            V("Daredevil", Rarity.Uncommon, Archetype.Risky, "+4 Mult for every card you hit on 15 or higher.", () => new Daredevil()),
            V("Horseshoe", Rarity.Uncommon, Archetype.Lucky, "1 in 4 chance a bust turns into a 21.", () => new Horseshoe()),
            V("Snowball", Rarity.Uncommon, Archetype.Combo, "+5 Chips permanently every win. Resets when you bust.", () => new Snowball()),
            V("Patience", Rarity.Uncommon, Archetype.Steady, "Gains +1 Mult permanently each hand you win without hitting.", () => new Patience()),

            V("Rigged Shoe", Rarity.Rare, Archetype.Cheater, "The dealer must hit until they reach 18. +5 Heat per hand.", () => new RiggedShoe()),
            V("Lucky Sevens", Rarity.Rare, Archetype.Lucky, "Each 7 in your hand gives x1.5 Mult. Three 7s gives x7.", () => new LuckySevens()),
            V("Mirror", Rarity.Rare, Archetype.Combo, "Copies the Vice to its right.", () => new Mirror()),
            V("Black Cat", Rarity.Rare, Archetype.Risky, "When you bust, your next hand gets x3 Mult.", () => new BlackCat()),
            V("All In", Rarity.Rare, Archetype.Risky, "x4 Mult on every hand, but you earn no interest.", () => new AllIn()),
            V("Last Call", Rarity.Rare, Archetype.Steady, "Your final hand at each table gets x3 Mult.", () => new LastCall()),
            V("Twins", Rarity.Rare, Archetype.Combo, "Split hands are scored twice.", () => new TwinsVice()),
            V("Cold Read", Rarity.Rare, Archetype.Cheater, "After you stand, see the dealer's next card and choose whether they take it. +8 Heat.", () => new ColdRead()),
            V("Card Mechanic", Rarity.Rare, Archetype.Cheater, "Once per hand, swap your last card with the dealer's face-up card. +10 Heat.", () => new CardMechanicVice()),
            V("Smooth Talker", Rarity.Rare, Archetype.Cheater, "+1 Mult for every 5 Heat you have.", () => new SmoothTalker()),
            V("Knife's Edge", Rarity.Rare, Archetype.Risky, "x3 Mult at exactly 21, but x0.5 Mult at 18 or lower.", () => new KnifesEdge()),
            V("Double or Bust", Rarity.Rare, Archetype.Risky, "You can Double Down after hitting.", () => new DoubleOrBust()),
            V("Rabbit's Foot", Rarity.Rare, Archetype.Lucky, "Every chance-based effect rolls twice and keeps the better result.", () => new RabbitsFoot()),
            V("Loan Officer", Rarity.Rare, Archetype.Greedy, "Loan Shark debt doesn't grow.", () => new LoanOfficer()),
            V("Cashier", Rarity.Rare, Archetype.Steady, "Unused Hands pay 3 gold each instead of 1.", () => new Cashier()),
            V("Echo", Rarity.Rare, Archetype.Combo, "Your first card in each hand is scored twice.", () => new Echo()),

            V("Midas", Rarity.Legendary, Archetype.Greedy, "Every card in a winning hand becomes Gilded.", () => new Midas()),
            V("Natural Born", Rarity.Legendary, Archetype.Lucky, "Blackjacks get x5 Mult and can't be tied by a dealer blackjack.", () => new NaturalBorn()),
            V("Lady Luck", Rarity.Legendary, Archetype.Lucky, "All odds are doubled (Horseshoe, Coin Flip, shop rarity).", () => new LadyLuck()),
            V("High Stakes", Rarity.Legendary, Archetype.Risky, "Gains x0.25 Mult each time you hit on 17 or higher and don't bust.", () => new HighStakes()),
            V("The Dealer", Rarity.Legendary, Archetype.Cheater, "Once per table, you decide whether the dealer hits or stands. +15 Heat.", () => new TheDealerVice()),
            V("Whale", Rarity.Legendary, Archetype.Greedy, "x1 Mult for every 10 gold you hold.", () => new Whale()),
            V("Chain Reaction", Rarity.Legendary, Archetype.Combo, "Every time another Vice triggers, x1.1 Mult.", () => new ChainReaction()),

            V("Dirty Money", Rarity.Cursed, Archetype.Greedy, "+12 Mult on every win, but +5 Heat per win.", () => new DirtyMoney()),
            V("Devil's Deal", Rarity.Cursed, Archetype.Risky, "x3 Mult on every hand, but you start each table with 1 fewer Hand.", () => new DevilsDeal()),
            V("Bad Habit", Rarity.Cursed, Archetype.Risky, "+50 Chips, but you must hit at least once every hand.", () => new BadHabit()),
            V("Borrowed Time", Rarity.Cursed, Archetype.Steady, "+2 Hands per table, but each one costs 2 gold.", () => new BorrowedTime()),
        };

        public static ViceDefinition Get(string id) => All.First(d => d.Id == id);

        public static IEnumerable<ViceDefinition> OfRarity(Rarity rarity) => All.Where(d => d.Rarity == rarity);
    }
}
