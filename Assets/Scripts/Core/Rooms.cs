using System;
using System.Collections.Generic;

namespace HouseEdge.Core
{
    public enum RoomKind { Bar, BackRoom, PawnShop, LoanShark, CardRoom, LostAndFound, SecurityOffice, HighLimitLounge }

    public enum RoomActionKind
    {
        BuyCocktail, FreeCocktail,
        PlayBackRoom,
        SellViceFull, BuyUsedRare,
        Borrow,
        RemoveCard, DuplicateCard, EnhanceCard,
        TakeLostVice,
        PayOffHeat, Bribe,
        SkipNextTable,
    }

    // one thing you can do in a room. Arg is the cocktail, loan amount or Vice slot it's about
    public readonly struct RoomOption
    {
        public RoomActionKind Kind { get; }
        public string Label { get; }
        public int Cost { get; }
        public int Arg { get; }
        public bool NeedsCard { get; }

        public RoomOption(RoomActionKind kind, string label, int cost = 0, int arg = -1, bool needsCard = false)
        {
            Kind = kind;
            Label = label;
            Cost = cost;
            Arg = arg;
            NeedsCard = needsCard;
        }
    }

    public static class Rooms
    {
        public static readonly IReadOnlyList<RoomKind> All = (RoomKind[])Enum.GetValues(typeof(RoomKind));

        public static string Name(RoomKind room) => room switch
        {
            RoomKind.Bar => "The Bar",
            RoomKind.BackRoom => "Back Room",
            RoomKind.PawnShop => "Pawn Shop",
            RoomKind.LoanShark => "Loan Shark",
            RoomKind.CardRoom => "Card Room",
            RoomKind.LostAndFound => "Lost and Found",
            RoomKind.SecurityOffice => "Security Office",
            _ => "High Limit Lounge",
        };

        public static string Describe(RoomKind room) => room switch
        {
            RoomKind.Bar => "Cocktails are half price, or take one random Cocktail for free.",
            RoomKind.BackRoom => "One hand of high-stakes blackjack. Win and your gold doubles. Lose and it's gone.",
            RoomKind.PawnShop => "Sell Vices for full price, or buy a used Rare (it might be Cursed).",
            RoomKind.LoanShark => "Borrow 10, 20 or 30 gold. Debt grows 50% each table. Unpaid by the end of the next floor, and The Enforcer shows up.",
            RoomKind.CardRoom => "Remove, duplicate or enhance one card in your deck.",
            RoomKind.LostAndFound => "Take a free random Vice. 1 in 3 chance it's Cursed.",
            RoomKind.SecurityOffice => "Pay 2 gold per 10 Heat to clear it, or bribe a guard for a Cheater Vice (+20 Heat).",
            _ => "Skip the next table entirely, but the table after it has a 1.5x target.",
        };
    }

    // a room you walked into. remembers what you already did there
    public sealed class Room
    {
        public RoomKind Kind { get; }
        public string Name => Rooms.Name(Kind);
        public string Description => Rooms.Describe(Kind);

        // Bar
        public List<CocktailKind> Cocktails { get; } = new List<CocktailKind>();
        public List<bool> CocktailSold { get; } = new List<bool>();
        // Pawn Shop
        public ViceDefinition UsedRare { get; internal set; }
        // Back Room: a one-hand table, once it starts
        public Table BackRoomTable { get; internal set; }

        // a one-time choice (free drink, loan, card service, free Vice, bribe, back room hand, skip) was made
        public bool Used { get; internal set; }
        public string Result { get; internal set; }

        public Room(RoomKind kind) => Kind = kind;
    }
}
