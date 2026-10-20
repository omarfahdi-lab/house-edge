using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // enhancements, stamps and curse cards. deal order: player, dealer up, player, dealer hole, then draws
    public class CardModTests
    {
        private static HandScore Score(Table t) => t.LastScores.Single();

        // enhancements

        [Test]
        public void Gilded_Pays3Gold_InAWinningHand()
        {
            var t = TableOf(C("K", Enhancement.Gilded), "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold + 3));
        }

        [Test]
        public void Gilded_PaysNothing_WhenYouLose()
        {
            var t = TableOf(C("7", Enhancement.Gilded), "10", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold));
        }

        [TestCase(new[] { "10" }, 21)]
        [TestCase(new[] { "5", "9" }, 21)]
        [TestCase(new[] { "K", "Q" }, 21)]
        [TestCase(new[] { "K", "Q", "5" }, 26)]
        public void Flex_PicksTheValueClosestTo21(string[] others, int total)
        {
            var cards = others.Select(C).ToList();
            cards.Add(C("2", Enhancement.Flex));
            Assert.That(HandValue.Of(cards).Total, Is.EqualTo(total));
        }

        [Test]
        public void Fragile_DoublesTheMult_AndBreaksAfter3Wins()
        {
            var gambler = new Gambler();
            var fragile = C("K", Enhancement.Fragile);
            gambler.Deck.Add(fragile);
            var t = new Table(gambler, StackOf(fragile, "9", "Q", "9"), new GameRandom(1), long.MaxValue);
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(4));
            Assert.That(fragile.FragileWins, Is.EqualTo(1));

            fragile.FragileWins = 2;
            var t2 = new Table(gambler, StackOf(fragile, "9", "Q", "9"), new GameRandom(1), long.MaxValue);
            Play(t2, Deal, Stand);
            Assert.That(fragile.Destroyed, Is.True);
            Assert.That(gambler.Deck, Has.No.Member(fragile));
        }

        [Test]
        public void Marked_ShowsAsTheNextCard_AndAdds2HeatWhenDrawn()
        {
            var t = TableOf("10", "10", "6", "9", C("4", Enhancement.Marked));
            t.Apply(Deal);
            Assert.That(t.NextCardPreview?.ToString(), Is.EqualTo("4♠"));
            t.Apply(Hit);
            Assert.That(t.Gambler.Heat, Is.EqualTo(Balance.MarkedHeat));
        }

        [Test]
        public void UnmarkedNextCard_StaysHidden()
        {
            var t = TableOf("10", "10", "6", "9", "4");
            t.Apply(Deal);
            Assert.That(t.NextCardPreview, Is.Null);
        }

        [Test]
        public void Ghost_IsWorth0_AndGives15Chips()
        {
            // 10 + 9 + ghost = 19 vs 18
            var t = TableOf("10", "10", "9", "8", C("5", Enhancement.Ghost));
            Play(t, Deal, Hit, Stand);
            Assert.That(t.PlayerHands[0].Value.Total, Is.EqualTo(19));
            Assert.That(Score(t).Chips, Is.EqualTo(19 + 10 + 15));
        }

        [Test]
        public void Hot_GivesMultForEachTimeItWasDrawnThisTable()
        {
            var hot = C("K", Enhancement.Hot);
            hot.TimesDrawnThisTable = 2;
            var t = TableOf(hot, "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Mult, Is.EqualTo(2 + 3));
        }

        [Test]
        public void Heavy_CountsOneHigher_AndGives20Chips()
        {
            var t = TableOf(C("8", Enhancement.Heavy), "9", "10", "9");
            Play(t, Deal, Stand);
            Assert.That(t.PlayerHands[0].Value.Total, Is.EqualTo(19));
            Assert.That(Score(t).Chips, Is.EqualTo(19 + 10 + 20));
        }

        // stamps

        [Test]
        public void RedStamp_ScoresTheCardTwice()
        {
            var t = TableOf(C("K", Stamp.Red), "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Chips, Is.EqualTo(30 + 10));
        }

        [Test]
        public void RedStamp_RetriggersTheEnhancement()
        {
            var t = TableOf(C("K", Enhancement.Heavy, Stamp.Red), "9", "9", "9");
            Play(t, Deal, Stand);
            // a Heavy K is 11: 11 + 9 = 20 beats 18. +20 twice and the card's 11 again
            Assert.That(Score(t).Chips, Is.EqualTo(20 + 10 + 20 + 11 + 20));
        }

        [Test]
        public void BlueStamp_GivesABurn_WhenYouDrawIt()
        {
            var t = TableOf(C("2", Stamp.Blue), "10", "3", "9");
            t.Apply(Deal);
            Assert.That(t.BurnsLeft, Is.EqualTo(Balance.StartingBurns + 1));
        }

        [Test]
        public void GoldStamp_Gives2Gold_WhenYouDrawIt()
        {
            var t = TableOf(C("2", Stamp.Gold), "10", "3", "9");
            t.Apply(Deal);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold + 2));
        }

        [Test]
        public void BlackStamp_TheDealerNeverDrawsIt()
        {
            // the dealer's up card would be the black K; it goes to the bottom and he gets the 9
            var t = TableOf("10", C("K", Stamp.Black), "8", "9", "7");
            t.Apply(Deal);
            Assert.That(t.DealerCards.Select(c => c.ToString()), Has.No.Member("K♠"));
        }

        // curses

        [Test]
        public void DeadCard_CountsTen_AndTakes2Mult()
        {
            var t = TableOf(Card.Cursed(Curse.DeadCard), "9", "K", "9");
            Play(t, Deal, Stand);
            Assert.That(t.PlayerHands[0].Value.Total, Is.EqualTo(20));
            Assert.That(Score(t).Mult, Is.EqualTo(2 - 2));
        }

        [Test]
        public void SnakeEyes_CountsOne_AndEndsYourTurn()
        {
            var t = TableOf("10", "10", "5", "8", Card.Cursed(Curse.SnakeEyes));
            Play(t, Deal, Hit);
            Assert.That(t.PlayerHands[0].Value.Total, Is.EqualTo(16));
            Assert.That(t.Phase, Is.EqualTo(TablePhase.ReadyToDeal), "the hand was played out");
        }

        [Test]
        public void TaxCard_Costs2Gold_WhenDrawn()
        {
            var t = TableOf("10", "10", "5", "8", Card.Cursed(Curse.TaxCard));
            Play(t, Deal, Hit);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold - 2));
        }

        [Test]
        public void Jinx_DisablesAViceForThisHandOnly()
        {
            var penny = ViceCatalog.Get("LuckyPenny").Create();
            var t = new Table(StackOf("K", "9", Card.Cursed(Curse.Jinx), "9", "K", "9", "Q", "9"), long.MaxValue, new[] { penny });
            t.Apply(Deal);
            Assert.That(penny.Disabled, Is.True);
            t.Apply(Stand);
            t.Apply(Deal);
            Assert.That(penny.Disabled, Is.False);
        }
    }

    public class SideBetTests
    {
        [Test]
        public void SideBets_AreOfferedBeforeTheDeal_AndCostGold()
        {
            var t = TableOf("8", "10", "8h", "9");
            Assert.That(t.LegalActions(), Has.Member(BetPerfectPair));
            t.Apply(BetPerfectPair);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold - 2));
            Assert.That(t.LegalActions(), Has.No.Member(BetHotHand), "one side bet per hand");
        }

        [Test]
        public void PerfectPair_Pays8_OrPays20ForTheSameSuit()
        {
            var pair = TableOf("8", "10", "8h", "9");
            Play(pair, BetPerfectPair, Deal);
            Assert.That(pair.SideBetResult?.Payout, Is.EqualTo(8));

            var perfect = TableOf("8", "10", "8", "9");
            Play(perfect, BetPerfectPair, Deal);
            Assert.That(perfect.SideBetResult?.Payout, Is.EqualTo(20));
        }

        [Test]
        public void TwentyOnePlus3_PaysForFlushStraightOrTrips()
        {
            var flush = TableOf("2", "9", "7", "Q");
            Play(flush, Bet21Plus3, Deal);
            Assert.That(flush.SideBetResult?.Payout, Is.EqualTo(6));

            var straight = TableOf("7", "9h", "8", "Q");
            Play(straight, Bet21Plus3, Deal);
            Assert.That(straight.SideBetResult?.Payout, Is.EqualTo(8));

            var trips = TableOf("7", "7h", "7d", "Q");
            Play(trips, Bet21Plus3, Deal);
            Assert.That(trips.SideBetResult?.Payout, Is.EqualTo(15));
        }

        [Test]
        public void BustIt_PaysWhenTheDealerBusts_EvenIfYouLose()
        {
            // you bust, but the dealer still plays for Bust It and busts with 3 cards
            var t = TableOf("10", "10", "6", "6", "K", "K");
            Play(t, BetBustIt, Deal, Hit, Stand);
            Assert.That(t.LastScores.Single().Outcome, Is.EqualTo(Outcome.Bust));
            Assert.That(t.SideBetResult?.Payout, Is.EqualTo(5));
        }

        [Test]
        public void LuckyLadies_Pays10ForTwenty_And100ForTwoQueensOfHearts()
        {
            var twenty = TableOf("K", "9", "Q", "9");
            Play(twenty, BetLuckyLadies, Deal);
            Assert.That(twenty.SideBetResult?.Payout, Is.EqualTo(10));

            var queens = TableOf("Qh", "9", "Qh", "9");
            Play(queens, BetLuckyLadies, Deal);
            Assert.That(queens.SideBetResult?.Payout, Is.EqualTo(100));
        }

        [Test]
        public void HotHand_Pays2Gold_AndAdds5Mult_OnAWin()
        {
            var t = TableOf("K", "9", "Q", "9");
            Play(t, BetHotHand, Deal, Stand);
            Assert.That(t.LastScores.Single().Mult, Is.EqualTo(2 + 5));
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold - 1 + 2));
        }

        [Test]
        public void AnteUp_DoublesTheMult()
        {
            var t = TableOf("K", "9", "Q", "9");
            Play(t, AnteUp, Deal, Stand);
            Assert.That(t.LastScores.Single().Total, Is.EqualTo(30 * 4));
        }

        [Test]
        public void AnteUp_Costs3Gold_WhenYouLose()
        {
            var t = TableOf("10", "10", "7", "9");
            Play(t, AnteUp, Deal, Stand);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold - 3));
            Assert.That(t.AnteLost, Is.True);
        }
    }
}
