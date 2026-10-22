using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // every outcome from the scoring table in DESIGN.md. deal order: player, dealer up, player, dealer hole
    public class ScoringTests
    {
        private static HandScore Only(Table t) => t.LastScores.Single();

        [Test]
        public void Win_Is_CardsPlus10Chips_Times2Mult()
        {
            // the DESIGN.md example: win with 20 -> (20 + 10) x 2 = 60
            var t = StackedTableNoTarget("K", "9", "Q", "9");
            Play(t, Deal, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Win));
            Assert.That(Only(t).Chips, Is.EqualTo(30));
            Assert.That(Only(t).Mult, Is.EqualTo(2));
            Assert.That(Only(t).Total, Is.EqualTo(60));
            Assert.That(t.Score, Is.EqualTo(60));
        }

        [Test]
        public void TwentyOne_WithThreeCards_Is_CardsPlus20Chips_Times3Mult()
        {
            var t = StackedTableNoTarget("K", "9", "5", "9", "6");
            Play(t, Deal, Hit, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.TwentyOne));
            Assert.That(Only(t).Total, Is.EqualTo((21 + 20) * 3));
        }

        [Test]
        public void Blackjack_Is_CardsPlus30Chips_Times4Mult_AndEndsTheTurn()
        {
            var t = StackedTableNoTarget("A", "9", "K", "8");
            t.Apply(Deal);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Blackjack));
            Assert.That(Only(t).Total, Is.EqualTo((21 + 30) * 4));
            Assert.That(t.Phase, Is.EqualTo(TablePhase.ReadyToDeal));
        }

        [Test]
        public void Charlie_WinsAutomatically_With40ChipsTimes4Mult()
        {
            // dealer has 20 and would beat 15, but five cards without busting wins
            var t = StackedTableNoTarget("2", "K", "3", "K", "2", "4", "4");
            Play(t, Deal, Hit, Hit, Hit);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Charlie));
            Assert.That(Only(t).Total, Is.EqualTo((15 + 40) * 4));
            Assert.That(t.DealerCards.Count, Is.EqualTo(2), "the dealer doesn't draw against a Charlie");
        }

        [Test]
        public void Charlie_BeatsTwentyOne_WhenFiveCardsMake21()
        {
            var t = StackedTableNoTarget("2", "K", "3", "9", "2", "4", "10");
            Play(t, Deal, Hit, Hit, Hit);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Charlie));
        }

        [Test]
        public void DealerBust_Adds15Chips_OnTopOfTheWin()
        {
            // player 18 stands, dealer 16 hits a K and busts -> (18 + 10 + 15) x 2
            var t = StackedTableNoTarget("10", "10", "8", "6", "K");
            Play(t, Deal, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Win));
            Assert.That(Only(t).DealerBusted, Is.True);
            Assert.That(Only(t).Total, Is.EqualTo((18 + 10 + 15) * 2));
        }

        [Test]
        public void Push_ScoresCardsOnly_TimesOne()
        {
            var t = StackedTableNoTarget("10", "10", "8", "8");
            Play(t, Deal, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Push));
            Assert.That(Only(t).Total, Is.EqualTo(18));
        }

        [Test]
        public void Lose_ScoresNothing_AndUsesTheHand()
        {
            var t = StackedTableNoTarget("10", "10", "7", "9");
            Play(t, Deal, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(Only(t).Total, Is.EqualTo(0));
            Assert.That(t.HandsLeft, Is.EqualTo(Balance.StartingHands - 1));
        }

        [Test]
        public void Bust_ScoresNothing_AndTheDealerDoesntDraw()
        {
            // Stand accepts the bust instead of burning it away
            var t = new Table(Stack("10", "10", "6", "6", "K", "5"), long.MaxValue);
            Play(t, Deal, Hit, Stand);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Bust));
            Assert.That(Only(t).Total, Is.EqualTo(0));
            Assert.That(t.DealerCards.Count, Is.EqualTo(2));
        }

        [Test]
        public void DoubleDown_DoublesTheMult_OnAWin()
        {
            // 11 doubles into 21 with three cards: (21 + 20) x 3 x 2
            var t = StackedTableNoTarget("6", "10", "5", "8", "K");
            Play(t, Deal, DoubleDown);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.TwentyOne));
            Assert.That(Only(t).Mult, Is.EqualTo(6));
            Assert.That(Only(t).Total, Is.EqualTo(41 * 6));
        }

        [Test]
        public void Steps_PlayTheScoreBack_InOrder()
        {
            // win with 20 and double down: cards, the Win bonus, then the x2
            var t = StackedTableNoTarget("6", "10", "5", "9", "9");
            Play(t, Deal, DoubleDown);
            var score = Only(t);
            var steps = score.Steps;
            Assert.That(steps.Select(s => s.Kind), Is.EqualTo(new[] { ScoreSource.Hand, ScoreSource.Bonus, ScoreSource.Double }));
            Assert.That(steps[0].ChipsAdded, Is.EqualTo(20));
            Assert.That(steps[1].ChipsAdded, Is.EqualTo(10));
            Assert.That(steps[1].MultAdded, Is.EqualTo(2));
            Assert.That(steps[2].Factor, Is.EqualTo(2));
            Assert.That(steps.Last().Chips, Is.EqualTo(score.Chips));
            Assert.That(steps.Last().Mult, Is.EqualTo(score.Mult));
        }

        [Test]
        public void Steps_AddUpToTheScore_ForEveryHandInManyRuns()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var run = new Run(seed);
                var bot = new BalanceBot();
                for (int i = 0; i < 400 && !run.IsOver; i++)
                {
                    run.Apply(bot.Choose(run));
                    foreach (var score in run.ActiveTable.LastScores)
                    {
                        if (score.Steps.Count == 0)
                        {
                            Assert.That(score.Chips, Is.EqualTo(0));
                            continue;
                        }
                        Assert.That(score.Steps.Sum(s => s.ChipsAdded), Is.EqualTo(score.Chips));
                        Assert.That(score.Steps.Last().Mult, Is.EqualTo(score.Mult).Within(1e-9));
                    }
                }
            }
        }

        [Test]
        public void DoubleDown_DoesntDoubleAPush()
        {
            var t = StackedTableNoTarget("6", "10", "5", "8", "7");
            Play(t, Deal, DoubleDown);
            Assert.That(Only(t).Outcome, Is.EqualTo(Outcome.Push));
            Assert.That(Only(t).Total, Is.EqualTo(18));
        }

        [Test]
        public void ReachingTheTarget_WinsTheTable()
        {
            var t = new Table(Stack("A", "9", "K", "8"), 150);
            t.Apply(Deal);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.Won));
            Assert.That(t.LegalActions(), Is.Empty);
        }

        [Test]
        public void RunningOutOfHands_LosesTheTable()
        {
            var cards = Enumerable.Repeat(new[] { "10", "10", "7", "9" }, Balance.StartingHands).SelectMany(x => x).ToArray();
            var t = StackedTableNoTarget(cards);
            for (int i = 0; i < Balance.StartingHands; i++) Play(t, Deal, Stand);
            Assert.That(t.Phase, Is.EqualTo(TablePhase.Lost));
            Assert.That(t.HandsLeft, Is.EqualTo(0));
        }
    }
}
