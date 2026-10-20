using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;

namespace HouseEdge.Tests
{
    public class ShopTests
    {
        [TestCase(0, 0)]
        [TestCase(4, 0)]
        [TestCase(5, 1)]
        [TestCase(14, 2)]
        [TestCase(25, 5)]
        [TestCase(100, 5)]
        public void Interest_IsOnePer5Gold_CappedAt5(int gold, int expected)
        {
            Assert.That(Balance.Interest(gold, Balance.InterestCap), Is.EqualTo(expected));
        }

        [Test]
        public void BeatingATable_PaysBasePlusUnusedHandsPlusInterest()
        {
            var run = RunTests.FindRunThatBeatsFirstTable();
            var payout = run.LastPayout;
            Assert.That(payout.Base, Is.EqualTo(3), "Low Table pays 3");
            Assert.That(payout.UnusedHands, Is.EqualTo(run.Table.HandsLeft));
            Assert.That(payout.Total, Is.EqualTo(payout.Base + payout.UnusedHands + payout.Interest));
        }

        [Test]
        public void TableBasePay_Is3_4_5()
        {
            Assert.That(Balance.TablePayout(TableKind.Low), Is.EqualTo(3));
            Assert.That(Balance.TablePayout(TableKind.High), Is.EqualTo(4));
            Assert.That(Balance.TablePayout(TableKind.Boss), Is.EqualTo(5));
        }

        [Test]
        public void TheShop_Offers3DifferentVices_2CocktailsAndAComp()
        {
            var run = RunTests.FindRunThatBeatsFirstTable();
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Shop));
            var vices = run.Shop.Offers.Where(o => o.Kind == OfferKind.Vice).ToList();
            Assert.That(vices.Count, Is.EqualTo(Balance.ShopViceOffers));
            Assert.That(vices.Select(o => o.Vice.Id).Distinct().Count(), Is.EqualTo(Balance.ShopViceOffers));
            foreach (var offer in vices)
                Assert.That(offer.BasePrice, Is.EqualTo(Balance.VicePrice(offer.Vice.Rarity)));
            Assert.That(run.Shop.Offers.Count(o => o.Kind == OfferKind.Cocktail), Is.EqualTo(Balance.ShopCocktailOffers));
            Assert.That(run.Shop.Offers.Count(o => o.Kind == OfferKind.Comp), Is.EqualTo(Balance.ShopCompOffers));
        }

        [Test]
        public void BuyingAVice_CostsGold_AndAddsItToTheRight()
        {
            var run = ShopWithGold(30);
            var offer = run.Shop.Offers[0];
            int gold = run.Gambler.Gold;
            run.Apply(RunInput.Buy(0));
            Assert.That(run.Gambler.Gold, Is.EqualTo(gold - run.PriceOf(offer)));
            Assert.That(run.Gambler.Vices.Last().Definition, Is.SameAs(offer.Vice));
            Assert.That(run.IsLegal(RunInput.Buy(0)), Is.False, "sold out");
        }

        [Test]
        public void YouCantBuy_WithoutGoldOrAFreeSlot()
        {
            var poor = ShopWithGold(0);
            Assert.That(poor.IsLegal(RunInput.Buy(0)), Is.False);

            var full = ShopWithGold(100);
            full.Gambler.ViceSlots = 0;
            Assert.That(full.IsLegal(RunInput.Buy(0)), Is.False);
        }

        [Test]
        public void SellingAVice_PaysHalfItsPrice()
        {
            var run = ShopWithGold(30);
            run.Apply(RunInput.Buy(0));
            var vice = run.Gambler.Vices[0];
            int gold = run.Gambler.Gold;
            run.Apply(RunInput.SellVice(0));
            Assert.That(run.Gambler.Vices, Is.Empty);
            Assert.That(run.Gambler.Gold, Is.EqualTo(gold + Balance.VicePrice(vice.Definition.Rarity) / 2));
        }

        [Test]
        public void MovingAVice_ChangesTheTriggerOrder()
        {
            var run = ShopWithGold(30);
            run.Apply(RunInput.Buy(0));
            run.Apply(RunInput.Buy(1));
            var first = run.Gambler.Vices[0];
            run.Apply(RunInput.MoveVice(0, 1));
            Assert.That(run.Gambler.Vices[1], Is.SameAs(first));
        }

        [Test]
        public void Reroll_CostsMoreEachTime_AndRestocks()
        {
            var run = ShopWithGold(30);
            int cost = run.Shop.RerollCost;
            run.Apply(RunInput.Reroll());
            Assert.That(run.Gambler.Gold, Is.EqualTo(30 - cost));
            Assert.That(run.Shop.RerollCost, Is.EqualTo(cost + 1));
            Assert.That(run.Shop.Offers.All(o => !o.Sold), Is.True);
            Assert.That(run.RerollCost, Is.EqualTo(cost + 1));
        }

        [Test]
        public void TheShop_NeverOffersAViceYouOwn()
        {
            var run = ShopWithGold(500);
            for (int i = 0; i < 20; i++)
            {
                if (run.IsLegal(RunInput.Buy(0)) && run.Gambler.HasFreeViceSlot) run.Apply(RunInput.Buy(0));
                var owned = run.Gambler.Vices.Select(v => v.Definition.Id).ToList();
                Assert.That(run.Shop.Offers.Where(o => !o.Sold && o.Kind == OfferKind.Vice).Select(o => o.Vice.Id).Intersect(owned), Is.Empty);
                run.Apply(RunInput.Reroll());
            }
        }

        [Test]
        public void BoughtVices_PlayAtTheNextTable()
        {
            var run = ShopWithGold(30);
            run.Apply(RunInput.Buy(0));
            run.Apply(RunInput.LeaveShop());
            Assert.That(run.Table.Effects, Has.Member(run.Gambler.Vices[0]));
        }

        [Test]
        public void ShopInputs_ReplayExactly()
        {
            var run = ShopWithGold(0);
            // the seed's own gold, not the test's top-up, so replay can match
            var replay = Run.Replay(run.Seed, run.Inputs);
            Assert.That(replay.Shop.Offers.Select(o => o.Name), Is.EqualTo(run.Shop.Offers.Select(o => o.Name)));
        }

        // a run sitting in its first shop with exactly this much gold
        private static Run ShopWithGold(int gold)
        {
            var run = RunTests.FindRunThatBeatsFirstTable();
            run.Gambler.LoseGold(run.Gambler.Gold);
            run.Gambler.GainGold(gold);
            return run;
        }
    }
}
