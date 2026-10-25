namespace HouseEdge.Core
{
    public enum PushRule { Push, DealerWins, PlayerWins }

    // the rules one table plays by. bosses, dealers, Vices and Heat change them in OnTableStart
    public sealed class TableRules
    {
        // the player's limit. the dealer always busts over 21
        public int PlayerBustLimit = Balance.BustLimit;
        public int DealerStandsOn = Balance.DealerStandsOn;
        public bool DealerHitsSoft17;
        public bool AcesAlwaysOne;
        public PushRule Pushes = PushRule.Push;

        public bool BurnsDisabled;
        // Bottomless Burns: Burns never run out
        public bool UnlimitedBurns;
        public int CharlieCards = Balance.CharlieCards;
        public int HitCost;
        public bool SplitAnyTwo;
        public bool DoubleAfterHit;

        // the Mime: your second card stays face down until you stand (only the view changes)
        public bool SecondCardFaceDown;
        public bool HoleCardFaceUp;
        public bool SeeNextCard;

        // the Twins: the dealer plays two hands and you have to beat both
        public bool DealerPlaysTwoHands;

        // reshuffle the discards back in every N deals (0 = never)
        public int ReshuffleEveryDeals;
        public bool ReshuffleAfterDealerBust;

        // the dealer hits a hard 17 by mistake with 1 chance in N (0 = never)
        public int DealerMistakeOneIn;
        // The Cheat: once a deal, a card that busts the dealer goes back and he draws again
        public bool DealerRedrawsFirstBust;

        // Keep 2 of 3: you're dealt three cards and give one to the dealer as his hole card
        public bool KeepTwoOfThree;

        // the Rival: the dealer draws until he beats your best hand, and maybe on a tie too
        public bool DealerChasesPlayer;
        public bool DealerHitsOnTies;

        public TableRules Clone() => (TableRules)MemberwiseClone();

        public void CopyFrom(TableRules other)
        {
            PlayerBustLimit = other.PlayerBustLimit;
            DealerStandsOn = other.DealerStandsOn;
            DealerHitsSoft17 = other.DealerHitsSoft17;
            AcesAlwaysOne = other.AcesAlwaysOne;
            Pushes = other.Pushes;
            BurnsDisabled = other.BurnsDisabled;
            UnlimitedBurns = other.UnlimitedBurns;
            CharlieCards = other.CharlieCards;
            HitCost = other.HitCost;
            SplitAnyTwo = other.SplitAnyTwo;
            DoubleAfterHit = other.DoubleAfterHit;
            SecondCardFaceDown = other.SecondCardFaceDown;
            HoleCardFaceUp = other.HoleCardFaceUp;
            SeeNextCard = other.SeeNextCard;
            DealerPlaysTwoHands = other.DealerPlaysTwoHands;
            ReshuffleEveryDeals = other.ReshuffleEveryDeals;
            ReshuffleAfterDealerBust = other.ReshuffleAfterDealerBust;
            DealerMistakeOneIn = other.DealerMistakeOneIn;
            DealerRedrawsFirstBust = other.DealerRedrawsFirstBust;
            KeepTwoOfThree = other.KeepTwoOfThree;
            DealerChasesPlayer = other.DealerChasesPlayer;
            DealerHitsOnTies = other.DealerHitsOnTies;
        }
    }
}
