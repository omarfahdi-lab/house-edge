using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    public class HandValueTests
    {
        [Test]
        public void FaceCardsCountTen_NumberCardsCountTheirRank()
        {
            Assert.That(Value("K", "Q").Total, Is.EqualTo(20));
            Assert.That(Value("J", "9").Total, Is.EqualTo(19));
            Assert.That(Value("2", "3", "4").Total, Is.EqualTo(9));
        }

        [Test]
        public void AceCountsEleven_WhenItFits_AndTheHandIsSoft()
        {
            var v = Value("A", "6");
            Assert.That(v.Total, Is.EqualTo(17));
            Assert.That(v.IsSoft, Is.True);
        }

        [Test]
        public void AceDropsToOne_WhenElevenWouldBust_AndTheHandTurnsHard()
        {
            var v = Value("A", "6", "9");
            Assert.That(v.Total, Is.EqualTo(16));
            Assert.That(v.IsSoft, Is.False);
        }

        [TestCase(new[] { "A", "A" }, 12, true)]
        [TestCase(new[] { "A", "A", "9" }, 21, true)]
        [TestCase(new[] { "A", "A", "A", "8" }, 21, true)]
        [TestCase(new[] { "A", "A", "A", "A" }, 14, true)]
        [TestCase(new[] { "A", "A", "K" }, 12, false)]
        [TestCase(new[] { "A", "A", "A", "A", "K", "8" }, 22, false)]
        public void MultipleAces_OnlyOneCanCountEleven(string[] cards, int total, bool soft)
        {
            var v = Value(cards);
            Assert.That(v.Total, Is.EqualTo(total));
            Assert.That(v.IsSoft, Is.EqualTo(soft));
        }

        [Test]
        public void Bust_IsOver21()
        {
            Assert.That(Value("K", "Q", "2").IsBust, Is.True);
            Assert.That(Value("K", "Q", "A").IsBust, Is.False);
        }

        [Test]
        public void TwoCard21_IsAceAndTenValue()
        {
            Assert.That(Value("A", "K").IsTwoCard21, Is.True);
            Assert.That(Value("10", "A").IsTwoCard21, Is.True);
            Assert.That(Value("7", "7", "7").IsTwoCard21, Is.False, "three cards");
        }

        [Test]
        public void Charlie_IsFiveCardsWithoutBusting()
        {
            Assert.That(Value("2", "3", "2", "4", "5").IsCharlie, Is.True);
            Assert.That(Value("2", "3", "2", "4").IsCharlie, Is.False, "four cards");
            Assert.That(Value("2", "3", "2", "K", "9").IsCharlie, Is.False, "busted");
        }
    }
}
