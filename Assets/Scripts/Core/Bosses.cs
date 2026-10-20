using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    public sealed class BossDefinition
    {
        public string Name { get; }
        public string Twist { get; }
        private readonly Func<Boss> _create;

        public BossDefinition(string name, string twist, Func<Boss> create)
        {
            Name = name;
            Twist = twist;
            _create = create;
        }

        public Boss Create()
        {
            var boss = _create();
            boss.Definition = this;
            return boss;
        }

        public override string ToString() => Name;
    }

    // a Pit Boss table's twist. it runs before the cards and Vices
    public abstract class Boss : TableEffect
    {
        public BossDefinition Definition { get; internal set; }
        public string Name => Definition.Name;
        public virtual long TargetFactor => 1;
    }

    public static class Bosses
    {
        // Pit Bosses for floors 1-7, picked at random
        public static readonly IReadOnlyList<BossDefinition> PitBosses = new List<BossDefinition>
        {
            new BossDefinition("The Twins", "The dealer plays two hands. You have to beat both to win.", () => new TheTwins()),
            new BossDefinition("The Iceman", "The dealer stands on 15.", () => new TheIceman()),
            new BossDefinition("The Cheat", "The dealer wins every push.", () => new TheCheat()),
            new BossDefinition("The Mime", "Your second card stays face down until you stand.", () => new TheMime()),
            new BossDefinition("The Taxman", "Every hit costs 1 gold.", () => new TheTaxman()),
            new BossDefinition("The Purist", "Aces only count as 1.", () => new ThePurist()),
            new BossDefinition("The Ceiling", "Your bust limit drops to 19.", () => new TheCeiling()),
            new BossDefinition("The Sweeper", "Burns are disabled.", () => new TheSweeper()),
            new BossDefinition("The Silencer", "Your leftmost Vice is disabled.", () => new TheSilencer()),
            new BossDefinition("The Card Mechanic", "After you stand, he swaps your highest card with his face-up card.", () => new TheCardMechanic()),
            new BossDefinition("The Hoarder", "Shuffles 3 Dead Cards into your deck for the rest of the run.", () => new TheHoarder()),
            new BossDefinition("The Pit Boss", "Every hand gives +10 Heat.", () => new ThePitBoss()),
        };

        public static readonly BossDefinition EyeInTheSky = new BossDefinition("The Eye in the Sky",
            "Security caught you. All Cheater Vices are disabled and the target is 2x.", () => new TheEyeInTheSky());

        public static readonly BossDefinition Enforcer = new BossDefinition("The Enforcer",
            "You skipped a loan. Takes 3 gold for every hand you don't win. If you hit 0 gold, he takes a Vice.", () => new TheEnforcer());

        public static readonly BossDefinition TheHouse = new BossDefinition("The House",
            "Three phases. The twist changes every 2 hands, pulling from every Pit Boss.", () => new TheHouse());

        public static BossDefinition Get(string name) =>
            PitBosses.Concat(new[] { EyeInTheSky, Enforcer, TheHouse }).First(b => b.Name == name);
    }

    public sealed class TheTwins : Boss
    {
        public override void OnTableStart(Table table) => table.Rules.DealerPlaysTwoHands = true;
    }

    public sealed class TheIceman : Boss
    {
        public override void OnTableStart(Table table) => table.Rules.DealerStandsOn = 15;
    }

    public sealed class TheCheat : Boss
    {
        public override void OnTableStart(Table table) => table.Rules.Pushes = PushRule.DealerWins;
    }

    public sealed class TheMime : Boss
    {
        public override void OnTableStart(Table table) => table.Rules.SecondCardFaceDown = true;
    }

    public sealed class TheTaxman : Boss
    {
        public override void OnTableStart(Table table) => table.Rules.HitCost = Balance.TaxmanHitCost;
    }

    public sealed class ThePurist : Boss
    {
        public override void OnTableStart(Table table) => table.Rules.AcesAlwaysOne = true;
    }

    public sealed class TheCeiling : Boss
    {
        public override void OnTableStart(Table table) => table.Rules.PlayerBustLimit = 19;
    }

    public sealed class TheSweeper : Boss
    {
        public override void OnTableStart(Table table) => table.Rules.BurnsDisabled = true;
    }

    public sealed class TheSilencer : Boss
    {
        public override void OnTableStart(Table table)
        {
            var leftmost = table.Gambler.Vices.FirstOrDefault();
            if (leftmost != null) leftmost.Disabled = true;
        }
    }

    public sealed class TheCardMechanic : Boss
    {
        public override void OnStand(Table table, PlayerHand hand)
        {
            int highest = 0;
            for (int i = 1; i < hand.Cards.Count; i++)
                if (hand.Cards[i].Value > hand.Cards[highest].Value) highest = i;
            table.SwapWithDealerUpCard(hand, highest);
        }
    }

    public sealed class TheHoarder : Boss
    {
        public override void OnTableStart(Table table)
        {
            for (int i = 0; i < 3; i++) table.ShuffleIntoShoe(Card.Cursed(Curse.DeadCard), addToDeck: true);
        }
    }

    public sealed class ThePitBoss : Boss
    {
        public override void OnDeal(Table table) => table.AddHeat(Balance.PitBossHeatPerHand);
    }

    public sealed class TheEyeInTheSky : Boss
    {
        public override long TargetFactor => Balance.EyeInTheSkyTargetFactor;

        public override void OnTableStart(Table table)
        {
            foreach (var vice in table.Gambler.Vices)
                if (vice.Definition.IsCheater) vice.Disabled = true;
        }
    }

    public sealed class TheEnforcer : Boss
    {
        public override void OnDealEnd(Table table)
        {
            foreach (var score in table.LastScores)
            {
                if (score.Outcome.IsWin()) continue;
                if (table.Gambler.Gold > 0) table.Gambler.LoseGold(Balance.EnforcerTake);
                else table.TakeRandomVice();
            }
        }
    }

    // swaps in a new Pit Boss twist every 2 hands. only rule twists and hook twists that work mid-table
    public sealed class TheHouse : Boss
    {
        private Boss _twist;
        private TableRules _baseline;

        public Boss CurrentTwist => _twist;

        public override void OnTableStart(Table table) => table.AddHands(Balance.TheHouseExtraHands);

        public override void OnBeforeDeal(Table table)
        {
            if (_baseline == null) _baseline = table.Rules.Clone();
            if (table.DealsPlayed % Balance.TheHouseTwistEvery != 0) return;

            // the Hoarder and the Silencer change things for good, so they don't rotate in
            var pool = Bosses.PitBosses.Where(b => b.Name != "The Hoarder" && b.Name != "The Silencer").ToList();
            _twist = pool[table.Random.Next(pool.Count)].Create();
            table.Rules.CopyFrom(_baseline);
            _twist.OnTableStart(table);
        }

        public override void OnDeal(Table table) => _twist?.OnDeal(table);

        public override void OnStand(Table table, PlayerHand hand) => _twist?.OnStand(table, hand);
    }
}
