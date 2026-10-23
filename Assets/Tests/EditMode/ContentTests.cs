using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    public class PackTests
    {
        private static int PackOffer(Run run) => run.Shop.Offers.ToList().FindIndex(o => o.Kind == OfferKind.Pack);

        // the first shop, rerolled until it offers this kind of pack, with gold to buy it
        private static Run ShopWithPack(PackKind kind, int firstSeed = 1)
        {
            var run = RunTests.FindRunThatBeatsFirstTableFrom(firstSeed);
            Assert.That(run, Is.Not.Null);
            run.Gambler.GainGold(5000);
            for (int i = 0; i < 200 && run.Shop.Offers[PackOffer(run)].Pack != kind; i++) run.Apply(RunInput.Reroll());
            Assert.That(run.Shop.Offers[PackOffer(run)].Pack, Is.EqualTo(kind), $"no {kind} after 200 rerolls");
            return run;
        }

        private static Run OpenPack(PackKind kind, int firstSeed = 1)
        {
            var run = ShopWithPack(kind, firstSeed);
            run.Apply(RunInput.Buy(PackOffer(run)));
            return run;
        }

        [Test]
        public void EveryShop_HasAPackOffer()
        {
            var run = RunTests.FindRunThatBeatsFirstTable();
            run.Gambler.GainGold(1000);
            for (int i = 0; i < 20; i++)
            {
                Assert.That(run.Shop.Offers.Count(o => o.Kind == OfferKind.Pack), Is.EqualTo(Balance.ShopPackOffers));
                run.Apply(RunInput.Reroll());
            }
        }

        [Test]
        public void BuyingAPack_OpensItWithThreeCards()
        {
            var run = ShopWithPack(PackKind.Card);
            int gold = run.Gambler.Gold;
            var offer = run.Shop.Offers[PackOffer(run)];
            run.Apply(RunInput.Buy(PackOffer(run)));
            Assert.That(run.Phase, Is.EqualTo(RunPhase.OpeningPack));
            Assert.That(run.PackCards.Count, Is.EqualTo(Balance.PackCards));
            Assert.That(run.Gambler.Gold, Is.EqualTo(gold - run.PriceOf(offer)));
            Assert.That(offer.Sold, Is.True);
        }

        [Test]
        public void PickingACard_AddsExactlyThatCard_AndGoesBackToTheShop()
        {
            var run = OpenPack(PackKind.Card);
            int deck = run.Gambler.Deck.Count;
            var picked = run.PackCards[1];
            run.Apply(RunInput.PickPackCard(1));
            Assert.That(run.Gambler.Deck.Count, Is.EqualTo(deck + 1));
            Assert.That(run.Gambler.Deck, Has.Member(picked));
            Assert.That(run.Gambler.Deck.Last(), Is.SameAs(picked));
            Assert.That(run.PackCards, Is.Empty);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Shop));
        }

        [Test]
        public void SkippingAPack_AddsNothing()
        {
            var run = OpenPack(PackKind.Lucky);
            int deck = run.Gambler.Deck.Count;
            run.Apply(RunInput.SkipPack());
            Assert.That(run.Gambler.Deck.Count, Is.EqualTo(deck));
            Assert.That(run.PackCards, Is.Empty);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Shop));
        }

        [Test]
        public void WhileAPackIsOpen_OnlyPickingOrSkippingIsLegal()
        {
            var run = OpenPack(PackKind.Court);
            for (int i = 0; i < run.PackCards.Count; i++) Assert.That(run.IsLegal(RunInput.PickPackCard(i)), Is.True);
            Assert.That(run.IsLegal(RunInput.PickPackCard(-1)), Is.False);
            Assert.That(run.IsLegal(RunInput.PickPackCard(run.PackCards.Count)), Is.False);
            Assert.That(run.IsLegal(RunInput.SkipPack()), Is.True);
            Assert.That(run.IsLegal(RunInput.LeaveShop()), Is.False);
            Assert.That(run.IsLegal(RunInput.Reroll()), Is.False);
            Assert.That(run.IsLegal(RunInput.Buy(0)), Is.False);
            Assert.That(run.IsLegal(RunInput.SellVice(0)), Is.False);
            Assert.That(run.IsLegal(RunInput.Table(Deal)), Is.False);
            Assert.That(run.IsLegal(RunInput.ChooseRoom(0)), Is.False);
        }

        [Test]
        public void SkipAndPick_AreIllegalWithNoPackOpen()
        {
            var run = RunTests.FindRunThatBeatsFirstTable();
            Assert.That(run.IsLegal(RunInput.SkipPack()), Is.False);
            Assert.That(run.IsLegal(RunInput.PickPackCard(0)), Is.False);
        }

        [Test]
        public void SuitPacks_AreOneSuit_WithOneWild()
        {
            for (int seed = 1; seed <= 41; seed += 10)
            {
                var run = OpenPack(PackKind.Suit, seed);
                Assert.That(run.PackCards.Select(c => c.Suit).Distinct().Count(), Is.EqualTo(1), string.Join(",", run.PackCards));
                Assert.That(run.PackCards.Count(c => c.Enhancement == Enhancement.Wild), Is.EqualTo(1));
            }
        }

        [Test]
        public void CourtPacks_AreAllFaceCards()
        {
            for (int seed = 1; seed <= 41; seed += 10)
            {
                var run = OpenPack(PackKind.Court, seed);
                Assert.That(run.PackCards.All(c => c.Rank == Rank.Jack || c.Rank == Rank.Queen || c.Rank == Rank.King),
                    string.Join(",", run.PackCards));
                Assert.That(run.PackCards.Count(c => c.Enhancement != Enhancement.None || c.Stamp != Stamp.None), Is.GreaterThanOrEqualTo(1));
            }
        }

        [Test]
        public void LuckyPacks_AreAllEnhancedOrStamped()
        {
            for (int seed = 1; seed <= 41; seed += 10)
            {
                var run = OpenPack(PackKind.Lucky, seed);
                Assert.That(run.PackCards.All(c => c.Enhancement != Enhancement.None || c.Stamp != Stamp.None),
                    string.Join(",", run.PackCards.Select(c => $"{c} {c.Enhancement} {c.Stamp}")));
            }
        }

        [Test]
        public void TheSameSeed_OpensTheSamePack()
        {
            // a run that can afford the pack with the gold it earned, so the inputs alone replay it
            for (int seed = 1; seed < 500; seed++)
            {
                var run = RunTests.FindRunThatBeatsFirstTableFrom(seed);
                Assert.That(run, Is.Not.Null);
                seed = run.Seed;
                int pack = PackOffer(run);
                if (!run.IsLegal(RunInput.Buy(pack))) continue;
                run.Apply(RunInput.Buy(pack));
                var opened = run.PackCards.Select(Describe).ToList();
                run.Apply(RunInput.PickPackCard(0));
                run.Apply(RunInput.LeaveShop());

                var replay = Run.Replay(run.Seed, run.Inputs);
                Assert.That(replay.Gambler.Deck.Select(Describe), Is.EqualTo(run.Gambler.Deck.Select(Describe)));
                Assert.That(replay.Gambler.Deck.Last().ToString(), Is.EqualTo(opened[0].Split(' ')[0]));
                Assert.That(replay.Phase, Is.EqualTo(run.Phase));
                Assert.That(replay.Gambler.Gold, Is.EqualTo(run.Gambler.Gold));

                // and stopping the replay at the open pack shows the same three cards
                var upToOpen = Run.Replay(run.Seed, run.Inputs.Take(run.Inputs.Count - 2));
                Assert.That(upToOpen.Phase, Is.EqualTo(RunPhase.OpeningPack));
                Assert.That(upToOpen.PackCards.Select(Describe), Is.EqualTo(opened));
                return;
            }
            Assert.Fail("no seed could afford a pack in its first shop");
        }

        private static string Describe(Card c) => $"{c} {c.Enhancement} {c.Stamp}";
    }

    public class WildCardTests
    {
        [Test]
        public void AWildCard_CompletesAFlush()
        {
            var hand = new List<Card> { C("7h"), C("9h"), C("Ks", Enhancement.Wild) };
            Assert.That(Combos.Find(hand), Has.Member(Combo.Flush));
            Assert.That(Combos.Find(Cards("7h", "9h", "Ks")), Has.No.Member(Combo.Flush));
        }

        [Test]
        public void AWildCard_MakesATwoCardHandSuited()
        {
            var hand = new List<Card> { C("7h"), C("Kc", Enhancement.Wild) };
            Assert.That(Combos.Find(hand), Has.Member(Combo.Suited));
            Assert.That(Combos.Find(Cards("7h", "Kc")), Has.No.Member(Combo.Suited));
        }

        [Test]
        public void AWildCard_DoesNotMakeTwoOtherSuitsMatch()
        {
            var hand = new List<Card> { C("7h"), C("9s"), C("Kc", Enhancement.Wild) };
            Assert.That(Combos.Find(hand), Has.No.Member(Combo.Flush));
        }
    }

    public class EliteTests
    {
        // a run that just beat the Low Table, with an Elite among its first stops
        internal static Run OnTheMapWithElite(int firstSeed = 1)
        {
            for (int seed = firstSeed; seed < firstSeed + 1000; seed++)
            {
                var run = RunTests.FindRunOnTheMapFrom(seed);
                if (run == null) break;
                seed = run.Seed;
                if (run.RoomChoices.Contains(RoomKind.Elite)) return run;
            }
            Assert.Fail("no seed offered an Elite");
            return null;
        }

        private static int Elite(Run run) => run.RoomChoices.ToList().IndexOf(RoomKind.Elite);

        // plays the Elite out; returns the gold held just before the last input
        private static int PlayElite(Run run, bool alwaysStand)
        {
            int gold = run.Gambler.Gold;
            while (run.Phase == RunPhase.AtTable && run.AtElite)
            {
                gold = run.Gambler.Gold;
                RunTests.PlayOne(run, alwaysStand);
            }
            return gold;
        }

        [Test]
        public void AnElite_IsAHardTableUnderAnotherPitBoss()
        {
            for (int firstSeed = 1, found = 0; found < 5; found++)
            {
                var run = OnTheMapWithElite(firstSeed);
                firstSeed = run.Seed + 1;
                var next = run.NextStage.stage;
                run.Apply(RunInput.ChooseRoom(Elite(run)));
                Assert.That(run.AtElite, Is.True);
                Assert.That(run.Phase, Is.EqualTo(RunPhase.AtTable));
                Assert.That(run.Boss, Is.Not.Null);
                Assert.That(Bosses.PitBosses, Has.Member(run.Boss.Definition));
                Assert.That(run.Boss.Definition, Is.Not.SameAs(run.FloorBoss));
                Assert.That(run.Table.Effects, Has.Member(run.Boss));
                Assert.That(run.Table.HandsLeft, Is.EqualTo(Balance.StartingHands - Balance.EliteHandsLess));
                Assert.That(run.Table.Target,
                    Is.EqualTo((long)System.Math.Round(run.TargetFor(next) * run.Boss.TargetFactor * Balance.EliteTargetFactor)));
                Assert.That(run.BossesFaced, Has.Member(run.Boss.Name));
                Assert.That(run.Stage, Is.EqualTo(TableKind.Low), "an Elite doesn't move you along the floor");
            }
        }

        [Test]
        public void LosingAnElite_HalvesYourGold_ButTheRunGoesOn()
        {
            for (int firstSeed = 1, tries = 0; tries < 40; tries++)
            {
                var run = OnTheMapWithElite(firstSeed);
                firstSeed = run.Seed + 1;
                run.Apply(RunInput.ChooseRoom(Elite(run)));
                int gold = PlayElite(run, alwaysStand: true);
                if (run.Table.Phase != TablePhase.Lost) continue;

                Assert.That(run.IsOver, Is.False);
                Assert.That(run.AtElite, Is.False);
                Assert.That(run.Phase, Is.EqualTo(RunPhase.RoomChoice), "back on the map for the next stop");
                Assert.That(run.Stage, Is.EqualTo(TableKind.Low));
                Assert.That(run.Gambler.Gold, Is.EqualTo(gold - gold / 2));
                Assert.That(run.EliteResult, Is.Not.Null);
                Assert.That(run.EliteResult, Does.Contain($"{gold / 2} gold"));
                Assert.That(run.NextStops.Count, Is.GreaterThan(0));
                Assert.That(run.NextStops.All(s => s.Row == 1), Is.True);

                // the walk goes on to the High Table as normal
                AutoPlay.ToNextTable(run);
                Assert.That(run.Phase, Is.EqualTo(RunPhase.AtTable));
                Assert.That(run.Stage, Is.EqualTo(TableKind.High));
                Assert.That(run.AtElite, Is.False);
                return;
            }
            Assert.Fail("no Elite was lost");
        }

        [Test]
        public void WinningAnElite_OffersAReward_ThenBackToTheMap()
        {
            foreach (bool pick in new[] { false, true })
            {
                var run = WonElite();
                Assert.That(run.Phase, Is.EqualTo(RunPhase.BossReward));
                Assert.That(run.Rewards.Count, Is.GreaterThan(0));
                Assert.That(run.EliteResult, Is.Null);
                Assert.That(run.AtElite, Is.False);
                int vices = run.Gambler.Vices.Count;
                if (pick)
                {
                    var reward = run.Rewards[0];
                    run.Apply(RunInput.PickReward(0));
                    Assert.That(run.Gambler.Vices.Count, Is.EqualTo(vices + 1));
                    Assert.That(run.Gambler.Vices.Last().Definition, Is.SameAs(reward));
                }
                else run.Apply(RunInput.SkipReward());
                Assert.That(run.Phase, Is.EqualTo(RunPhase.RoomChoice), "an Elite's reward leads back onto the map, not to a shop");
                Assert.That(run.Stage, Is.EqualTo(TableKind.Low));
            }
        }

        [Test]
        public void WinningAnElite_PaysLikeAHighTable()
        {
            var run = WonElite();
            Assert.That(run.LastPayout.Base, Is.EqualTo(Balance.TablePayout(TableKind.High)));
            Assert.That(run.TablesBeaten, Is.EqualTo(2));
        }

        // an Elite won by playing it out. the gambler gets strong Vices first so a win is easy to find
        private static Run WonElite()
        {
            for (int firstSeed = 1, tries = 0; tries < 60; tries++)
            {
                var run = OnTheMapWithElite(firstSeed);
                firstSeed = run.Seed + 1;
                foreach (var id in new[] { "LuckyPenny", "HighRoller", "Whale", "NaturalBorn" })
                    run.Gambler.Vices.Add(ViceCatalog.Get(id).Create());
                run.Gambler.GainGold(60);
                run.Apply(RunInput.ChooseRoom(Elite(run)));
                PlayElite(run, alwaysStand: false);
                if (run.Table.Phase == TablePhase.Won) return run;
            }
            Assert.Fail("no Elite was won");
            return null;
        }
    }

    public class MysteryTests
    {
        private static int Choice(Run run, int arg) => run.RoomOptions().FindIndex(o => o.Kind == RoomActionKind.EventChoice && o.Arg == arg);

        // a run standing at a "?" stop that rolled this event
        internal static Run At(EventKind kind, int gold = 10)
        {
            int firstSeed = 1;
            for (int i = 0; i < 300; i++)
            {
                var run = RoomRuns.In(RoomKind.Mystery, gold, firstSeed);
                if (run.Room.Event == kind) return run;
                firstSeed = run.Seed + 1;
            }
            Assert.Fail($"no seed rolled {kind}");
            return null;
        }

        [Test]
        public void AMysteryStop_RollsAnEvent_AndOffersItsChoices()
        {
            var run = RoomRuns.In(RoomKind.Mystery);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InRoom));
            Assert.That(run.Room.Kind, Is.EqualTo(RoomKind.Mystery));
            Assert.That(Events.All, Has.Member(run.Room.Event));
            Assert.That(run.Room.Name, Is.EqualTo(Events.Title(run.Room.Event)));
            Assert.That(run.Room.Description, Is.EqualTo(Events.Scene(run.Room.Event)));
            var options = run.RoomOptions();
            var choices = Events.Choices(run.Room.Event);
            Assert.That(options.Select(o => o.Label), Is.EqualTo(choices.Select(c => c.label)));
            Assert.That(options.All(o => o.Kind == RoomActionKind.EventChoice), Is.True);
            Assert.That(options.Select(o => o.Arg), Is.EqualTo(Enumerable.Range(0, choices.Count)));
        }

        [Test]
        public void EveryEvent_CanShowUp()
        {
            foreach (var kind in Events.All) Assert.That(At(kind).Room.Event, Is.EqualTo(kind));
        }

        [Test]
        public void AChoice_CanOnlyBeTakenOnce()
        {
            var run = At(EventKind.LuckyChip);
            run.Apply(RunInput.RoomOption(Choice(run, 1)));
            Assert.That(run.Room.Used, Is.True);
            Assert.That(run.Room.Result, Is.Not.Null.And.Not.Empty);
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 0))), Is.False);
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 1))), Is.False);
            Assert.That(run.IsLegal(RunInput.LeaveRoom()), Is.True);
            run.Apply(RunInput.LeaveRoom());
            Assert.That(run.Phase, Is.EqualTo(RunPhase.AtTable), "the stop before the High Table");
            Assert.That(run.Stage, Is.EqualTo(TableKind.High));
        }

        [Test]
        public void LuckyChip_Pocketed_GivesALuckyVice_AndHeat()
        {
            var run = At(EventKind.LuckyChip);
            int heat = run.Gambler.Heat;
            run.Apply(RunInput.RoomOption(Choice(run, 0)));
            Assert.That(run.Gambler.Heat, Is.EqualTo(heat + 20));
            Assert.That(run.Gambler.Vices.Count, Is.EqualTo(1));
            var vice = run.Gambler.Vices[0].Definition;
            Assert.That(vice.Archetype, Is.EqualTo(Archetype.Lucky));
            Assert.That(vice.IsCursed, Is.False);
            Assert.That(run.SeenVices, Has.Member(vice.Id));
        }

        [Test]
        public void LuckyChip_TurnedIn_Pays8()
        {
            var run = At(EventKind.LuckyChip, gold: 10);
            run.Apply(RunInput.RoomOption(Choice(run, 1)));
            Assert.That(run.Gambler.Gold, Is.EqualTo(18));
            Assert.That(run.Gambler.Vices, Is.Empty);
        }

        [Test]
        public void LuckyChip_NeedsAViceSlot()
        {
            var run = At(EventKind.LuckyChip);
            FillViceSlots(run);
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 0))), Is.False);
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 1))), Is.True);
        }

        [Test]
        public void Shredder_RemovesTheThreeLowestCards()
        {
            var run = At(EventKind.Shredder);
            int deck = run.Gambler.Deck.Count;
            int twos = run.Gambler.Deck.Count(c => c.Rank == Rank.Two);
            run.Apply(RunInput.RoomOption(Choice(run, 0)));
            Assert.That(run.Gambler.Deck.Count, Is.EqualTo(deck - 3));
            Assert.That(run.Gambler.Deck.Count(c => c.Rank == Rank.Two), Is.EqualTo(twos - 3), "a standard deck's lowest cards are its 2s");
        }

        [Test]
        public void Shredder_WontTakeTheDeckBelowTheMinimum()
        {
            var run = At(EventKind.Shredder);
            run.Gambler.Deck.RemoveRange(0, run.Gambler.Deck.Count - (Balance.MinDeckSize + 2));
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 0))), Is.False);
            run.Gambler.Deck.Add(C("9s"));
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 0))), Is.True);
        }

        [Test]
        public void PhotoBooth_CopiesYourBestCard()
        {
            var run = At(EventKind.PhotoBooth);
            int deck = run.Gambler.Deck.Count;
            int aces = run.Gambler.Deck.Count(c => c.Rank == Rank.Ace);
            run.Apply(RunInput.RoomOption(Choice(run, 0)));
            Assert.That(run.Gambler.Deck.Count, Is.EqualTo(deck + 1));
            Assert.That(run.Gambler.Deck.Count(c => c.Rank == Rank.Ace), Is.EqualTo(aces + 1));
        }

        [Test]
        public void PhotoBooth_CopiesAnEnhancedCardOverAPlainOne()
        {
            var run = At(EventKind.PhotoBooth);
            var gilded = run.Gambler.Deck.First(c => c.Rank == Rank.Ace);
            gilded.Enhancement = Enhancement.Gilded;
            run.Apply(RunInput.RoomOption(Choice(run, 0)));
            var copy = run.Gambler.Deck.Last();
            Assert.That(copy, Is.Not.SameAs(gilded));
            Assert.That(copy.Rank, Is.EqualTo(Rank.Ace));
            Assert.That(copy.Suit, Is.EqualTo(gilded.Suit));
            Assert.That(copy.Enhancement, Is.EqualTo(Enhancement.Gilded));
        }

        [Test]
        public void SecuritySweep_LyingLow_Costs4_AndCools25()
        {
            var run = At(EventKind.SecuritySweep, gold: 10);
            run.Gambler.AddHeat(40);
            run.Apply(RunInput.RoomOption(Choice(run, 0)));
            Assert.That(run.Gambler.Heat, Is.EqualTo(15));
            Assert.That(run.Gambler.Gold, Is.EqualTo(6));
        }

        [Test]
        public void SecuritySweep_LyingLow_NeedsTheGold()
        {
            var run = At(EventKind.SecuritySweep, gold: 3);
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 0))), Is.False);
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 1))), Is.True);
        }

        [Test]
        public void SecuritySweep_Bluffing_MovesHeatOneWayOrTheOther()
        {
            var run = At(EventKind.SecuritySweep, gold: 10);
            run.Gambler.AddHeat(60);
            run.Apply(RunInput.RoomOption(Choice(run, 1)));
            Assert.That(run.Gambler.Heat, Is.EqualTo(10).Or.EqualTo(85));
            Assert.That(run.Gambler.Gold, Is.EqualTo(10));
        }

        [Test]
        public void CompTicket_CashedIn_LevelsUpAComboComp()
        {
            var run = At(EventKind.CompTicket);
            int Levels() => Combos.All.Sum(c => run.Gambler.CompLevel(Comps.For(c)));
            int before = Levels();
            run.Apply(RunInput.RoomOption(Choice(run, 0)));
            Assert.That(Levels(), Is.EqualTo(before + 1));
        }

        [Test]
        public void CompTicket_Sold_Pays6()
        {
            var run = At(EventKind.CompTicket, gold: 10);
            run.Apply(RunInput.RoomOption(Choice(run, 1)));
            Assert.That(run.Gambler.Gold, Is.EqualTo(16));
        }

        [Test]
        public void BackAlley_GivesARareVice_AndADeadCard()
        {
            var run = At(EventKind.BackAlley);
            int deck = run.Gambler.Deck.Count;
            run.Apply(RunInput.RoomOption(Choice(run, 0)));
            Assert.That(run.Gambler.Vices.Count, Is.EqualTo(1));
            Assert.That(run.Gambler.Vices[0].Definition.Rarity, Is.EqualTo(Rarity.Rare));
            Assert.That(run.Gambler.Deck.Count, Is.EqualTo(deck + 1));
            Assert.That(run.Gambler.Deck.Count(c => c.Curse == Curse.DeadCard), Is.EqualTo(1));
            Assert.That(run.SeenVices, Has.Member(run.Gambler.Vices[0].Definition.Id));
        }

        [Test]
        public void BackAlley_NeedsAViceSlot()
        {
            var run = At(EventKind.BackAlley);
            FillViceSlots(run);
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 0))), Is.False);
        }

        [Test]
        public void SpilledTray_NeedsACocktailSlot_OrTips3()
        {
            var run = At(EventKind.SpilledTray, gold: 10);
            while (run.Gambler.HasFreeCocktailSlot) run.Gambler.Cocktails.Add(new Cocktail(CocktailKind.IceWater));
            Assert.That(run.IsLegal(RunInput.RoomOption(Choice(run, 0))), Is.False);
            run.Apply(RunInput.RoomOption(Choice(run, 1)));
            Assert.That(run.Gambler.Gold, Is.EqualTo(13));
        }

        [Test]
        public void Hustler_Costs5_AndPays10OnAWin()
        {
            var run = At(EventKind.Hustler, gold: 10);
            run.Apply(RunInput.RoomOption(Choice(run, 0)));
            Assert.That(run.Gambler.Gold, Is.EqualTo(5).Or.EqualTo(15));
        }

        private static void FillViceSlots(Run run)
        {
            foreach (var def in ViceCatalog.OfRarity(Rarity.Common).Take(run.Gambler.ViceSlots - run.Gambler.Vices.Count).ToList())
                run.Gambler.Vices.Add(def.Create());
            Assert.That(run.Gambler.HasFreeViceSlot, Is.False);
        }
    }

    public class CollectionTests
    {
        private static readonly string[] ComboVices = { "Matchmaker", "StraightShooter", "CourtJester", "FlushRoyale", "ComboKing" };

        [Test]
        public void ANewProfile_LocksTheFiveComboVices()
        {
            var p = new Profile();
            Assert.That(p.LockedVices, Is.EquivalentTo(ComboVices));
            foreach (var id in ComboVices)
            {
                Assert.That(p.IsUnlocked(ViceCatalog.Get(id)), Is.False);
                Assert.That(Profile.UnlockHint(ViceCatalog.Get(id)), Is.Not.Null.And.Not.Empty);
            }
            Assert.That(p.IsUnlocked(ViceCatalog.Get("LuckyPenny")), Is.True);
        }

        [Test]
        public void LockedVices_NeverShowUpInARun()
        {
            var options = new RunOptions { LockedVices = ComboVices.ToList() };
            int shops = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var run = new Run(seed, options);
                for (int i = 0; i < 3000 && !run.IsOver; i++)
                {
                    switch (run.Phase)
                    {
                        case RunPhase.AtTable: AutoPlay.Step(run); continue;
                        case RunPhase.Shop:
                            shops++;
                            foreach (var offer in run.Shop.Offers.Where(o => o.Kind == OfferKind.Vice))
                                Assert.That(ComboVices, Has.No.Member(offer.Vice.Id), $"seed {seed} offered {offer.Vice.Id}");
                            // buy what it can, so later rolls see held Vices too
                            // Unity's .NET has no FirstOrDefault with a default value
                            int buy = Enumerable.Range(0, run.Shop.Offers.Count)
                                .Where(o => run.Shop.Offers[o].Kind == OfferKind.Vice && run.IsLegal(RunInput.Buy(o)))
                                .DefaultIfEmpty(-1).First();
                            if (buy >= 0) { run.Apply(RunInput.Buy(buy)); continue; }
                            break;
                        case RunPhase.BossReward:
                            foreach (var reward in run.Rewards) Assert.That(ComboVices, Has.No.Member(reward.Id));
                            break;
                    }
                    AutoPlay.Advance(run);
                }
                foreach (var vice in run.Gambler.Vices) Assert.That(ComboVices, Has.No.Member(vice.Definition.Id));
                Assert.That(run.SeenVices.Intersect(ComboVices), Is.Empty);
            }
            Assert.That(shops, Is.GreaterThan(40));
        }

        [Test]
        public void Rerolling_NeverOffersALockedVice()
        {
            var locked = new Profile().LockedVices;
            for (int seed = 1; seed <= 60; seed++)
            {
                var run = new Run(seed, new RunOptions { LockedVices = locked });
                var map = RunTests.FindRunThatBeatsFirstTableFrom(seed);
                if (map == null) continue;
                run = Run.Replay(map.Seed, map.Inputs, new RunOptions { LockedVices = locked });
                if (run.Phase != RunPhase.Shop) continue;
                run.Gambler.GainGold(200);
                for (int i = 0; i < 15; i++)
                {
                    run.Apply(RunInput.Reroll());
                    foreach (var offer in run.Shop.Offers.Where(o => o.Kind == OfferKind.Vice))
                    {
                        Assert.That(locked, Has.No.Member(offer.Vice.Id));
                        Assert.That(run.SeenVices, Has.Member(offer.Vice.Id));
                    }
                }
                return;
            }
            Assert.Fail("no run reached a shop");
        }

        [Test]
        public void WithoutLocks_ComboVicesDoShowUp()
        {
            var seen = new HashSet<string>();
            for (int seed = 1; seed <= 40; seed++)
            {
                var run = new Run(seed);
                for (int i = 0; i < 3000 && !run.IsOver; i++)
                {
                    if (run.Phase == RunPhase.AtTable) AutoPlay.Step(run);
                    else AutoPlay.Advance(run);
                }
                seen.UnionWith(run.SeenVices);
            }
            Assert.That(seen.Intersect(ComboVices), Is.Not.Empty, "the locked-Vice test would prove nothing otherwise");
        }

        [Test]
        public void Record_MergesTheCollection()
        {
            var run = FinishedRun(r => r.SeenVices.Count > 0 && r.BossesFaced.Count > 0);
            var p = new Profile();
            p.Record(run);
            Assert.That(run.SeenVices, Is.Not.Empty);
            foreach (var id in run.SeenVices) Assert.That(p.HasSeen(ViceCatalog.Get(id)), Is.True, id);
            Assert.That(run.BossesFaced, Is.Not.Empty);
            foreach (var boss in run.BossesFaced) Assert.That(p.HasFaced(boss), Is.True, boss);
            foreach (var combo in Combos.All) Assert.That(p.CombosWon(combo), Is.EqualTo(run.CombosWon[(int)combo]));

            // a second run adds to the counts
            p.Record(run);
            foreach (var combo in Combos.All) Assert.That(p.CombosWon(combo), Is.EqualTo(2L * run.CombosWon[(int)combo]));
        }

        [Test]
        public void WinningAFlush_UnlocksFlushRoyale()
        {
            var run = FinishedRun(r => r.CombosWon[(int)Combo.Flush] > 0);
            var p = new Profile();
            var flushRoyale = ViceCatalog.Get("FlushRoyale");
            Assert.That(p.IsUnlocked(flushRoyale), Is.False);
            var unlocked = p.Record(run);
            Assert.That(unlocked, Has.Member($"{flushRoyale.Name} (Vice)"));
            Assert.That(p.IsUnlocked(flushRoyale), Is.True);
            Assert.That(p.LockedVices, Has.No.Member("FlushRoyale"));
            Assert.That(p.Record(run), Has.No.Member($"{flushRoyale.Name} (Vice)"), "an unlock is only reported once");
        }

        [Test]
        public void ARunWithNoFlush_LeavesFlushRoyaleLocked()
        {
            var run = FinishedRun(r => r.CombosWon[(int)Combo.Flush] == 0);
            var p = new Profile();
            var unlocked = p.Record(run);
            Assert.That(p.IsUnlocked(ViceCatalog.Get("FlushRoyale")), Is.False);
            Assert.That(unlocked, Has.No.Member("Flush Royale (Vice)"));
            Assert.That(p.LockedVices, Has.Member("FlushRoyale"));
        }

        [Test]
        public void ComboKing_UnlocksAt50CombosInTotal()
        {
            var p = new Profile();
            var run = FinishedRun(r => r.CombosWon.Sum() > 0);
            int perRun = run.CombosWon.Sum();
            int needed = (50 + perRun - 1) / perRun;
            for (int i = 1; i < needed; i++) p.Record(run);
            Assert.That(p.IsUnlocked(ViceCatalog.Get("ComboKing")), Is.False);
            Assert.That(p.Record(run), Has.Member("Combo King (Vice)"));
            Assert.That(p.IsUnlocked(ViceCatalog.Get("ComboKing")), Is.True);
        }

        [Test]
        public void TheCollection_RoundTripsThroughText()
        {
            var run = FinishedRun(r => r.CombosWon[(int)Combo.Flush] > 0);
            var p = new Profile();
            p.Record(run);
            var text = p.Serialize();
            Assert.That(text, Does.Contain("seen=").And.Contain("bosses=").And.Contain("combos=").And.Contain("vices="));
            var back = Profile.Parse(text);
            Assert.That(back.Serialize(), Is.EqualTo(text));
            Assert.That(back.LockedVices, Is.EquivalentTo(p.LockedVices));
            Assert.That(back.IsUnlocked(ViceCatalog.Get("FlushRoyale")), Is.True);
            foreach (var id in run.SeenVices) Assert.That(back.HasSeen(ViceCatalog.Get(id)), Is.True);
            foreach (var boss in run.BossesFaced) Assert.That(back.HasFaced(boss), Is.True);
            foreach (var combo in Combos.All) Assert.That(back.CombosWon(combo), Is.EqualTo(p.CombosWon(combo)));
        }

        [Test]
        public void AnOldProfile_WithoutCollectionLines_StillLocksTheComboVices()
        {
            var back = Profile.Parse("decks=0\nstake=0\nruns=3\n");
            Assert.That(back.Runs, Is.EqualTo(3));
            Assert.That(back.LockedVices, Is.EquivalentTo(ComboVices));
            Assert.That(back.CombosWon(Combo.Flush), Is.EqualTo(0));
        }

        // a run the bot played to the end, from the first seed that matches
        private static Run FinishedRun(System.Func<Run, bool> matches)
        {
            for (int seed = 1; seed < 200; seed++)
            {
                var run = new Run(seed);
                for (int i = 0; i < 3000 && !run.IsOver; i++)
                {
                    if (run.Phase == RunPhase.AtTable) AutoPlay.Step(run);
                    else AutoPlay.Advance(run);
                }
                if (run.IsOver && matches(run)) return run;
            }
            Assert.Fail("no finished run matched");
            return null;
        }
    }
}
