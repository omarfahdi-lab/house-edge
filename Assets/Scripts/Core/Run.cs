using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    public enum TableKind { Low, High, Boss }

    public enum RunPhase
    {
        // playing blackjack at Run.Table
        AtTable,
        // a Pit Boss fell: pick a Rare or Legendary Vice, or skip
        BossReward,
        // buy, sell and reorder, then leave
        Shop,
        // pick one of two rooms on the floor map
        RoomChoice,
        // inside Run.Room
        InRoom,
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

    // a full climb. each floor: Low Table, a room, High Table, a room, Pit Boss, with a shop after every table.
    // one seed drives all of it
    public sealed class Run
    {
        private readonly List<RunInput> _inputs = new List<RunInput>();
        private readonly List<string> _usedBosses = new List<string>();
        private readonly List<ViceDefinition> _rewards = new List<ViceDefinition>();
        private readonly List<RoomKind> _roomChoices = new List<RoomKind>();
        private bool _skipNextTable;
        private double _nextTargetFactor = 1;

        public int Seed { get; }
        public RunOptions Options { get; }
        public GameRandom Random { get; }
        public Gambler Gambler { get; }

        // records for the run-over screen and the profile
        public long TotalScore { get; private set; }
        public long BestHand { get; private set; }
        public double BestBlackjackMult { get; private set; }
        public bool WasBusted { get; private set; }
        public bool BeatTheHouse { get; private set; }
        public string Code => RunCodes.Encode(Seed, Options);

        public int Floor { get; private set; } = 1;
        public TableKind Stage { get; private set; } = TableKind.Low;
        public Table Table { get; private set; }
        public Boss Boss { get; private set; }
        public Dealer Dealer { get; private set; }
        public BossDefinition FloorBoss { get; private set; }
        public bool AtEyeInTheSky { get; private set; }
        public bool AtEnforcer { get; private set; }
        public RunPhase Phase { get; private set; }
        public int TablesBeaten { get; private set; }
        public Shop Shop { get; } = new Shop();
        public TablePayout LastPayout { get; private set; }
        public IReadOnlyList<ViceDefinition> Rewards => _rewards;
        public IReadOnlyList<RoomKind> RoomChoices => _roomChoices;
        public Room Room { get; private set; }
        // a Vice handed to you, e.g. for beating The Eye in the Sky
        public ViceDefinition LastFreeVice { get; private set; }

        public IReadOnlyList<RunInput> Inputs => _inputs;
        public bool IsOver => Phase == RunPhase.Won || Phase == RunPhase.Lost;

        // the table being played right now: the Back Room hand while it's on, otherwise the run's table
        public Table ActiveTable => Phase == RunPhase.InRoom && Room?.BackRoomTable != null && !Room.Used ? Room.BackRoomTable : Table;

        public Run(int seed, RunOptions options = null)
        {
            Seed = seed;
            Options = options?.Clone() ?? new RunOptions();
            Random = new GameRandom(seed);
            Gambler = new Gambler(Balance.StartingGold, Decks.Build(Options.Deck));
            if (Options.Deck == StartingDeck.MarkedDeck) Gambler.AddHeat(Balance.MarkedDeckHeat);
            if (Options.Stake >= Stake.Black) Gambler.Deck.Add(Card.Cursed(Curse.DeadCard));
            PickFloorBoss();
            StartTable();
        }

        public static Run Replay(int seed, IEnumerable<RunInput> inputs, RunOptions options = null)
        {
            var run = new Run(seed, options);
            foreach (var input in inputs) run.Apply(input);
            return run;
        }

        public long TargetFor(TableKind stage) => (long)Math.Round(Balance.Target(Floor, stage) * Stakes.TargetFactor(Options.Stake));
        public long CurrentTarget => Table?.Target ?? TargetFor(Stage);

        public int PriceOf(ShopOffer offer) => Shop.HeatAdjusted(offer.BasePrice, Gambler);
        public int RerollCost => Shop.HeatAdjusted(Shop.RerollCost, Gambler);
        public int SellValue(Vice vice) => Balance.SellPrice(Balance.VicePrice(vice.Definition.Rarity));

        // the stage after the current one, and whether it starts a new floor
        public (int floor, TableKind stage) NextStage => Stage == TableKind.Boss ? (Floor + 1, TableKind.Low) : (Floor, Stage + 1);

        public bool IsLegal(RunInput input)
        {
            bool betweenTables = Phase == RunPhase.Shop || Phase == RunPhase.BossReward || Phase == RunPhase.InRoom;
            switch (input.Kind)
            {
                case RunInputKind.Table:
                    return (Phase == RunPhase.AtTable || Phase == RunPhase.InRoom && ActiveTable != Table)
                        && !ActiveTable.IsOver && ActiveTable.IsLegal(input.Action);
                case RunInputKind.Buy:
                    return Phase == RunPhase.Shop && InRange(input.Index, Shop.Offers.Count) && CanBuy(Shop.Offers[input.Index]);
                case RunInputKind.SellVice:
                    return betweenTables && InRange(input.Index, Gambler.Vices.Count) && !InBackRoomHand;
                case RunInputKind.MoveVice:
                    return betweenTables && InRange(input.Index, Gambler.Vices.Count)
                        && InRange(input.Index2, Gambler.Vices.Count) && input.Index != input.Index2;
                case RunInputKind.Reroll:
                    return Phase == RunPhase.Shop && Gambler.CanAfford(RerollCost);
                case RunInputKind.LeaveShop:
                    return Phase == RunPhase.Shop;
                case RunInputKind.UseCocktail:
                    return CanUseCocktail(input);
                case RunInputKind.PickReward:
                    return Phase == RunPhase.BossReward && InRange(input.Index, _rewards.Count) && Gambler.HasFreeViceSlot;
                case RunInputKind.SkipReward:
                    return Phase == RunPhase.BossReward;
                case RunInputKind.ChooseRoom:
                    return Phase == RunPhase.RoomChoice && InRange(input.Index, _roomChoices.Count);
                case RunInputKind.RoomOption:
                    return Phase == RunPhase.InRoom && CanTakeRoomOption(input);
                case RunInputKind.LeaveRoom:
                    return Phase == RunPhase.InRoom && !InBackRoomHand;
                case RunInputKind.RepayDebt:
                    return Gambler.Debt > 0 && Gambler.Gold > 0 && (betweenTables || Phase == RunPhase.RoomChoice) && !InBackRoomHand;
                default:
                    return false;
            }
        }

        private bool InBackRoomHand => Phase == RunPhase.InRoom && Room.BackRoomTable != null && !Room.Used;

        private static bool InRange(int index, int count) => index >= 0 && index < count;

        private bool CanBuy(ShopOffer offer)
        {
            if (offer.Sold || !Gambler.CanAfford(PriceOf(offer))) return false;
            return offer.Kind switch
            {
                OfferKind.Vice => Gambler.HasFreeViceSlot,
                OfferKind.Cocktail => Gambler.HasFreeCocktailSlot,
                _ => true,
            };
        }

        public bool CanUseCocktail(RunInput input)
        {
            if (!InRange(input.Index, Gambler.Cocktails.Count) || IsOver) return false;
            var cocktail = Gambler.Cocktails[input.Index];
            bool atTable = Phase == RunPhase.AtTable && !Table.IsOver;
            bool betweenTables = Phase == RunPhase.Shop || Phase == RunPhase.BossReward || Phase == RunPhase.RoomChoice
                                 || Phase == RunPhase.InRoom && !InBackRoomHand;
            switch (cocktail.Use)
            {
                case CocktailUse.AtTable when !atTable:
                case CocktailUse.OnDeck when !betweenTables:
                case CocktailUse.Anytime when !atTable && !betweenTables:
                    return false;
            }
            if (cocktail.Kind == CocktailKind.WildCard && !Gambler.HasFreeViceSlot) return false;

            var targets = input.CardTargets().ToList();
            var (min, max) = cocktail.Targets;
            if (targets.Count < min || targets.Count > max || targets.Distinct().Count() != targets.Count) return false;
            return targets.All(i => InRange(i, Gambler.Deck.Count) && Cocktails.CanTarget(cocktail.Kind, Gambler.Deck[i]));
        }

        public void Apply(RunInput input)
        {
            if (!IsLegal(input)) throw new InvalidOperationException($"{input} isn't allowed during {Phase}.");
            _inputs.Add(input);

            switch (input.Kind)
            {
                case RunInputKind.Table:
                    if (ActiveTable != Table)
                    {
                        ActiveTable.Apply(input.Action);
                        SettleBackRoom();
                        break;
                    }
                    Table.Apply(input.Action);
                    if (Table.IsOver) FinishTable();
                    break;
                case RunInputKind.Buy:
                    Buy(Shop.Offers[input.Index]);
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
                    Gambler.LoseGold(RerollCost);
                    Shop.Rerolled();
                    Shop.Stock(Gambler, Random);
                    break;
                case RunInputKind.LeaveShop:
                    LeaveShop();
                    break;
                case RunInputKind.UseCocktail:
                    Drink(input);
                    break;
                case RunInputKind.PickReward:
                    Gambler.Vices.Add(_rewards[input.Index].Create());
                    OpenShop();
                    break;
                case RunInputKind.SkipReward:
                    OpenShop();
                    break;
                case RunInputKind.ChooseRoom:
                    EnterRoom(_roomChoices[input.Index]);
                    break;
                case RunInputKind.RoomOption:
                    TakeRoomOption(RoomOptions()[input.Index], input.Index2);
                    break;
                case RunInputKind.LeaveRoom:
                    LeaveRoom();
                    break;
                case RunInputKind.RepayDebt:
                    Gambler.RepayDebt();
                    break;
            }
        }

        private void Buy(ShopOffer offer)
        {
            Gambler.LoseGold(PriceOf(offer));
            offer.Sold = true;
            switch (offer.Kind)
            {
                case OfferKind.Vice: Gambler.Vices.Add(offer.Vice.Create()); break;
                case OfferKind.Cocktail: Gambler.Cocktails.Add(new Cocktail(offer.Cocktail)); break;
                case OfferKind.Comp: Gambler.LevelUp(offer.Comp); break;
            }
        }

        private void Drink(RunInput input)
        {
            var cocktail = Gambler.Cocktails[input.Index];
            Gambler.Cocktails.RemoveAt(input.Index);
            var cards = input.CardTargets().Select(i => Gambler.Deck[i]).ToList();
            switch (cocktail.Kind)
            {
                case CocktailKind.OldFashioned: foreach (var c in cards) c.Enhancement = Enhancement.Gilded; break;
                case CocktailKind.Martini: Gambler.Deck.Add(cards[0].Copy()); break;
                case CocktailKind.Screwdriver: foreach (var c in cards) Gambler.Deck.Remove(c); break;
                case CocktailKind.LongIsland: cards[0].Rank++; break;
                case CocktailKind.LastWord: cards[0].Rank--; break;
                case CocktailKind.Mojito: cards[0].Enhancement = Enhancement.Flex; break;
                case CocktailKind.Champagne: cards[0].Enhancement = Enhancement.Fragile; break;
                case CocktailKind.DarkAndStormy: cards[0].Stamp = Stamp.Red; break;
                case CocktailKind.IceWater: Table.AddBurns(2); break;
                case CocktailKind.WhiskeySour: Gambler.GainGold(2 * Gambler.Vices.Count); break;
                case CocktailKind.ColdBrew: Gambler.CoolDown(25); break;
                case CocktailKind.WildCard: AddRandomVice(Balance.RollShopRarity(Random)); break;
            }
        }

        private ViceDefinition AddRandomVice(Rarity rarity)
        {
            var vice = Shop.RollVice(Random, OwnedViceIds(), rarity);
            if (vice != null) Gambler.Vices.Add(vice.Create());
            return vice;
        }

        private List<string> OwnedViceIds() => Gambler.Vices.Select(v => v.Definition.Id).ToList();

        // tables

        private void PickFloorBoss()
        {
            if (Floor == Balance.Floors && !BeatTheHouse)
            {
                FloorBoss = Bosses.TheHouse;
                return;
            }
            var pool = Bosses.PitBosses.Where(b => !_usedBosses.Contains(b.Name)).ToList();
            if (pool.Count == 0) pool = Bosses.PitBosses.ToList();
            FloorBoss = pool[Random.Next(pool.Count)];
            _usedBosses.Add(FloorBoss.Name);
        }

        private void StartTable()
        {
            foreach (var vice in Gambler.Vices) vice.Disabled = false;
            if (Gambler.HeatLevel == HeatLevel.Flagged)
            {
                var cheaters = Gambler.Vices.Where(v => v.Definition.IsCheater).ToList();
                if (cheaters.Count > 0) cheaters[Random.Next(cheaters.Count)].Disabled = true;
            }

            var effects = new List<TableEffect>();
            Boss = AtEyeInTheSky ? Bosses.EyeInTheSky.Create()
                : AtEnforcer ? Bosses.Enforcer.Create()
                : Stage == TableKind.Boss ? FloorBoss.Create()
                : null;
            Dealer = Boss == null ? Dealers.Random(Random) : null;
            if (Boss != null) effects.Add(Boss);
            if (Dealer != null) effects.Add(Dealer);
            effects.Add(new RunRules(Options));
            effects.AddRange(Gambler.Vices);
            effects.Add(new RunStats(this));

            long target = (long)Math.Round(TargetFor(Stage) * (Boss?.TargetFactor ?? 1) * _nextTargetFactor);
            _nextTargetFactor = 1;
            Table = new Table(Gambler, Random, target, effects);
            Phase = RunPhase.AtTable;
        }

        private void FinishTable()
        {
            TotalScore += Table.Score;
            if (Table.Phase == TablePhase.Lost)
            {
                Phase = RunPhase.Lost;
                return;
            }
            TablesBeaten++;
            bool wasEye = AtEyeInTheSky;
            bool wasEnforcer = AtEnforcer;
            AtEyeInTheSky = false;
            AtEnforcer = false;
            LastFreeVice = null;
            if (Floor == Balance.Floors && Stage == TableKind.Boss && !wasEye && !wasEnforcer && !BeatTheHouse)
            {
                BeatTheHouse = true;
                if (!Options.Endless)
                {
                    Phase = RunPhase.Won;
                    return;
                }
            }

            PayOut(wasEye || wasEnforcer ? TableKind.Boss : Stage);
            Gambler.GrowDebt();

            if (wasEnforcer) Gambler.ClearDebt();
            if (wasEye)
            {
                // you beat security: a clean slate and a free Rare Vice
                Gambler.CoolDown(Balance.MaxHeat);
                if (Gambler.HasFreeViceSlot) LastFreeVice = AddRandomVice(Rarity.Rare);
            }
            else if (Gambler.Heat >= Balance.MaxHeat)
            {
                AtEyeInTheSky = true;
                WasBusted = true;
                StartTable();
                return;
            }
            else Gambler.CoolDown(Options.Stake >= Stake.Purple ? Balance.HeatCooldownPerTable / 2 : Balance.HeatCooldownPerTable);

            if (Stage == TableKind.Boss && !wasEnforcer) OfferBossReward();
            else OpenShop();
        }

        private void PayOut(TableKind kind)
        {
            // interest is on the gold you held when the table ended, before this payout
            bool noInterest = Gambler.Vices.Any(v => v.BlocksInterest);
            int perHand = Gambler.Vices.Select(v => v.GoldPerUnusedHand).DefaultIfEmpty(Balance.GoldPerUnusedHand).Max();
            bool redStake = Options.Stake >= Stake.Red && kind == TableKind.Low;
            LastPayout = new TablePayout(
                redStake ? 0 : Balance.TablePayout(kind),
                Table.HandsLeft * Math.Max(perHand, Balance.GoldPerUnusedHand),
                noInterest ? 0 : Balance.Interest(Gambler.Gold, ArchetypeBonuses.InterestCap(Gambler)));
            Gambler.GainGold(LastPayout.Total);
        }

        // two Vices, each Rare or (1 in 4) Legendary
        private void OfferBossReward()
        {
            _rewards.Clear();
            var taken = OwnedViceIds();
            for (int i = 0; i < Balance.BossRewardChoices; i++)
            {
                var rarity = Random.Next(4) == 0 ? Rarity.Legendary : Rarity.Rare;
                var vice = RollRewardVice(rarity, taken);
                if (vice == null) break;
                taken.Add(vice.Id);
                _rewards.Add(vice);
            }
            if (_rewards.Count == 0) OpenShop();
            else Phase = RunPhase.BossReward;
        }

        private ViceDefinition RollRewardVice(Rarity rarity, ICollection<string> taken)
        {
            var pool = ViceCatalog.OfRarity(rarity).Where(d => !taken.Contains(d.Id)).ToList();
            if (pool.Count > 0) return pool[Random.Next(pool.Count)];
            return Shop.RollVice(Random, taken, Rarity.Rare);
        }

        private void OpenShop()
        {
            Shop.Reset();
            Shop.Stock(Gambler, Random);
            Phase = RunPhase.Shop;
        }

        // after the shop: a room on the way to the next table, or straight on after a boss
        private void LeaveShop()
        {
            if (Stage == TableKind.Boss)
            {
                // the loan is due at the end of the floor you were given
                if (Gambler.Debt > 0 && Floor >= Gambler.DebtDueFloor)
                {
                    AtEnforcer = true;
                    StartTable();
                    return;
                }
                AdvanceStage();
                StartTable();
                return;
            }
            OfferRooms();
        }

        private void AdvanceStage()
        {
            if (Stage == TableKind.Boss)
            {
                Floor++;
                Stage = TableKind.Low;
                PickFloorBoss();
            }
            else Stage++;
        }

        // rooms

        private void OfferRooms()
        {
            _roomChoices.Clear();
            var pool = Rooms.All.ToList();
            // the lounge can't skip The House
            if (NextStage == (Balance.Floors, TableKind.Boss) && !BeatTheHouse) pool.Remove(RoomKind.HighLimitLounge);
            for (int i = 0; i < Balance.RoomChoices && pool.Count > 0; i++)
            {
                var room = pool[Random.Next(pool.Count)];
                pool.Remove(room);
                _roomChoices.Add(room);
            }
            Phase = RunPhase.RoomChoice;
        }

        private void EnterRoom(RoomKind kind)
        {
            Room = new Room(kind);
            switch (kind)
            {
                case RoomKind.Bar:
                    var cocktails = Cocktails.All.ToList();
                    for (int i = 0; i < Balance.BarCocktails; i++)
                    {
                        var pick = cocktails[Random.Next(cocktails.Count)];
                        cocktails.Remove(pick);
                        Room.Cocktails.Add(pick);
                        Room.CocktailSold.Add(false);
                    }
                    break;
                case RoomKind.PawnShop:
                    Room.UsedRare = Shop.RollVice(Random, OwnedViceIds(), Rarity.Rare);
                    break;
            }
            Phase = RunPhase.InRoom;
        }

        private void LeaveRoom()
        {
            if (_skipNextTable)
            {
                // the lounge: the next table never happens, and the one after it is harder
                _skipNextTable = false;
                _nextTargetFactor = Balance.LoungeTargetFactor;
                bool skippedBoss = NextStage.stage == TableKind.Boss;
                AdvanceStage();
                if (!skippedBoss)
                {
                    OfferRooms();
                    return;
                }
                // skipping the boss doesn't skip the loan shark
                if (Gambler.Debt > 0 && Floor >= Gambler.DebtDueFloor)
                {
                    AtEnforcer = true;
                    StartTable();
                    return;
                }
            }
            AdvanceStage();
            StartTable();
        }

        public List<RoomOption> RoomOptions()
        {
            var options = new List<RoomOption>();
            if (Room == null) return options;
            switch (Room.Kind)
            {
                case RoomKind.Bar:
                    for (int i = 0; i < Room.Cocktails.Count; i++)
                        options.Add(new RoomOption(RoomActionKind.BuyCocktail, Cocktails.Name(Room.Cocktails[i]),
                            Shop.HeatAdjusted(Balance.BarCocktailPrice, Gambler), i));
                    options.Add(new RoomOption(RoomActionKind.FreeCocktail, "Take a free random Cocktail"));
                    break;
                case RoomKind.BackRoom:
                    options.Add(new RoomOption(RoomActionKind.PlayBackRoom, $"Play for your {Gambler.Gold}g"));
                    break;
                case RoomKind.PawnShop:
                    for (int i = 0; i < Gambler.Vices.Count; i++)
                        options.Add(new RoomOption(RoomActionKind.SellViceFull, $"Sell {Gambler.Vices[i].Name}",
                            -PawnValue(Gambler.Vices[i]), i));
                    if (Room.UsedRare != null)
                        options.Add(new RoomOption(RoomActionKind.BuyUsedRare, $"Buy a used {Room.UsedRare.Name}",
                            Shop.HeatAdjusted(Balance.UsedRarePrice, Gambler)));
                    break;
                case RoomKind.LoanShark:
                    foreach (int amount in Balance.LoanAmounts)
                        options.Add(new RoomOption(RoomActionKind.Borrow, $"Borrow {amount}g", 0, amount));
                    break;
                case RoomKind.CardRoom:
                    options.Add(new RoomOption(RoomActionKind.RemoveCard, "Remove a card", needsCard: true));
                    options.Add(new RoomOption(RoomActionKind.DuplicateCard, "Duplicate a card", needsCard: true));
                    options.Add(new RoomOption(RoomActionKind.EnhanceCard, "Enhance a card", needsCard: true));
                    break;
                case RoomKind.LostAndFound:
                    options.Add(new RoomOption(RoomActionKind.TakeLostVice, "Take a Vice from the box"));
                    break;
                case RoomKind.SecurityOffice:
                    int steps = Math.Min((Gambler.Heat + 9) / 10, Gambler.Gold / Balance.HeatClearCostPer10);
                    if (Gambler.Heat > 0)
                        options.Add(new RoomOption(RoomActionKind.PayOffHeat, $"Clear {Math.Min(steps * 10, Gambler.Heat)} Heat",
                            steps * Balance.HeatClearCostPer10, steps));
                    options.Add(new RoomOption(RoomActionKind.Bribe, $"Bribe a guard (+{Balance.BribeHeat} Heat)"));
                    break;
                case RoomKind.HighLimitLounge:
                    var (floor, stage) = NextStage;
                    options.Add(new RoomOption(RoomActionKind.SkipNextTable, $"Skip the {StageLabel(stage, floor)}"));
                    break;
            }
            return options;
        }

        private static string StageLabel(TableKind stage, int floor) => stage switch
        {
            TableKind.Low => "Low Table",
            TableKind.High => "High Table",
            _ => floor == Balance.Floors ? "The House" : "Pit Boss",
        };

        public int PawnValue(Vice vice) => vice.Definition.IsCursed ? 1 : Balance.VicePrice(vice.Definition.Rarity);

        private bool CanTakeRoomOption(RunInput input)
        {
            if (InBackRoomHand) return false;
            var options = RoomOptions();
            if (!InRange(input.Index, options.Count)) return false;
            var option = options[input.Index];
            if (option.NeedsCard != InRange(input.Index2, Gambler.Deck.Count)) return false;
            if (option.Cost > 0 && !Gambler.CanAfford(option.Cost)) return false;

            switch (option.Kind)
            {
                case RoomActionKind.BuyCocktail:
                    return !Room.Used && !Room.CocktailSold[option.Arg] && Gambler.HasFreeCocktailSlot;
                case RoomActionKind.FreeCocktail:
                    return !Room.Used && !Room.CocktailSold.Any(s => s) && Gambler.HasFreeCocktailSlot;
                case RoomActionKind.PlayBackRoom:
                    return Room.BackRoomTable == null && Gambler.Gold > 0;
                case RoomActionKind.SellViceFull:
                    return true;
                case RoomActionKind.BuyUsedRare:
                    return !Room.Used && Gambler.HasFreeViceSlot;
                case RoomActionKind.Borrow:
                    return !Room.Used;
                case RoomActionKind.RemoveCard:
                    return !Room.Used && Gambler.Deck.Count > Balance.MinDeckSize;
                case RoomActionKind.DuplicateCard:
                case RoomActionKind.EnhanceCard:
                    return !Room.Used && !Gambler.Deck[input.Index2].IsCursed;
                case RoomActionKind.TakeLostVice:
                    return !Room.Used && Gambler.HasFreeViceSlot;
                case RoomActionKind.PayOffHeat:
                    return option.Arg > 0;
                case RoomActionKind.Bribe:
                    return !Room.Used && Gambler.HasFreeViceSlot && ViceCatalog.All.Any(d => d.IsCheater && !d.IsCursed && !OwnedViceIds().Contains(d.Id));
                case RoomActionKind.SkipNextTable:
                    return !Room.Used;
                default:
                    return false;
            }
        }

        private void TakeRoomOption(RoomOption option, int card)
        {
            if (option.Cost > 0) Gambler.LoseGold(option.Cost);
            switch (option.Kind)
            {
                case RoomActionKind.BuyCocktail:
                    Room.CocktailSold[option.Arg] = true;
                    Gambler.Cocktails.Add(new Cocktail(Room.Cocktails[option.Arg]));
                    break;
                case RoomActionKind.FreeCocktail:
                    var drink = Cocktails.All[Random.Next(Cocktails.All.Count)];
                    Gambler.Cocktails.Add(new Cocktail(drink));
                    Room.Used = true;
                    Room.Result = $"The bartender slides you a {Cocktails.Name(drink)}.";
                    break;
                case RoomActionKind.PlayBackRoom:
                    // one hand, no Vices, nothing to score: just win or lose
                    Room.BackRoomTable = new Table(Gambler, Random, long.MaxValue);
                    Room.BackRoomTable.AddHands(1 - Room.BackRoomTable.HandsLeft);
                    break;
                case RoomActionKind.SellViceFull:
                    var vice = Gambler.Vices[option.Arg];
                    Gambler.Vices.RemoveAt(option.Arg);
                    Gambler.GainGold(PawnValue(vice));
                    break;
                case RoomActionKind.BuyUsedRare:
                    Room.Used = true;
                    var bought = Random.Next(3) == 0 ? RandomCursedVice() : Room.UsedRare;
                    if (bought != null) Gambler.Vices.Add(bought.Create());
                    Room.Result = bought != null && bought.IsCursed
                        ? $"It was a fake. You got {bought.Name} instead."
                        : $"{Room.UsedRare.Name} is yours.";
                    break;
                case RoomActionKind.Borrow:
                    Room.Used = true;
                    Gambler.Borrow(option.Arg, Floor + 1);
                    Room.Result = $"You owe {Gambler.Debt}g by the end of floor {Gambler.DebtDueFloor}.";
                    break;
                case RoomActionKind.RemoveCard:
                    Room.Used = true;
                    Room.Result = $"{Gambler.Deck[card]} is gone.";
                    Gambler.Deck.RemoveAt(card);
                    break;
                case RoomActionKind.DuplicateCard:
                    Room.Used = true;
                    Gambler.Deck.Add(Gambler.Deck[card].Copy());
                    Room.Result = $"Another {Gambler.Deck[card]} joins your deck.";
                    break;
                case RoomActionKind.EnhanceCard:
                    Room.Used = true;
                    var options = new[] { Enhancement.Gilded, Enhancement.Flex, Enhancement.Fragile, Enhancement.Ghost, Enhancement.Hot, Enhancement.Heavy }
                        .Where(e => e != Gambler.Deck[card].Enhancement).ToList();
                    Gambler.Deck[card].Enhancement = options[Random.Next(options.Count)];
                    Room.Result = $"{Gambler.Deck[card]} is now {Gambler.Deck[card].Enhancement}.";
                    break;
                case RoomActionKind.TakeLostVice:
                    Room.Used = true;
                    var found = Random.Next(3) == 0 ? RandomCursedVice() : Shop.RollVice(Random, OwnedViceIds(), Balance.RollShopRarity(Random));
                    if (found != null) Gambler.Vices.Add(found.Create());
                    Room.Result = found == null ? "The box is empty." : found.IsCursed ? $"Uh oh. It's {found.Name}." : $"You found {found.Name}.";
                    break;
                case RoomActionKind.PayOffHeat:
                    Gambler.CoolDown(option.Arg * 10);
                    break;
                case RoomActionKind.Bribe:
                    Room.Used = true;
                    var pool = ViceCatalog.All.Where(d => d.IsCheater && !d.IsCursed && !OwnedViceIds().Contains(d.Id)).ToList();
                    var cheat = pool[Random.Next(pool.Count)];
                    Gambler.Vices.Add(cheat.Create());
                    Gambler.AddHeat(Balance.BribeHeat);
                    Room.Result = $"The guard hands you {cheat.Name} and winks.";
                    break;
                case RoomActionKind.SkipNextTable:
                    Room.Used = true;
                    _skipNextTable = true;
                    Room.Result = "The next table is off. The one after it will be watching you.";
                    break;
            }
        }

        // keeps the run's records as hands are scored
        private sealed class RunStats : TableEffect
        {
            private readonly Run _run;

            public RunStats(Run run) => _run = run;

            public override void OnDealEnd(Table table)
            {
                foreach (var score in table.LastScores)
                {
                    _run.BestHand = Math.Max(_run.BestHand, score.Total);
                    if (score.Outcome == Outcome.Blackjack) _run.BestBlackjackMult = Math.Max(_run.BestBlackjackMult, score.Mult);
                }
            }
        }

        private ViceDefinition RandomCursedVice()
        {
            var owned = OwnedViceIds();
            var pool = ViceCatalog.OfRarity(Rarity.Cursed).Where(d => !owned.Contains(d.Id)).ToList();
            return pool.Count > 0 ? pool[Random.Next(pool.Count)] : null;
        }

        // the Back Room hand ends: double up, lose it all, or walk away even
        private void SettleBackRoom()
        {
            var table = Room.BackRoomTable;
            if (table.LastScores.Count == 0) return;
            if (table.Phase != TablePhase.ReadyToDeal && !table.IsOver) return;
            Room.Used = true;
            var outcome = table.LastScores[0].Outcome;
            if (outcome.IsWin())
            {
                int gold = Gambler.Gold;
                Gambler.GainGold(gold);
                Room.Result = $"You won! {gold}g became {Gambler.Gold}g.";
            }
            else if (outcome == Outcome.Lose || outcome == Outcome.Bust)
            {
                int lost = Gambler.LoseGold(Gambler.Gold);
                Room.Result = $"The house takes all {lost}g.";
            }
            else Room.Result = outcome == Outcome.Surrender ? "You fold and walk away." : "A push. You walk away even.";
            if (outcome != Outcome.Surrender)
            {
                var dealer = table.DealerValue;
                Room.Result += $" You had {table.PlayerHands[0].Value.Total}, the dealer " +
                               (dealer.IsBust ? $"busted with {dealer.Total}." : $"had {dealer.Total}.");
            }
        }
    }
}
