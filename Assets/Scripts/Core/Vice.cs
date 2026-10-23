using System;

namespace HouseEdge.Core
{
    public enum Rarity { Common, Uncommon, Rare, Legendary, Cursed }

    public enum Archetype { Lucky, Risky, Cheater, Greedy, Steady, Combo }

    // Vices that let you decide whether the dealer draws: Cold Read once a hand, The Dealer once a table
    public enum DealerControl { None, ColdRead, Full }

    // what a Vice is, separate from the instance a player owns (which can hold state like a streak)
    public sealed class ViceDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public Rarity Rarity { get; }
        public Archetype Archetype { get; }
        public string Description { get; }
        private readonly Func<Vice> _create;

        public ViceDefinition(string name, Rarity rarity, Archetype archetype, string description, Func<Vice> create)
        {
            Id = name.Replace(" ", "").Replace("'", "");
            Name = name;
            Rarity = rarity;
            Archetype = archetype;
            Description = description;
            _create = create;
        }

        public bool IsCheater => Archetype == Archetype.Cheater;
        public bool IsCursed => Rarity == Rarity.Cursed;

        public Vice Create()
        {
            var vice = _create();
            vice.Definition = this;
            return vice;
        }

        public override string ToString() => Name;
    }

    // a passive item that bends the rules. hooks run left to right in the player's Vice order
    public abstract class Vice : TableEffect
    {
        public ViceDefinition Definition { get; internal set; }
        public string Name => Definition.Name;

        // times it changed a hand's score this run. Vices level up as they trigger
        public int Triggers { get; internal set; }
        public int Level => Triggers >= Balance.ViceLevel3At ? 3 : Triggers >= Balance.ViceLevel2At ? 2 : 1;
        // triggers needed for the next level, or 0 at the top
        public int NextLevelAt => Level == 1 ? Balance.ViceLevel2At : Level == 2 ? Balance.ViceLevel3At : 0;
        // what the level adds on top of the Vice's own effect
        public double LevelBoost => (Level - 1) * Balance.ViceLevelBoost;

        // Bet Spreader: a lost raise costs this many fewer Hands
        public virtual int RaiseRefund => 0;

        // switched off by bosses, Jinx or Heat for a while
        public bool Disabled { get; set; }
        public override bool IsActive => !Disabled;

        // shown on the Vice for scaling ones, e.g. "+6 Mult"
        public virtual string Status => null;

        // multiplies Heat you gain while you hold it (Poker Face is 0.7)
        public virtual double HeatFactor => 1;

        // chance effects: Lady Luck doubles the odds, Rabbit's Foot rolls twice
        public virtual int ModifyChances(int chances) => chances;
        public virtual bool RollsTwice => false;

        public virtual int SideBetDiscount => 0;

        // Natural Born: a dealer blackjack can't tie yours
        public virtual bool BlackjacksCantBeTied => false;

        // Lady Luck: shop rarity rolls twice and keeps the better one
        public virtual bool LuckyShop => false;

        // Tell Reader: the dealer's tell is spelled out
        public virtual bool RevealsTells => false;

        // Loan Officer: Loan Shark debt doesn't grow
        public virtual bool FreezesDebt => false;

        // Bad Habit: you can't stand until you've hit
        public virtual bool MustHitEveryHand => false;

        // payout changes: All In blocks interest, Cashier pays more per unused Hand
        public virtual bool BlocksInterest => false;
        public virtual int GoldPerUnusedHand => Balance.GoldPerUnusedHand;

        // Card Mechanic: once per hand, swap your last card with the dealer's face-up card
        public virtual bool GrantsSwap => false;
        public virtual DealerControl DealerControl => DealerControl.None;

        // scaling Vices grow twice as fast with the Combo set bonus
        protected static int Growth(Table table) => ArchetypeBonuses.ScalingRate(table.Gambler);

        public override string ToString() => Name;
    }
}
