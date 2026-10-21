using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    // everything the player carries from table to table in a run
    public sealed class Gambler
    {
        private readonly int[] _compLevels = new int[Comps.All.Count];

        public int Gold { get; private set; }
        public List<Card> Deck { get; }

        // in trigger order, left to right
        public List<Vice> Vices { get; } = new List<Vice>();
        public int ViceSlots { get; set; } = Balance.ViceSlots;
        public bool HasFreeViceSlot => Vices.Count < ViceSlots;

        public List<Cocktail> Cocktails { get; } = new List<Cocktail>();
        public int CocktailSlots { get; set; } = Balance.CocktailSlots;
        public bool HasFreeCocktailSlot => Cocktails.Count < CocktailSlots;

        // how suspicious the casino is, 0 to 100
        public int Heat { get; private set; }

        // owed to the Loan Shark, due by the end of DebtDueFloor
        public int Debt { get; private set; }
        public int DebtDueFloor { get; private set; }

        public Gambler(int gold = Balance.StartingGold, IEnumerable<Card> deck = null)
        {
            Gold = gold;
            Deck = new List<Card>(deck ?? Shoe.StandardDeck());
        }

        public bool CanAfford(int cost) => Gold >= cost;

        public void GainGold(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Gold += amount;
        }

        // never goes below 0
        public int LoseGold(int amount)
        {
            int lost = Math.Min(Math.Max(amount, 0), Gold);
            Gold -= lost;
            return lost;
        }

        public HeatLevel HeatLevel => Balance.HeatLevelFor(Heat);

        // Vices like Poker Face shrink what you gain. returns what was actually added
        public int AddHeat(int amount)
        {
            if (amount <= 0) return 0;
            double factor = 1;
            foreach (var vice in Vices)
                if (!vice.Disabled) factor *= vice.HeatFactor;
            factor *= ArchetypeBonuses.HeatFactor(this);
            int gained = (int)Math.Round(amount * factor, MidpointRounding.AwayFromZero);
            int before = Heat;
            Heat = Math.Min(Balance.MaxHeat, Heat + gained);
            return Heat - before;
        }

        public void CoolDown(int amount) => Heat = Math.Max(0, Heat - amount);

        public void Borrow(int amount, int dueFloor)
        {
            if (Debt == 0) DebtDueFloor = dueFloor;
            Debt += amount;
            GainGold(amount);
        }

        // 50% more after every table, unless a Vice like Loan Officer freezes it
        public void GrowDebt()
        {
            if (Debt == 0 || Vices.Any(v => !v.Disabled && v.FreezesDebt)) return;
            Debt = (int)Math.Ceiling(Debt * Balance.DebtGrowth);
        }

        public void RepayDebt()
        {
            int paid = LoseGold(Debt);
            Debt -= paid;
        }

        public void ClearDebt() => Debt = 0;

        public int CompLevel(Comp comp) => _compLevels[(int)comp];
        public void LevelUp(Comp comp) => _compLevels[(int)comp]++;

        public int ArchetypeCount(Archetype archetype) => Vices.Count(v => v.Definition.Archetype == archetype);
    }
}
