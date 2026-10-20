using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    public class ShoeTests
    {
        [Test]
        public void StandardDeck_Has52UniqueCards()
        {
            var deck = Shoe.StandardDeck();
            Assert.That(deck.Count, Is.EqualTo(52));
            Assert.That(deck.Select(c => c.ToString()).Distinct().Count(), Is.EqualTo(52));
        }

        [Test]
        public void SameSeed_ShufflesTheSameOrder()
        {
            Assert.That(DrawAll(42), Is.EqualTo(DrawAll(42)));
        }

        [Test]
        public void DifferentSeeds_ShuffleDifferentOrders()
        {
            Assert.That(DrawAll(1), Is.Not.EqualTo(DrawAll(2)));
        }

        [Test]
        public void Shuffle_KeepsEveryCard()
        {
            Assert.That(DrawAll(7).OrderBy(s => s), Is.EqualTo(Shoe.StandardDeck().Select(c => c.ToString()).OrderBy(s => s)));
        }

        [Test]
        public void Stacked_DrawsInTheGivenOrder()
        {
            var shoe = Stack("A", "K", "2");
            Assert.That(shoe.Draw().ToString(), Is.EqualTo("A♠"));
            Assert.That(shoe.Draw().ToString(), Is.EqualTo("K♠"));
            Assert.That(shoe.Draw().ToString(), Is.EqualTo("2♠"));
        }

        [Test]
        public void WhenTheDrawPileRunsOut_TheDiscardsAreShuffledBackIn()
        {
            var shoe = Stack("A");
            shoe.Discard(Cards("2", "3"));
            shoe.Draw();
            Assert.That(shoe.Count, Is.EqualTo(0));

            var drawn = new[] { shoe.Draw().ToString(), shoe.Draw().ToString() };
            Assert.That(drawn, Is.EquivalentTo(new[] { "2♠", "3♠" }));
            Assert.That(shoe.DiscardCount, Is.EqualTo(0));
        }

        private static string[] DrawAll(int seed)
        {
            var shoe = Shoe.Shuffled(Shoe.StandardDeck(), new GameRandom(seed));
            return Enumerable.Range(0, 52).Select(_ => shoe.Draw().ToString()).ToArray();
        }
    }
}
