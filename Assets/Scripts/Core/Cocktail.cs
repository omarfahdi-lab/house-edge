using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    public enum CocktailKind
    {
        OldFashioned, Martini, Screwdriver, LongIsland, LastWord, Mojito,
        Champagne, DarkAndStormy, IceWater, WhiskeySour, ColdBrew, WildCard,
    }

    public enum CocktailUse
    {
        // changes cards in your deck, so only between tables (in the shop)
        OnDeck,
        // only while you're at a table
        AtTable,
        Anytime,
    }

    // a one-use consumable. you hold up to 2
    public sealed class Cocktail
    {
        public CocktailKind Kind { get; }

        public Cocktail(CocktailKind kind) => Kind = kind;

        public string Name => Cocktails.Name(Kind);
        public string Description => Cocktails.Describe(Kind);
        public CocktailUse Use => Cocktails.UseOf(Kind);
        // how many deck cards it needs picked: (at least, at most)
        public (int min, int max) Targets => Cocktails.Targets(Kind);

        public override string ToString() => Name;
    }

    public static class Cocktails
    {
        public static readonly IReadOnlyList<CocktailKind> All = (CocktailKind[])Enum.GetValues(typeof(CocktailKind));

        public static string Name(CocktailKind kind) => kind switch
        {
            CocktailKind.OldFashioned => "Old Fashioned",
            CocktailKind.LongIsland => "Long Island",
            CocktailKind.LastWord => "Last Word",
            CocktailKind.DarkAndStormy => "Dark and Stormy",
            CocktailKind.IceWater => "Ice Water",
            CocktailKind.WhiskeySour => "Whiskey Sour",
            CocktailKind.ColdBrew => "Cold Brew",
            CocktailKind.WildCard => "Wild Card",
            _ => kind.ToString(),
        };

        public static string Describe(CocktailKind kind) => kind switch
        {
            CocktailKind.OldFashioned => "Make up to 2 cards Gilded.",
            CocktailKind.Martini => "Duplicate a card in your deck.",
            CocktailKind.Screwdriver => "Remove up to 2 cards from your deck.",
            CocktailKind.LongIsland => "Raise a card's value by 1.",
            CocktailKind.LastWord => "Lower a card's value by 1.",
            CocktailKind.Mojito => "Make a card Flex.",
            CocktailKind.Champagne => "Make a card Fragile.",
            CocktailKind.DarkAndStormy => "Add a Red Stamp to a card.",
            CocktailKind.IceWater => "+2 Burns at this table.",
            CocktailKind.WhiskeySour => "Gain 2 gold for each Vice you hold.",
            CocktailKind.ColdBrew => "Lower your Heat by 25.",
            _ => "Create a random Vice if you have room.",
        };

        public static CocktailUse UseOf(CocktailKind kind) => kind switch
        {
            CocktailKind.IceWater => CocktailUse.AtTable,
            CocktailKind.WhiskeySour => CocktailUse.Anytime,
            CocktailKind.ColdBrew => CocktailUse.Anytime,
            CocktailKind.WildCard => CocktailUse.Anytime,
            _ => CocktailUse.OnDeck,
        };

        public static (int min, int max) Targets(CocktailKind kind) => kind switch
        {
            CocktailKind.OldFashioned => (1, 2),
            CocktailKind.Screwdriver => (1, 2),
            CocktailKind.IceWater => (0, 0),
            CocktailKind.WhiskeySour => (0, 0),
            CocktailKind.ColdBrew => (0, 0),
            CocktailKind.WildCard => (0, 0),
            _ => (1, 1),
        };

        // a card a cocktail can't change: curses only go away at the Card Room or with a Screwdriver
        public static bool CanTarget(CocktailKind kind, Card card)
        {
            if (kind == CocktailKind.Screwdriver || kind == CocktailKind.Martini) return true;
            if (card.IsCursed) return false;
            if (kind == CocktailKind.LongIsland) return card.Rank != Rank.Ace;
            if (kind == CocktailKind.LastWord) return card.Rank != Rank.Two;
            return true;
        }
    }
}
