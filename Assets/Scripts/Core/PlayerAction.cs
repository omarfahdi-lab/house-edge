namespace HouseEdge.Core
{
    // player inputs are data, so a run is a seed plus a list of these
    public enum PlayerAction
    {
        Deal,
        Hit,
        Stand,
        DoubleDown,
        Split,
        Burn,
        Surrender,
        TakeInsurance,
        DeclineInsurance,

        // before the deal
        AnteUp,
        BetPerfectPair,
        Bet21Plus3,
        BetBustIt,
        BetLuckyLadies,
        BetHotHand,

        // Card Mechanic Vice: swap your last card with the dealer's face-up card
        SwapWithDealer,
        // Cold Read and The Dealer Vices: you decide the dealer's next card
        LetDealerDraw,
        StopDealer,

        // before the deal: raise the bet one step (x2, x3), and back to x1 after the top
        Raise,

        // a lucky chip rolling across the felt: grab it
        CatchChip,
        // slide your last card up your sleeve, once a table: while the camera looks away, or caught by it
        PalmClean,
        PalmSeen,

        // Keep 2 of 3: the card you give away becomes the dealer's hole card, and you keep the other two
        GiveFirst,
        GiveSecond,
        GiveThird,
    }

    public enum TablePhase
    {
        ReadyToDeal,
        InsuranceOffer,
        PlayerTurn,
        // a Vice lets you decide whether the dealer takes the next card
        DealerDecision,
        Won,
        Lost,
        // Keep 2 of 3: three cards dealt to you, one has to go to the dealer
        Choosing,
    }
}
