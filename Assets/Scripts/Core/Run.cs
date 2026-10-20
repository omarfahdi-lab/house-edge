using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    public enum TableKind { Low, High, Boss }

    public enum RunPhase
    {
        // playing blackjack at Run.Table
        AtTable,
        // a table was beaten and paid out. buy, sell and reorder Vices, then leave
        Shop,
        Won,
        Lost,
    }

    // gold for beating a table, shown at the top of the shop
    public readonly struct TablePayout
    {
        public int Base { get; }
        public int UnusedHands { get; }
        public int Interest { get; }
        public int Total => Base + UnusedHands + Interest;

        public TablePayout(int @base, int unusedHands, int interest)
        {
            Base = @base;
            UnusedHands = unusedHands;
            Interest = interest;
        }
    }

    // a full climb: 8 floors of Low Table, High Table and Pit Boss. one seed drives all of it
    public sealed class Run
    {
        private readonly List<RunInput> _inputs = new List<RunInput>();

        public int Seed { get; }
        public GameRandom Random { get; }
        public Gambler Gambler { get; }

        public int Floor { get; private set; } = 1;
        public TableKind Stage { get; private set; } = TableKind.Low;
        public Table Table { get; private set; }
        public RunPhase Phase { get; private set; }
        public int TablesBeaten { get; private set; }
        public Shop Shop { get; } = new Shop();
        public TablePayout LastPayout { get; private set; }

        public IReadOnlyList<RunInput> Inputs => _inputs;
        public bool IsOver => Phase == RunPhase.Won || Phase == RunPhase.Lost;

        public Run(int seed)
        {
            Seed = seed;
            Random = new GameRandom(seed);
            Gambler = new Gambler();
            StartTable();
        }

        public static Run Replay(int seed, IEnumerable<RunInput> inputs)
        {
            var run = new Run(seed);
            foreach (var input in inputs) run.Apply(input);
            return run;
        }

        public long CurrentTarget => Balance.Target(Floor, Stage);

        public bool IsLegal(RunInput input)
        {
            switch (input.Kind)
            {
                case RunInputKind.Table:
                    return Phase == RunPhase.AtTable && Table.IsLegal(input.Action);
                case RunInputKind.BuyVice:
                    if (Phase != RunPhase.Shop || !InRange(input.Index, Shop.Offers.Count)) return false;
                    var offer = Shop.Offers[input.Index];
                    return !offer.Sold && Gambler.HasFreeViceSlot && Gambler.CanAfford(offer.Price);
                case RunInputKind.SellVice:
                    return Phase == RunPhase.Shop && InRange(input.Index, Gambler.Vices.Count);
                case RunInputKind.MoveVice:
                    return Phase == RunPhase.Shop && InRange(input.Index, Gambler.Vices.Count)
                        && InRange(input.Index2, Gambler.Vices.Count) && input.Index != input.Index2;
                case RunInputKind.Reroll:
                    return Phase == RunPhase.Shop && Gambler.CanAfford(Shop.RerollCost);
                case RunInputKind.LeaveShop:
                    return Phase == RunPhase.Shop;
                default:
                    return false;
            }
        }

        private static bool InRange(int index, int count) => index >= 0 && index < count;

        public void Apply(RunInput input)
        {
            if (!IsLegal(input)) throw new InvalidOperationException($"{input} isn't allowed during {Phase}.");
            _inputs.Add(input);

            switch (input.Kind)
            {
                case RunInputKind.Table:
                    Table.Apply(input.Action);
                    if (Table.IsOver) FinishTable();
                    break;
                case RunInputKind.BuyVice:
                    var offer = Shop.Offers[input.Index];
                    Gambler.LoseGold(offer.Price);
                    offer.Sold = true;
                    Gambler.Vices.Add(offer.Vice.Create());
                    break;
                case RunInputKind.SellVice:
                    var sold = Gambler.Vices[input.Index];
                    Gambler.Vices.RemoveAt(input.Index);
                    Gambler.GainGold(SellValue(sold));
                    break;
                case RunInputKind.MoveVice:
                    var moved = Gambler.Vices[input.Index];
                    Gambler.Vices.RemoveAt(input.Index);
                    Gambler.Vices.Insert(input.Index2, moved);
                    break;
                case RunInputKind.Reroll:
                    Gambler.LoseGold(Shop.RerollCost);
                    Shop.Rerolled();
                    Shop.Stock(Gambler, Random);
                    break;
                case RunInputKind.LeaveShop:
                    NextTable();
                    break;
            }
        }

        public int SellValue(Vice vice) => Balance.SellPrice(Balance.VicePrice(vice.Definition.Rarity));

        private void StartTable()
        {
            Table = new Table(Gambler, Random, CurrentTarget, Gambler.Vices);
            Phase = RunPhase.AtTable;
        }

        private void FinishTable()
        {
            if (Table.Phase == TablePhase.Lost)
            {
                Phase = RunPhase.Lost;
                return;
            }
            TablesBeaten++;
            if (Floor == Balance.Floors && Stage == TableKind.Boss)
            {
                Phase = RunPhase.Won;
                return;
            }

            // interest is on the gold you held when the table ended, before this payout
            LastPayout = new TablePayout(
                Balance.TablePayout(Stage),
                Table.HandsLeft * Balance.GoldPerUnusedHand,
                Balance.Interest(Gambler.Gold, Balance.InterestCap));
            Gambler.GainGold(LastPayout.Total);

            Shop.Reset();
            Shop.Stock(Gambler, Random);
            Phase = RunPhase.Shop;
        }

        private void NextTable()
        {
            if (Stage == TableKind.Boss)
            {
                Floor++;
                Stage = TableKind.Low;
            }
            else Stage++;
            StartTable();
        }
    }
}
