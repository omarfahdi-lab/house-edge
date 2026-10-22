using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // deal order: player, dealer up, player, dealer hole, then draws
    public class BossTests
    {
        private static Boss B(string name) => Bosses.Get(name).Create();

        private static Table At(string boss, params string[] cards) =>
            new Table(Stack(cards), long.MaxValue, new TableEffect[] { B(boss) });

        private static Outcome Result(Table t) => t.LastScores.Single().Outcome;

        [Test]
        public void ThereAre12PitBosses_PlusTheSpecialOnes()
        {
            Assert.That(Bosses.PitBosses.Count, Is.EqualTo(12));
            Assert.That(Bosses.PitBosses.Select(b => b.Name).Distinct().Count(), Is.EqualTo(12));
        }

        [Test]
        public void Iceman_DealerStandsOn15()
        {
            var t = At("The Iceman", "10", "10", "6", "5", "K");
            Play(t, Deal, Stand);
            Assert.That(t.DealerCards.Count, Is.EqualTo(2));
            Assert.That(Result(t), Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void Cheat_PalmsTheFirstCardThatWouldBustHim_AndDrawsAgain()
        {
            // you stand on 18; he has 16, draws a King (bust), palms it, then draws a 4 for 20
            var t = At("The Cheat", "10", "10", "8", "6", "K", "4");
            Play(t, Deal, Stand);
            Assert.That(Result(t), Is.EqualTo(Outcome.Lose));
            Assert.That(t.DealerValue.Total, Is.EqualTo(20));
            Assert.That(t.Announcements, Has.Count.EqualTo(1));
        }

        [Test]
        public void Cheat_OnlyPalmsOnceAHand()
        {
            // the King is palmed, then a 6 busts him for real
            var t = At("The Cheat", "10", "10", "8", "6", "K", "6");
            Play(t, Deal, Stand);
            Assert.That(t.DealerValue.IsBust);
            Assert.That(Result(t), Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void Purist_AcesOnlyCountOne()
        {
            var t = At("The Purist", "A", "10", "K", "9");
            t.Apply(Deal);
            Assert.That(t.PlayerHands[0].Value.Total, Is.EqualTo(11));
            Assert.That(t.PlayerHands[0].IsBlackjack, Is.False);
        }

        [Test]
        public void Ceiling_BustsOver19()
        {
            var t = At("The Ceiling", "10", "10", "Q", "9");
            t.Apply(Deal);
            Assert.That(t.PlayerHands[0].Value.IsBust, Is.True, "20 is a bust under The Ceiling");
            Assert.That(Result(t), Is.EqualTo(Outcome.Bust), "the hand ends at once");
        }

        [Test]
        public void Sweeper_DisablesBurns()
        {
            var t = At("The Sweeper", "10", "10", "2", "9", "3");
            Play(t, Deal, Hit);
            Assert.That(t.LegalActions(), Has.No.Member(Burn));
        }

        [Test]
        public void Taxman_ChargesAGoldPerHit_AndNoGoldMeansNoHit()
        {
            var t = new Table(Stack("10", "10", "2", "9", "3", "2"), long.MaxValue, new TableEffect[] { B("The Taxman") }, gold: 1);
            Play(t, Deal, Hit);
            Assert.That(t.Gold, Is.EqualTo(0));
            Assert.That(t.LegalActions(), Has.No.Member(Hit));
        }

        [Test]
        public void Twins_DealerPlaysASecondHand_AndYouMustBeatBoth()
        {
            // you 19. dealer hand 1: 10+7 = 17. hand 2: 10+10 = 20 beats you
            var t = At("The Twins", "10", "10", "9", "7", "10", "10");
            Play(t, Deal, Stand);
            Assert.That(t.SecondDealerCards.Count, Is.EqualTo(2));
            Assert.That(Result(t), Is.EqualTo(Outcome.Lose));

            var beatBoth = At("The Twins", "10", "10", "9", "7", "10", "8");
            Play(beatBoth, Deal, Stand);
            Assert.That(Result(beatBoth), Is.EqualTo(Outcome.Win));
        }

        [Test]
        public void Silencer_DisablesTheLeftmostVice()
        {
            var gambler = new Gambler();
            var first = ViceCatalog.Get("LuckyPenny").Create();
            var second = ViceCatalog.Get("EvenMoney").Create();
            gambler.Vices.Add(first);
            gambler.Vices.Add(second);
            new Table(gambler, Stack("10", "10"), new GameRandom(1), 100, new TableEffect[] { B("The Silencer"), first, second });
            Assert.That(first.Disabled, Is.True);
            Assert.That(second.Disabled, Is.False);
        }

        [Test]
        public void CardMechanic_SwapsYourHighestCardWithHisFaceUpCard()
        {
            // you K + 7 stand. he takes your K for his 5: you have 5 + 7 = 12, he has K + 6 = 16, draws a 2 -> 18
            var t = At("The Card Mechanic", "K", "5", "7", "6", "2");
            Play(t, Deal, Stand);
            Assert.That(t.PlayerHands[0].Cards.Select(c => c.ToString()), Is.EqualTo(new[] { "5♥", "7♣" }));
            Assert.That(t.DealerCards[0].ToString(), Is.EqualTo("K♠"));
            Assert.That(Result(t), Is.EqualTo(Outcome.Lose));
        }

        [Test]
        public void Hoarder_Adds3DeadCardsToYourDeck_ForTheRun()
        {
            var gambler = new Gambler();
            int before = gambler.Deck.Count;
            var t = new Table(gambler, new GameRandom(1), 100, new TableEffect[] { B("The Hoarder") });
            Assert.That(gambler.Deck.Count, Is.EqualTo(before + 3));
            Assert.That(gambler.Deck.Count(c => c.Curse == Curse.DeadCard), Is.EqualTo(3));
            Assert.That(t.CardsInShoe, Is.EqualTo(before + 3));
        }

        [Test]
        public void PitBoss_Adds10HeatEveryHand()
        {
            var t = At("The Pit Boss", "10", "10", "9", "8", "10", "10", "9", "8");
            Play(t, Deal, Stand, Deal);
            Assert.That(t.Gambler.Heat, Is.EqualTo(20));
        }

        [Test]
        public void EyeInTheSky_DisablesCheaterVices_AndDoublesTheTarget()
        {
            var eye = Bosses.EyeInTheSky.Create();
            Assert.That(eye.TargetFactor, Is.EqualTo(2));
        }

        [Test]
        public void Enforcer_Takes3GoldForEveryHandYouDontWin()
        {
            var t = new Table(Stack("10", "10", "7", "9"), long.MaxValue, new TableEffect[] { Bosses.Enforcer.Create() }, gold: 10);
            Play(t, Deal, Stand);
            Assert.That(t.Gold, Is.EqualTo(7));
        }

        [Test]
        public void Enforcer_TakesAVice_WhenYouHaveNoGold()
        {
            var gambler = new Gambler(gold: 0);
            var vice = ViceCatalog.Get("LuckyPenny").Create();
            gambler.Vices.Add(vice);
            var t = new Table(gambler, Stack("10", "10", "7", "9"), new GameRandom(1), long.MaxValue,
                new TableEffect[] { Bosses.Enforcer.Create(), vice });
            Play(t, Deal, Stand);
            Assert.That(gambler.Vices, Is.Empty);
        }

        [Test]
        public void TheHouse_HasExtraHands_AndChangesItsTwistEvery2Hands()
        {
            var house = (TheHouse)Bosses.TheHouse.Create();
            var cards = Enumerable.Repeat(new[] { "10", "10", "9", "8" }, 6).SelectMany(x => x).ToArray();
            var t = new Table(Stack(cards), long.MaxValue, new TableEffect[] { house });
            Assert.That(t.HandsLeft, Is.EqualTo(Balance.StartingHands + Balance.TheHouseExtraHands));

            t.Apply(Deal);
            var first = house.CurrentTwist;
            Assert.That(first, Is.Not.Null);
            FinishHand(t);
            t.Apply(Deal);
            Assert.That(house.CurrentTwist, Is.SameAs(first), "same twist for 2 hands");
            FinishHand(t);
            t.Apply(Deal);
            Assert.That(house.CurrentTwist, Is.Not.SameAs(first), "a new twist every 2 hands");
        }

        private static void FinishHand(Table t)
        {
            while (t.Phase != TablePhase.ReadyToDeal && !t.IsOver)
                t.Apply(t.LegalActions().Contains(DeclineInsurance) ? DeclineInsurance : Stand);
        }
    }
}
