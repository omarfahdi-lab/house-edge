using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    public enum TableKind { Low, High, Boss }

    // one finished table, for the score graph at the end of a run
    public readonly struct TableRecord
    {
        public readonly int Floor;
        public readonly TableKind Stage;
        public readonly bool Elite, Won;
        public readonly long Score, Target;

        public TableRecord(int floor, TableKind stage, bool elite, bool won, long score, long target)
        {
            Floor = floor;
            Stage = stage;
            Elite = elite;
            Won = won;
            Score = score;
            Target = target;
        }
    }

    public enum RunPhase
    {
        // playing blackjack at Run.Table
        AtTable,
        // a Pit Boss fell: pick a Rare or Legendary Vice, or skip
        BossReward,
        // buy, sell and reorder, then leave
        Shop,
        // pick the next stop on the floor map
        RoomChoice,
        // inside Run.Room
        InRoom,
        Won,
        Lost,
        // a card pack is open: keep one of Run.PackCards, or none
        OpeningPack,
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

    // a full climb. each floor: Low Table, two stops on the map, High Table, two stops, Pit Boss, then a shop.
    // the stops are Shops and rooms on branching paths. one seed drives all of it
    public sealed class Run
    {
        private readonly List<RunInput> _inputs = new List<RunInput>();
        private readonly List<string> _usedBosses = new List<string>();
        private readonly List<ViceDefinition> _rewards = new List<ViceDefinition>();
        // where you are on the floor map: the section after the Low (0) or High (1) Table, the row (-1 before the
        // first stop) and the lane
        private int _section, _row = -1, _lane;
        private bool _skipNextTable;
        private double _nextTargetFactor = 1;

        public int Seed { get; }
        public RunOptions Options { get; }
        public GameRandom Random { get; }
        public Gambler Gambler { get; }

        // records for the run-over screen and the profile
        public long TotalScore { get; private set; }
        public long BestHand { get; private set; }
        public IReadOnlyList<TableRecord> History => _history;
        private readonly List<TableRecord> _history = new List<TableRecord>();
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
        // playing an optional Elite table on the map
        public bool AtElite { get; private set; }
        private bool _eliteReward;
        private BossDefinition _eliteBoss;

        // the cards in the pack being opened
        public IReadOnlyList<Card> PackCards => _packCards;
        public PackKind OpenPack { get; private set; }
        private readonly List<Card> _packCards = new List<Card>();

        // what this run saw and did, for the collection
        public HashSet<string> SeenVices { get; } = new HashSet<string>();
        public HashSet<string> BossesFaced { get; } = new HashSet<string>();
        public int[] CombosWon { get; } = new int[Combos.All.Count];
        public int[] FeatsHit { get; } = new int[Feats.All.Count];
        public RunPhase Phase { get; private set; }
        public int TablesBeaten { get; private set; }
        public Shop Shop { get; } = new Shop();
        public TablePayout LastPayout { get; private set; }
        public IReadOnlyList<ViceDefinition> Rewards => _rewards;
        public FloorMap Map { get; private set; }
        // the stops you can walk to next, and what they are
        public IReadOnlyList<PathStop> NextStops => Phase == RunPhase.RoomChoice ? Map.NextFrom(_section, _row, _lane) : new List<PathStop>();
        public IReadOnlyList<RoomKind> RoomChoices => NextStops.Select(s => s.Kind).ToList();
        // the stop you're at, if any
        public PathStop Here => _row < 0 ? null : Map.Stop(_section, _row, _lane);
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
            ApplyModifier();
            // everyone starts with one trick card, picked from the seed so the RNG stream is untouched
            Gambler.Tricks.Add(Tricks.All[(int)((uint)seed % (uint)Tricks.All.Count)]);
            if (Options.Deck == StartingDeck.MarkedDeck) Gambler.AddHeat(Balance.MarkedDeckHeat);
            if (Options.Stake >= Stake.Black)
                for (int i = 0; i < 2; i++) Gambler.Deck.Add(Card.Cursed(Curse.DeadCard));
            PickFloorBoss();
            BuildMap();
            StartTable();
        }

        private void ApplyModifier()
        {
            switch (Options.Modifier)
            {
                case Modifier.WildRoyals:
                    foreach (var card in Gambler.Deck)
                        if (card.IsFace) card.Enhancement = Enhancement.Wild;
                    break;
                case Modifier.ShortShoe:
                    Gambler.Deck.RemoveAll(c => !c.IsCursed && c.Rank >= Rank.Two && c.Rank <= Rank.Five);
                    break;
                case Modifier.HotNight:
                    Gambler.HeatMultiplier = 2;
                    break;
            }
        }

        private void BuildMap() => Map = new FloorMap(Floor, Random, loungeBeforeBoss: !(Floor == Balance.Floors && !BeatTheHouse));

        public static Run Replay(int seed, IEnumerable<RunInput> inputs, RunOptions options = null)
        {
            var run = new Run(seed, options);
            foreach (var input in inputs) run.Apply(input);
            return run;
        }

        public long TargetFor(TableKind stage) =>
            (long)Math.Round(Balance.Target(Floor, stage) * Stakes.TargetFactor(Options.Stake) * Modifiers.TargetFactor(Options.Modifier));
        public long CurrentTarget => Table?.Target ?? TargetFor(Stage);

        public int PriceOf(ShopOffer offer) => Priced(offer.BasePrice);
        public int RerollCost => Priced(Shop.RerollCost);

        // anything you pay for in a shop or room: Purple stake and up add 1, Suspected Heat adds 25%
        private int Priced(int basePrice) => Shop.HeatAdjusted(basePrice + Stakes.PriceBump(Options.Stake), Gambler);
        public int SellValue(Vice vice) => Balance.SellPrice(Balance.VicePrice(vice.Definition.Rarity));

        // the stage after the current one, and whether it starts a new floor
        public (int floor, TableKind stage) NextStage => Stage == TableKind.Boss ? (Floor + 1, TableKind.Low) : (Floor, Stage + 1);

        public bool IsLegal(RunInput input)
        {
            if (Phase == RunPhase.OpeningPack)
                return input.Kind == RunInputKind.SkipPack || input.Kind == RunInputKind.PickPackCard && InRange(input.Index, _packCards.Count);
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
                    return Phase == RunPhase.RoomChoice && InRange(input.Index, NextStops.Count);
                case RunInputKind.RoomOption:
                    return Phase == RunPhase.InRoom && CanTakeRoomOption(input);
                case RunInputKind.LeaveRoom:
                    return Phase == RunPhase.InRoom && !InBackRoomHand;
                case RunInputKind.PlayTrick:
                    return Phase == RunPhase.AtTable && !Table.IsOver && InRange(input.Index, Gambler.Tricks.Count)
                        && Table.CanUseTrick(Gambler.Tricks[input.Index], input.Index2);
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
                OfferKind.Trick => Gambler.HasFreeTrickSlot,
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
                    Shop.Stock(Gambler, Random, Options.LockedVices);
                    foreach (var offer in Shop.Offers.Where(o => o.Kind == OfferKind.Vice)) SeenVices.Add(offer.Vice.Id);
                    break;
                case RunInputKind.LeaveShop:
                    LeaveShop();
                    break;
                case RunInputKind.UseCocktail:
                    Drink(input);
                    break;
                case RunInputKind.PickReward:
                    Gambler.Vices.Add(_rewards[input.Index].Create());
                    AfterReward();
                    break;
                case RunInputKind.SkipReward:
                    AfterReward();
                    break;
                case RunInputKind.ChooseRoom:
                    WalkTo(NextStops[input.Index]);
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
                case RunInputKind.PlayTrick:
                    var trick = Gambler.Tricks[input.Index];
                    Gambler.Tricks.RemoveAt(input.Index);
                    Table.UseTrick(trick, input.Index2);
                    if (Table.IsOver) FinishTable();
                    break;
                case RunInputKind.PickPackCard:
                    Gambler.Deck.Add(_packCards[input.Index]);
                    _packCards.Clear();
                    Phase = RunPhase.Shop;
                    break;
                case RunInputKind.SkipPack:
                    _packCards.Clear();
                    Phase = RunPhase.Shop;
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
                case OfferKind.Trick: Gambler.Tricks.Add(offer.Trick); break;
                case OfferKind.Pack:
                    _packCards.Clear();
                    _packCards.AddRange(Packs.Open(offer.Pack, Random));
                    OpenPack = offer.Pack;
                    Phase = RunPhase.OpeningPack;
                    break;
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

        // Vices that can't be rolled: the ones you hold and the ones you haven't unlocked
        private List<string> OwnedViceIds() => Gambler.Vices.Select(v => v.Definition.Id).Concat(Options.LockedVices).ToList();

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
                : AtElite ? _eliteBoss.Create()
                : Stage == TableKind.Boss ? FloorBoss.Create()
                : null;
            if (Boss != null) BossesFaced.Add(Boss.Name);
            // the tutorial table gets a dealer with no rule changes
            bool tutorial = IsTutorialTable;
            Dealer = Boss == null ? tutorial ? new ChattyChen() : Dealers.Random(Random) : null;
            if (Boss != null) effects.Add(Boss);
            if (Dealer != null) effects.Add(Dealer);
            effects.Add(new RunRules(Options));
            if (Options.Modifier != Modifier.None) effects.Add(new ModifierRules(Options.Modifier));
            effects.AddRange(Gambler.Vices);
            effects.Add(new RunStats(this));

            // an Elite plays for the next table's target
            var stage = AtElite ? NextStage.stage : Stage;
            double bossFactor = Boss != null && (AtElite || Stage == TableKind.Boss) ? Stakes.BossFactor(Options.Stake) : 1;
            long target = (long)Math.Round(TargetFor(stage) * (Boss?.TargetFactor ?? 1) * bossFactor * (AtElite ? Balance.EliteTargetFactor : _nextTargetFactor));
            if (!AtElite) _nextTargetFactor = 1;
            Table = tutorial
                ? new Table(Gambler, Core.Tutorial.Shoe(Gambler.Deck, Random), Random, target, effects)
                : new Table(Gambler, Random, target, effects);
            if (AtElite) Table.AddHands(-Balance.EliteHandsLess);
            Phase = RunPhase.AtTable;
        }

        // the first table of a tutorial run, before it has been beaten
        public bool IsTutorialTable => Options.Tutorial && Floor == 1 && Stage == TableKind.Low && TablesBeaten == 0
                                       && !AtElite && !AtEyeInTheSky && !AtEnforcer;

        private void FinishTable()
        {
            if (AtElite)
            {
                FinishElite();
                return;
            }
            TotalScore += Table.Score;
            Record(false);
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
            else if (Stage == TableKind.Boss) OpenShop();
            else StartPath();
        }

        private void PayOut(TableKind kind)
        {
            // interest is on the gold you held when the table ended, before this payout
            bool noInterest = Gambler.Vices.Any(v => v.BlocksInterest);
            int perHand = Gambler.Vices.Select(v => v.GoldPerUnusedHand).DefaultIfEmpty(Balance.GoldPerUnusedHand).Max();
            bool redStake = Options.Stake >= Stake.Red && kind == TableKind.Low;
            LastPayout = new TablePayout(
                redStake ? 0 : Balance.TablePayout(kind) * Modifiers.PayoutFactor(Options.Modifier),
                Table.HandsLeft * Math.Max(perHand, Balance.GoldPerUnusedHand),
                noInterest ? 0 : Balance.Interest(Gambler.Gold, Options.Stake >= Stake.Platinum
                    ? Math.Min(Stakes.PlatinumInterestCap, ArchetypeBonuses.InterestCap(Gambler))
                    : ArchetypeBonuses.InterestCap(Gambler)));
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
            foreach (var reward in _rewards) SeenVices.Add(reward.Id);
            if (_rewards.Count == 0) AfterReward();
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
            Shop.Stock(Gambler, Random, Options.LockedVices);
            foreach (var offer in Shop.Offers.Where(o => o.Kind == OfferKind.Vice)) SeenVices.Add(offer.Vice.Id);
            Phase = RunPhase.Shop;
        }

        // a Pit Boss's reward leads to the shop; an Elite's back onto the map
        private void AfterReward()
        {
            if (_eliteReward)
            {
                _eliteReward = false;
                LeaveStop();
            }
            else OpenShop();
        }

        // an optional table under a random Pit Boss's rule (never this floor's own boss)
        private void StartElite()
        {
            var pool = Bosses.PitBosses.Where(b => b != FloorBoss).ToList();
            _eliteBoss = pool[Random.Next(pool.Count)];
            AtElite = true;
            StartTable();
        }

        private void Record(bool elite) =>
            _history.Add(new TableRecord(Floor, Stage, elite, Table.Phase != TablePhase.Lost, Table.Score, Table.Target));

        // win: paid like a High Table, then pick a Rare Vice. lose: the run goes on, but half your gold is gone
        private void FinishElite()
        {
            AtElite = false;
            TotalScore += Table.Score;
            Record(true);
            LastFreeVice = null;
            if (Table.Phase == TablePhase.Lost)
            {
                EliteResult = $"You lost the Elite table and {Gambler.Gold / 2} gold.";
                Gambler.LoseGold(Gambler.Gold / 2);
                Gambler.GrowDebt();
                LeaveStop();
                return;
            }
            TablesBeaten++;
            EliteResult = null;
            PayOut(TableKind.High);
            // a win also teaches you a trick
            if (Gambler.HasFreeTrickSlot) Gambler.Tricks.Add(Tricks.All[Random.Next(Tricks.All.Count)]);
            // a table like any other: the loan grows and Heat cools (half as fast on Purple stake)
            Gambler.GrowDebt();
            Gambler.CoolDown(Options.Stake >= Stake.Purple ? Balance.HeatCooldownPerTable / 2 : Balance.HeatCooldownPerTable);
            _eliteReward = true;
            OfferBossReward();
        }

        // what happened at the last Elite table you lost, for the map to say
        public string EliteResult { get; private set; }

        // after a Low or High Table: onto the map, choosing the first stop
        private void StartPath()
        {
            _section = Stage == TableKind.Low ? 0 : 1;
            _row = -1;
            _lane = 0;
            Phase = RunPhase.RoomChoice;
        }

        private void WalkTo(PathStop stop)
        {
            _row = stop.Row;
            _lane = stop.Lane;
            stop.Visited = true;
            if (stop.Kind == RoomKind.Shop) OpenShop();
            else if (stop.Kind == RoomKind.Elite) StartElite();
            else EnterRoom(stop.Kind);
        }

        // done with a stop: on to the next one, or to the table at the end of the path
        private void LeaveStop()
        {
            if (_row < FloorMap.Rows - 1)
            {
                Phase = RunPhase.RoomChoice;
                return;
            }
            _row = -1;
            AdvanceStage();
            StartTable();
        }

        // after a shop: the next stop on the map, or the next floor after a boss
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
            LeaveStop();
        }

        private void AdvanceStage()
        {
            if (Stage == TableKind.Boss)
            {
                Floor++;
                Stage = TableKind.Low;
                PickFloorBoss();
                BuildMap();
            }
            else Stage++;
        }

        // rooms

        internal void EnterRoom(RoomKind kind)
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
                case RoomKind.Mystery:
                    Room.Event = Events.All[Random.Next(Events.All.Count)];
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
                _row = -1;
                AdvanceStage();
                if (!skippedBoss)
                {
                    // straight on to the map after the skipped table
                    StartPath();
                    return;
                }
                // skipping the boss doesn't skip the loan shark
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
            LeaveStop();
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
                            Priced(Balance.BarCocktailPrice), i));
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
                            Priced(Balance.UsedRarePrice)));
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
                case RoomKind.Mystery:
                    var choices = Events.Choices(Room.Event);
                    for (int i = 0; i < choices.Count; i++)
                        options.Add(new RoomOption(RoomActionKind.EventChoice, choices[i].label,
                            choices[i].cost > 0 ? Priced(choices[i].cost) : choices[i].cost, i));
                    break;
                case RoomKind.Slots:
                    options.Add(new RoomOption(RoomActionKind.PullSlots, "Pull the lever", Priced(Balance.SlotPullCost)));
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
                case RoomActionKind.EventChoice:
                    return !Room.Used && EventChoiceOpen(Room.Event, option.Arg);
                case RoomActionKind.PullSlots:
                    return Room.Pulls < Balance.SlotPulls;
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
                case RoomActionKind.EventChoice:
                    Room.Used = true;
                    if (option.Cost < 0) Gambler.GainGold(-option.Cost);
                    Room.Result = PlayEvent(Room.Event, option.Arg);
                    break;
                case RoomActionKind.PullSlots:
                    Room.Result = PullSlots();
                    break;
                case RoomActionKind.SkipNextTable:
                    Room.Used = true;
                    _skipNextTable = true;
                    Room.Result = "The next table is off. The one after it will be watching you.";
                    break;
            }
        }

        // one pull of the slot machine: sets the reels and pays out. returns what to tell the player
        private string PullSlots()
        {
            Room.Pulls++;
            int roll = Random.Next(1000);
            if (roll < 5)
            {
                Room.Reels = Three(SlotSymbol.Seven);
                Gambler.GainGold(Balance.SlotJackpot);
                return $"JACKPOT! 7-7-7 pays {Balance.SlotJackpot} gold!";
            }
            if (roll < 40)
            {
                Room.Reels = Three(SlotSymbol.Bar);
                var vice = Gambler.HasFreeViceSlot ? Shop.RollVice(Random, OwnedViceIds(), Rarity.Rare) : null;
                if (vice == null)
                {
                    Gambler.GainGold(15);
                    return "Bar-Bar-Bar! No room for a Vice, so it pays 15 gold.";
                }
                Gambler.Vices.Add(vice.Create());
                SeenVices.Add(vice.Id);
                return $"Bar-Bar-Bar! Out drops {vice.Name}.";
            }
            if (roll < 120)
            {
                Room.Reels = Three(SlotSymbol.Cherry);
                Gambler.GainGold(12);
                return "Three cherries: 12 gold.";
            }
            if (roll < 260)
            {
                Room.Reels = Three(SlotSymbol.Bell);
                if (!Gambler.HasFreeTrickSlot)
                {
                    Gambler.GainGold(6);
                    return "Three bells! Your hands are full of tricks, so it pays 6 gold.";
                }
                var trick = Tricks.All[Random.Next(Tricks.All.Count)];
                Gambler.Tricks.Add(trick);
                return $"Three bells! You learn a trick: {Tricks.Name(trick)}.";
            }
            if (roll < 450)
            {
                var odd = (SlotSymbol)(1 + Random.Next(4));
                Room.Reels = new[] { SlotSymbol.Cherry, SlotSymbol.Cherry, SlotSymbol.Cherry };
                Room.Reels[Random.Next(3)] = odd;
                Gambler.GainGold(5);
                return "Two cherries: 5 gold.";
            }
            // a miss: no three of a kind and at most one cherry
            var reels = new SlotSymbol[3];
            do
            {
                for (int i = 0; i < 3; i++) reels[i] = (SlotSymbol)Random.Next(5);
            } while (reels[0] == reels[1] && reels[1] == reels[2] || reels.Count(r => r == SlotSymbol.Cherry) >= 2);
            Room.Reels = reels;
            return "Nothing. The machine hums smugly.";
        }

        private static SlotSymbol[] Three(SlotSymbol symbol) => new[] { symbol, symbol, symbol };

        // some choices need room for what they give
        private bool EventChoiceOpen(EventKind kind, int choice) => (kind, choice) switch
        {
            (EventKind.LuckyChip, 0) => Gambler.HasFreeViceSlot,
            (EventKind.BackAlley, 0) => Gambler.HasFreeViceSlot,
            (EventKind.SpilledTray, 0) => Gambler.HasFreeCocktailSlot,
            (EventKind.Magician, 0) => Gambler.HasFreeTrickSlot,
            (EventKind.Shredder, 0) => Gambler.Deck.Count - 3 >= Balance.MinDeckSize,
            _ => true,
        };

        // what a "?" choice does. returns what to tell the player
        private string PlayEvent(EventKind kind, int choice)
        {
            switch (kind, choice)
            {
                case (EventKind.LuckyChip, 0):
                    var lucky = ViceCatalog.All.Where(d => d.Archetype == Archetype.Lucky && !d.IsCursed && d.Rarity <= Rarity.Rare
                                                           && !OwnedViceIds().Contains(d.Id)).ToList();
                    Gambler.AddHeat(20);
                    if (lucky.Count == 0) return "The chip was a fake. Security noticed you anyway.";
                    var chip = lucky[Random.Next(lucky.Count)];
                    Gambler.Vices.Add(chip.Create());
                    SeenVices.Add(chip.Id);
                    return $"It's {chip.Name}. Someone saw you take it.";
                case (EventKind.LuckyChip, _):
                    return "The pit boss thanks you with 8 gold.";
                case (EventKind.Hustler, _):
                    if (Random.Next(2) == 0)
                    {
                        Gambler.GainGold(10);
                        return "Heads. He grumbles and pays you 10 gold.";
                    }
                    return "Tails. He pockets your 5 gold and vanishes.";
                case (EventKind.SpilledTray, 0):
                    var drink = Cocktails.All[Random.Next(Cocktails.All.Count)];
                    Gambler.Cocktails.Add(new Cocktail(drink));
                    return $"A {Cocktails.Name(drink)} survived the fall. It's yours.";
                case (EventKind.SpilledTray, _):
                    return "He thanks you with a 3 gold tip.";
                case (EventKind.BackAlley, _):
                    var bought = Shop.RollVice(Random, OwnedViceIds(), Rarity.Rare);
                    Gambler.Deck.Add(Card.Cursed(Curse.DeadCard));
                    if (bought == null) return "His coat was empty. You got a Dead Card for your trouble.";
                    Gambler.Vices.Add(bought.Create());
                    SeenVices.Add(bought.Id);
                    return $"You got {bought.Name}. And a Dead Card slipped into your deck.";
                case (EventKind.Shredder, _):
                    var lowest = Gambler.Deck.Where(c => !c.IsCursed).OrderBy(c => c.Value).Take(3).ToList();
                    foreach (var card in lowest) Gambler.Deck.Remove(card);
                    return $"Gone: {string.Join(", ", lowest.Select(c => c.RankLabel))}.";
                case (EventKind.PhotoBooth, _):
                    var best = Gambler.Deck.Where(c => !c.IsCursed).OrderByDescending(c => c.Value)
                        .ThenByDescending(c => c.Enhancement != Enhancement.None).FirstOrDefault();
                    if (best == null) return "The booth is out of film.";
                    Gambler.Deck.Add(best.Copy());
                    return $"A perfect copy of your {best.RankLabel} joins your deck.";
                case (EventKind.SecuritySweep, 0):
                    Gambler.CoolDown(25);
                    return "You keep your head down. Heat -25.";
                case (EventKind.SecuritySweep, _):
                    if (Random.Next(2) == 0)
                    {
                        Gambler.CoolDown(50);
                        return "They wave you through. Heat -50.";
                    }
                    Gambler.AddHeat(25);
                    return "They take a long look at you. Heat +25.";
                case (EventKind.Magician, 0):
                    var learned = Tricks.All[Random.Next(Tricks.All.Count)];
                    Gambler.Tricks.Add(learned);
                    return $"\"Watch closely.\" You learn {Tricks.Name(learned)}.";
                case (EventKind.Magician, _):
                    return "You tip him 2 gold. He pulls a coin from your ear and keeps it.";
                case (EventKind.CompTicket, 0):
                    var combo = Combos.All[Random.Next(Combos.All.Count)];
                    Gambler.LevelUp(Comps.For(combo));
                    return $"Your {Combos.Name(combo)} Comp goes up a level.";
                default:
                    return "You sell the ticket for 6 gold.";
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
                    foreach (var combo in score.Combos) _run.CombosWon[(int)combo]++;
                    foreach (var feat in score.Feats) _run.FeatsHit[(int)feat]++;
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
