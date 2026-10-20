namespace HouseEdge.Core
{
    public enum Outcome
    {
        Lose,
        Bust,
        Surrender,
        Push,
        Win,
        // a win with exactly 21 using 3+ cards
        TwentyOne,
        Blackjack,
        Charlie,
    }

    public static class OutcomeExtensions
    {
        public static bool IsWin(this Outcome outcome) =>
            outcome == Outcome.Win || outcome == Outcome.TwentyOne || outcome == Outcome.Blackjack || outcome == Outcome.Charlie;

        public static string Label(this Outcome outcome) => outcome switch
        {
            Outcome.TwentyOne => "21",
            _ => outcome.ToString(),
        };
    }
}
