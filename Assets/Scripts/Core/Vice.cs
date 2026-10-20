using System;

namespace HouseEdge.Core
{
    public enum Rarity { Common, Uncommon, Rare, Legendary }

    public enum Archetype { Lucky, Risky, Cheater, Greedy, Steady, Combo }

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

        // switched off by bosses, Jinx or Heat for a while
        public bool Disabled { get; set; }
        public override bool IsActive => !Disabled;

        // shown on the Vice for scaling ones, e.g. "+6 Mult"
        public virtual string Status => null;

        public override string ToString() => Name;
    }
}
