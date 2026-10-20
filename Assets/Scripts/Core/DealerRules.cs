namespace HouseEdge.Core
{
    public static class DealerRules
    {
        // stands on all 17s, soft ones included
        public static bool ShouldHit(HandValue value) => value.Total < Balance.DealerStandsOn;

        // the dealer checks the hole card for blackjack when showing an ace or a 10-value card
        public static bool PeeksFor(Card upCard) => upCard.Value >= 10;
    }
}
