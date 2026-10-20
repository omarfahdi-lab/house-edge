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
    }
}
