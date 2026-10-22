using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    // who a boss is: their name and title, their one rule (named and in plain words), a line they say
    // when you sit down, and a tip for beating them
    public sealed class BossDefinition
    {
        public string Name { get; }
        public string Title { get; }
        public string Ability { get; }
        public string Twist { get; }
        public string Quote { get; }
        public string Tip { get; }
        private readonly Func<Boss> _create;

        public BossDefinition(string name, string title, string ability, string twist, string quote, string tip, Func<Boss> create)
        {
            Name = name;
            Title = title;
            Ability = ability;
            Twist = twist;
            Quote = quote;
            Tip = tip;
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
        public virtual double TargetFactor => 1;
    }

    public static class Bosses
    {
        // Pit Bosses for floors 1-7, picked at random
        public static readonly IReadOnlyList<BossDefinition> PitBosses = new List<BossDefinition>
        {
            new BossDefinition("The Twins", "Double Trouble", "Two Dealers",
                "The dealer plays two hands. You have to beat both to win.",
                "One of us always wins. Usually both.",
                "Stand on lower totals: two dealer hands bust more often than one.", () => new TheTwins()),
            new BossDefinition("The Iceman", "Cold Blooded", "Cold Stop",
                "The dealer stops drawing at 15 instead of 17.",
                "I don't chase. I just wait for you to break.",
                "He often sits on 15 or 16, so any 17 or better beats him. Don't hit into a bust.", () => new TheIceman()),
            new BossDefinition("The Cheat", "Fast Hands", "Sleight of Hand",
                "Once a hand, when a card would bust the dealer, he palms it and draws again.",
                "Bust? Me? Must have been a misdeal.",
                "His first bust doesn't count, so don't stand on a weak hand hoping he'll bust.", () => new TheCheat()),
            new BossDefinition("The Mime", "The Silent Act", "Blindfold",
                "Your second card stays face down until your hand is over.",
                "...",
                "Count your hand from your first card and the cards you hit. Play it safe.", () => new TheMime()),
            new BossDefinition("The Taxman", "Collector of Debts", "Toll Booth",
                "Every Hit costs you 1 gold.",
                "Everything has a price, friend. Especially another card.",
                "Bring gold, and stand a little earlier than usual.", () => new TheTaxman()),
            new BossDefinition("The Purist", "Old Money", "No Soft Hands",
                "Aces only count as 1, never 11. No blackjacks.",
                "Eleven? How vulgar.",
                "Aces are just 1s here, so 10s and face cards matter more.", () => new ThePurist()),
            new BossDefinition("The Ceiling", "The Low Roof", "Low Ceiling",
                "You bust over 19 instead of 21.",
                "Mind your head. The roof is lower than it looks.",
                "Stand on 16 or more. A 19 is a perfect hand here.", () => new TheCeiling()),
            new BossDefinition("The Sweeper", "Tidy and Thorough", "Clean Sweep",
                "You can't Burn at this table.",
                "No second chances on my floor.",
                "Every Hit is final, so Hit less.", () => new TheSweeper()),
            new BossDefinition("The Silencer", "Keeper of Quiet", "Hush",
                "Your leftmost Vice is switched off for this table.",
                "Shh. Your favourite trick stays in its box.",
                "Move a Vice you can live without to the far left before this table.", () => new TheSilencer()),
            new BossDefinition("The Card Mechanic", "The Swapper", "Switcheroo",
                "When you Stand, he swaps your highest card with his face-up card.",
                "I'll take that. You can have this one.",
                "Hands made of middle cards lose the least when he swaps.", () => new TheCardMechanic()),
            new BossDefinition("The Hoarder", "Keeper of Junk", "Junk Drawer",
                "Shuffles 3 Dead Cards into your deck, and they stay for the rest of the run.",
                "Here. I've been saving these for you.",
                "Remove them later at a Card Room, or with a Screwdriver.", () => new TheHoarder()),
            new BossDefinition("The Pit Boss", "The Man Upstairs", "Under Watch",
                "Every hand you play adds 10 Heat.",
                "I've got my eye on you.",
                "Keep your Heat low going in, or security will come for you after.", () => new ThePitBoss()),
        };

        public static readonly BossDefinition EyeInTheSky = new BossDefinition("The Eye in the Sky", "Casino Security", "Caught",
            "Security caught you. Your Cheater Vices are off and the target is doubled.",
            "We saw everything.",
            "Win clean: this table is all about your honest Vices.", () => new TheEyeInTheSky());

        public static readonly BossDefinition Enforcer = new BossDefinition("The Enforcer", "The Loan Shark's Muscle", "Collections",
            "You skipped a loan. He takes 3 gold for every hand you don't win. If you're broke, he takes a Vice.",
            "The Shark sends his regards.",
            "Win hands, or keep some gold in your pocket.", () => new TheEnforcer());

        public static readonly BossDefinition TheHouse = new BossDefinition("The House", "Owner of Everything", "The House Always Wins",
            "Three phases. Every 2 hands The House takes on another Pit Boss's rule.",
            "You've come a long way. It ends here.",
            "Watch which rule is active and adjust every 2 hands. You get 2 extra Hands.", () => new TheHouse());

        public static BossDefinition Get(string name) =>
            PitBosses.Concat(new[] { EyeInTheSky, Enforcer, TheHouse }).First(b => b.Name == name);
    }

    // each boss's target is scaled so they're all about as hard: the sim beats each one 86-92% of the time.
    // the Twins, the Ceiling and the Card Mechanic are harder rules, so lower targets; the Mime, the Purist
    // and the Pit Boss are gentler rules, so higher ones
    public sealed class TheTwins : Boss
    {
        public override double TargetFactor => 0.75;
        public override void OnTableStart(Table table) => table.Rules.DealerPlaysTwoHands = true;
    }

    public sealed class TheIceman : Boss
    {
        public override double TargetFactor => 1.15;
        public override void OnTableStart(Table table) => table.Rules.DealerStandsOn = 15;

        public override void OnDealEnd(Table table)
        {
            var dealer = table.DealerValue;
            if (!dealer.IsBust && (dealer.Total == 15 || dealer.Total == 16)) table.Announce($"Stops cold on {dealer.Total}.");
        }
    }

    public sealed class TheCheat : Boss
    {
        public override void OnTableStart(Table table) => table.Rules.DealerRedrawsFirstBust = true;
    }

    public sealed class TheMime : Boss
    {
        public override double TargetFactor => 1.4;
        public override void OnTableStart(Table table) => table.Rules.SecondCardFaceDown = true;
    }

    public sealed class TheTaxman : Boss
    {
        public override double TargetFactor => 1.15;
        public override void OnTableStart(Table table) => table.Rules.HitCost = Balance.TaxmanHitCost;
        public override void OnHit(Table table, PlayerHand hand) => table.Announce($"Takes {Balance.TaxmanHitCost} gold for that card.");
    }

    public sealed class ThePurist : Boss
    {
        public override double TargetFactor => 1.35;
        public override void OnTableStart(Table table) => table.Rules.AcesAlwaysOne = true;

        public override void OnDeal(Table table)
        {
            if (table.PlayerHands.Any(h => h.Cards.Any(c => c.Rank == Rank.Ace && !c.IsCursed))) table.Announce("Your Ace counts as 1. Only 1.");
        }
    }

    public sealed class TheCeiling : Boss
    {
        public override double TargetFactor => 0.6;
        public override void OnTableStart(Table table) => table.Rules.PlayerBustLimit = 19;
    }

    public sealed class TheSweeper : Boss
    {
        public override double TargetFactor => 1.15;
        public override void OnTableStart(Table table) => table.Rules.BurnsDisabled = true;
    }

    public sealed class TheSilencer : Boss
    {
        public override double TargetFactor => 1.15;
        public override void OnTableStart(Table table)
        {
            var leftmost = table.Gambler.Vices.FirstOrDefault();
            if (leftmost == null) return;
            leftmost.Disabled = true;
            table.Announce($"Silences your {leftmost.Name}.");
        }
    }

    public sealed class TheCardMechanic : Boss
    {
        public override double TargetFactor => 0.8;
        public override void OnStand(Table table, PlayerHand hand)
        {
            int highest = 0;
            for (int i = 1; i < hand.Cards.Count; i++)
                if (hand.Cards[i].Value > hand.Cards[highest].Value) highest = i;
            var yours = hand.Cards[highest];
            var his = table.DealerCards.Count > 0 ? table.DealerCards[0] : null;
            table.SwapWithDealerUpCard(hand, highest);
            if (his != null && yours != his) table.Announce($"Swaps your {yours.RankLabel} for his {his.RankLabel}.");
        }
    }

    public sealed class TheHoarder : Boss
    {
        public override double TargetFactor => 1.15;
        public override void OnTableStart(Table table)
        {
            for (int i = 0; i < 3; i++) table.ShuffleIntoShoe(Card.Cursed(Curse.DeadCard), addToDeck: true);
            table.Announce("Slips 3 Dead Cards into your deck.");
        }
    }

    public sealed class ThePitBoss : Boss
    {
        public override double TargetFactor => 1.4;
        public override void OnDeal(Table table)
        {
            table.AddHeat(Balance.PitBossHeatPerHand);
            table.Announce($"+{Balance.PitBossHeatPerHand} Heat. He's watching.");
        }
    }

    public sealed class TheEyeInTheSky : Boss
    {
        public override double TargetFactor => Balance.EyeInTheSkyTargetFactor;

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
                if (table.Gambler.Gold > 0)
                {
                    table.Gambler.LoseGold(Balance.EnforcerTake);
                    table.Announce($"Takes {Balance.EnforcerTake} gold.");
                }
                else
                {
                    table.TakeRandomVice();
                    table.Announce("You're broke, so he takes a Vice.");
                }
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
            table.Announce($"Now plays as {_twist.Name}: {_twist.Definition.Ability}.");
        }

        public override void OnDeal(Table table) => _twist?.OnDeal(table);

        public override void OnHit(Table table, PlayerHand hand) => _twist?.OnHit(table, hand);

        public override void OnStand(Table table, PlayerHand hand) => _twist?.OnStand(table, hand);

        public override void OnDealEnd(Table table) => _twist?.OnDealEnd(table);
    }
}
