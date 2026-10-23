using System.Collections.Generic;

namespace HouseEdge.Core
{
    // what can happen at a "?" stop on the map: a short scene and a choice
    public enum EventKind { LuckyChip, Hustler, SpilledTray, BackAlley, Shredder, PhotoBooth, SecuritySweep, CompTicket, Magician }

    public static class Events
    {
        public static readonly IReadOnlyList<EventKind> All = (EventKind[])System.Enum.GetValues(typeof(EventKind));

        public static string Title(EventKind kind) => kind switch
        {
            EventKind.LuckyChip => "The Lucky Chip",
            EventKind.Hustler => "The Hustler",
            EventKind.SpilledTray => "Spilled Drinks",
            EventKind.BackAlley => "The Back Alley",
            EventKind.Shredder => "The Shredder",
            EventKind.PhotoBooth => "The Photo Booth",
            EventKind.SecuritySweep => "Security Sweep",
            EventKind.Magician => "The Magician",
            _ => "A Comp Ticket",
        };

        public static string Scene(EventKind kind) => kind switch
        {
            EventKind.LuckyChip => "A drunk high roller presses his lucky chip into your hand and wanders off.",
            EventKind.Hustler => "A hustler in a cheap suit flips a coin. \"Double or nothing, friend?\"",
            EventKind.SpilledTray => "A waiter trips and a tray of drinks lands at your feet.",
            EventKind.BackAlley => "A man in the alley sells Vices out of his coat. No questions asked, no refunds.",
            EventKind.Shredder => "A cleaner is feeding old cards into a shredder. He nods at your deck.",
            EventKind.PhotoBooth => "An old photo booth prints a perfect copy of anything you put in it.",
            EventKind.SecuritySweep => "Security is sweeping the floor, checking faces against a list.",
            EventKind.Magician => "A card magician at the bar fans a deck at you. \"Want to learn one?\"",
            _ => "A comp ticket is stuck to the bottom of your shoe.",
        };

        // the choices: what you see, and what it costs in gold (negative pays you)
        public static List<(string label, int cost)> Choices(EventKind kind) => kind switch
        {
            EventKind.LuckyChip => new List<(string, int)> { ("Pocket it: a random Lucky Vice, +20 Heat", 0), ("Turn it in: +8 gold", -8) },
            EventKind.Hustler => new List<(string, int)> { ("Bet 5 gold on a coin flip: win 10", 5) },
            EventKind.SpilledTray => new List<(string, int)> { ("Grab one: a free Cocktail", 0), ("Help him up: +3 gold tip", -3) },
            EventKind.BackAlley => new List<(string, int)> { ("Buy blind: a Rare Vice, and a Dead Card joins your deck", 0) },
            EventKind.Shredder => new List<(string, int)> { ("Shred your 3 lowest cards", 0) },
            EventKind.PhotoBooth => new List<(string, int)> { ("Copy your best card", 0) },
            EventKind.SecuritySweep => new List<(string, int)> { ("Lie low: -25 Heat", 4), ("Bluff past them: -50 Heat or +25 Heat", 0) },
            EventKind.Magician => new List<(string, int)> { ("Learn a trick: a random Trick card", 0), ("Just watch: tip 2 gold", 2) },
            _ => new List<(string, int)> { ("Cash it in: +1 level to a random combo Comp", 0), ("Sell it: +6 gold", -6) },
        };
    }
}
