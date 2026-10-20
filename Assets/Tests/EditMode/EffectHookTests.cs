using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    public class EffectHookTests
    {
        // like Lucky Penny: +4 Mult on any win
        private sealed class PlusMultOnWin : TableEffect
        {
            public override void OnHandScored(Table table, HandScore score)
            {
                if (score.Outcome.IsWin()) score.AddMult(4);
            }
        }

        private sealed class TimesTwoMult : TableEffect
        {
            public override void OnHandScored(Table table, HandScore score) => score.MultiplyMult(2);
        }

        private sealed class Recorder : TableEffect
        {
            public readonly List<string> Calls = new List<string>();
            public override void OnCardDrawn(Table table, Card card, bool toDealer) => Calls.Add((toDealer ? "dealer " : "player ") + card);
            public override void OnHit(Table table, PlayerHand hand) => Calls.Add("hit");
            public override void OnStand(Table table, PlayerHand hand) => Calls.Add("stand");
            public override void OnDealerBust(Table table) => Calls.Add("dealer bust");
            public override void OnHandScored(Table table, HandScore score) => Calls.Add("scored " + score.Outcome);
            public override void OnTableEnd(Table table, bool won) => Calls.Add(won ? "table won" : "table lost");
        }

        [Test]
        public void ScoringEffect_ChangesTheScore()
        {
            // DESIGN.md: win with 20 is 30 x 2. with +4 Mult it's 30 x 6
            var t = new Table(Stack("K", "9", "Q", "9"), long.MaxValue, new[] { new PlusMultOnWin() });
            Play(t, Deal, Stand);
            Assert.That(t.LastScores.Single().Total, Is.EqualTo(30 * 6));
        }

        [Test]
        public void Effects_TriggerLeftToRight()
        {
            // (2 + 4) x 2 = 12 one way, 2 x 2 + 4 = 8 the other
            var addThenTimes = new Table(Stack("K", "9", "Q", "9"), long.MaxValue, new TableEffect[] { new PlusMultOnWin(), new TimesTwoMult() });
            var timesThenAdd = new Table(Stack("K", "9", "Q", "9"), long.MaxValue, new TableEffect[] { new TimesTwoMult(), new PlusMultOnWin() });
            Play(addThenTimes, Deal, Stand);
            Play(timesThenAdd, Deal, Stand);
            Assert.That(addThenTimes.LastScores.Single().Mult, Is.EqualTo(12));
            Assert.That(timesThenAdd.LastScores.Single().Mult, Is.EqualTo(8));
        }

        [Test]
        public void EveryHook_FiresAtTheRightMoment()
        {
            // player 10+6 hits a 2 and stands on 18, dealer 10+6 hits a K and busts. 1 Hand so the table ends
            var recorder = new Recorder();
            var t = new Table(Stack("10", "10", "6", "6", "2", "K"), 1, new[] { recorder });
            Play(t, Deal, Hit, Stand);
            Assert.That(recorder.Calls, Is.EqualTo(new[]
            {
                "player 10♠", "dealer 10♠", "player 6♠", "dealer 6♠",
                "player 2♠", "hit",
                "stand",
                "dealer K♠", "dealer bust",
                "scored Win",
                "table won",
            }));
        }
    }
}
