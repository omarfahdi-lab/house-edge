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
    }

    public enum TablePhase
    {
        ReadyToDeal,
        InsuranceOffer,
        PlayerTurn,
        Won,
        Lost,
    }
}
