using System;

namespace HouseEdge.Core
{
    // all the tuning numbers live here. starting values come from DESIGN.md
    public static class Balance
    {
        // blackjack rules
        public const int BustLimit = 21;
        public const int DealerStandsOn = 17;
        public const int CharlieCards = 5;
        public const int MaxSplitsPerHand = 1;

        // per table
        // DESIGN.md starts at 4. the balance sim showed 4 Hands of blackjack lose ~10% of tables to
        // plain bad luck (no winning hand at all), which compounds over 24 tables, so it's 5
        public const int StartingHands = 5;
        public const int StartingBurns = 3;
        public const long FirstTableTarget = 50;

        // run
        public const int StartingGold = 4;
        public const int Floors = 8;

        // [floor - 1] = Low, High, Pit Boss. floor 8's boss is The House.
        // tuned with Tools/BalanceSim: Low grows ~1.7x a floor, High is 1.5x Low, the boss 2x
        // (The House 2.5x, it has 2 extra Hands). DESIGN.md's first draft started at 150 and doubled
        // every floor, which the bot never beat. see README "Balance"
        private static readonly long[][] TableTargets =
        {
            new long[] { 50, 75, 100 },
            new long[] { 85, 130, 170 },
            new long[] { 145, 220, 290 },
            new long[] { 250, 375, 500 },
            new long[] { 420, 630, 840 },
            new long[] { 710, 1070, 1420 },
            new long[] { 1200, 1800, 2400 },
            new long[] { 2050, 3100, 5100 },
        };

        // past floor 8 (Endless) targets double every floor
        public static long Target(int floor, TableKind kind) => floor <= Floors
            ? TableTargets[floor - 1][(int)kind]
            : TableTargets[Floors - 1][(int)kind] * (1L << Math.Min(floor - Floors, 40));

        public const int MarkedDeckHeat = 30;

        // economy
        public const int ViceSlots = 5;
        public const int InterestStep = 5;
        public const int InterestCap = 5;
        public const int GoldPerUnusedHand = 1;

        public static int TablePayout(TableKind kind) => kind switch
        {
            TableKind.Low => 3,
            TableKind.High => 4,
            _ => 5,
        };

        public static int Interest(int gold, int cap) => Math.Min(gold / InterestStep, cap);

        // shop
        public const int ShopViceOffers = 3;
        public const int RerollBaseCost = 2;

        public static int VicePrice(Rarity rarity) => rarity switch
        {
            Rarity.Common => 4,
            Rarity.Uncommon => 6,
            Rarity.Rare => 8,
            Rarity.Cursed => 2,
            _ => 12,
        };

        // you get half back, rounded down, at least 1. Cursed Vices sell for 1
        public static int SellPrice(int price) => Math.Max(1, price / 2);

        // per shop slot: 70% common, 25% uncommon, 5% rare. legendaries never show up in the shop
        public static Rarity RollShopRarity(GameRandom random)
        {
            int roll = random.Next(100);
            if (roll < 70) return Rarity.Common;
            return roll < 95 ? Rarity.Uncommon : Rarity.Rare;
        }

        // consumables and Comps
        public const int CocktailSlots = 2;
        public const int CocktailPrice = 3;
        public const int CompPrice = 4;
        public const int ShopCocktailOffers = 2;
        public const int ShopCompOffers = 1;
        public const int BossRewardChoices = 2;

        // cards
        public const int GildedGold = 3;
        public const int FragileBreaksAfter = 3;
        public const int MarkedHeat = 2;

        // heat
        public const int MaxHeat = 100;
        public const int HeatCooldownPerTable = 10;
        // one hand scoring over 3x the table's target draws eyes
        public const int BigHandFactor = 3;
        public const int BigHandHeat = 10;
        public const int WatchedReshuffleEvery = 2;
        public const double SuspectedPriceFactor = 1.25;

        public static HeatLevel HeatLevelFor(int heat) =>
            heat >= MaxHeat ? HeatLevel.Busted
            : heat >= 75 ? HeatLevel.Flagged
            : heat >= 50 ? HeatLevel.Suspected
            : heat >= 25 ? HeatLevel.Watched
            : HeatLevel.Clean;

        // bosses
        public const int TheHouseExtraHands = 2;
        public const int TheHouseTwistEvery = 2;
        public const int PitBossHeatPerHand = 10;
        public const int TaxmanHitCost = 1;
        public const int EyeInTheSkyTargetFactor = 2;
        public const int EnforcerTake = 3;

        // floor map and rooms
        public const int RoomChoices = 2;
        // the chance (in 100) that a stop on the map is a Shop. the first row after each table always has one
        public const int PathShopChance = 30;
        public const int BarCocktails = 3;
        // half of a normal Cocktail, rounded up
        public const int BarCocktailPrice = 2;
        public const int UsedRarePrice = 6;
        public static readonly int[] LoanAmounts = { 10, 20, 30 };
        public const double DebtGrowth = 1.5;
        public const int MinDeckSize = 20;
        public const int HeatClearCostPer10 = 2;
        public const int BribeHeat = 20;
        public const double LoungeTargetFactor = 1.5;

        // archetype set bonuses (3 Vices of the same archetype)
        public const int ArchetypeSetSize = 3;
        public const int GreedyInterestCapBonus = 3;

        // actions
        public const int AnteUpLoss = 3;
        public const double AnteUpMult = 2;
        public const int HotHandMult = 5;
        public const int CardMechanicHeat = 10;
        public const int ColdReadHeat = 8;
        public const int TheDealerHeat = 15;
        public const int SurrenderCost = 3;
        public const int InsuranceCost = 2;
        public const double DoubleDownMult = 2;

        // scoring: Chips = card total + base Chips, Mult = base Mult
        public const int DealerBustBonusChips = 15;
        public const int DealerBustBonusMult = 0;

        public static int BaseChips(Outcome outcome) => outcome switch
        {
            Outcome.Win => 10,
            Outcome.TwentyOne => 20,
            Outcome.Blackjack => 30,
            Outcome.Charlie => 40,
            _ => 0,
        };

        public static double BaseMult(Outcome outcome) => outcome switch
        {
            Outcome.Win => 2,
            Outcome.TwentyOne => 3,
            Outcome.Blackjack => 4,
            Outcome.Charlie => 4,
            Outcome.Push => 1,
            _ => 0,
        };

        // a push scores its cards, a loss or bust scores nothing
        public static bool ScoresCards(Outcome outcome) => outcome != Outcome.Lose && outcome != Outcome.Bust && outcome != Outcome.Surrender;
    }
}
