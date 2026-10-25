using System.Linq;
using HouseEdge.Core;
using NUnit.Framework;
using static HouseEdge.Core.PlayerAction;
using static HouseEdge.Tests.TestCards;

namespace HouseEdge.Tests
{
    // deal order: player, dealer up, player, dealer hole, then draws
    public class SuitAndComboViceTests
    {
        private static Vice V(string name) => ViceCatalog.All.First(d => d.Name == name).Create();

        private static Table With(Vice vice, params object[] cards) => With(new[] { vice }, cards);

        private static Table With(Vice[] vices, params object[] cards)
        {
            var gambler = new Gambler();
            gambler.Vices.AddRange(vices);
            return new Table(gambler, StackOf(cards), new GameRandom(1), long.MaxValue, vices);
        }

        private static HandScore Score(Table t) => t.LastScores.Single();

        private static HandScore Played(Vice[] vices, params object[] cards)
        {
            var t = With(vices, cards);
            Play(t, Deal, Stand);
            return Score(t);
        }

        private static Card Wild(string label) => C(label, Enhancement.Wild);

        [Test]
        public void TheNewVices_HaveTheirRarityAndArchetype()
        {
            void Check(string name, Rarity rarity, Archetype archetype)
            {
                var def = ViceCatalog.All.Where(d => d.Name == name).DefaultIfEmpty(null).First();
                Assert.That(def, Is.Not.Null, name);
                Assert.That(def.Rarity, Is.EqualTo(rarity), name);
                Assert.That(def.Archetype, Is.EqualTo(archetype), name);
            }
            Check("Clover Club", Rarity.Common, Archetype.Lucky);
            Check("Heartbreaker", Rarity.Common, Archetype.Steady);
            Check("Card Tricks", Rarity.Common, Archetype.Combo);
            Check("Diamond Mine", Rarity.Uncommon, Archetype.Greedy);
            Check("Ace of Spades", Rarity.Uncommon, Archetype.Risky);
            Check("Double Vision", Rarity.Uncommon, Archetype.Combo);
            Check("Rainbow", Rarity.Uncommon, Archetype.Lucky);
            Check("Full House", Rarity.Rare, Archetype.Combo);
        }

        // Clover Club

        [Test]
        public void CloverClub_Adds3MultPerClub_InAWinningHand()
        {
            // K♣ 9♠ = 19 beats 17, no combos
            var t = With(V("Clover Club"), "Kc", "9h", "9s", "8d");
            Play(t, Deal, Stand);
            // a win's x1 + one Club's +3
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 3));
        }

        [Test]
        public void CloverClub_CountsAWildCard_AsAClub()
        {
            var t = With(V("Clover Club"), "Kc", "9h", Wild("9s"), "8d");
            Play(t, Deal, Stand);
            // a win's x1 + the Suited combo's +1 (the Wild fits clubs) + two Clubs' +6
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 1 + 6));
        }

        [Test]
        public void CloverClub_GivesNothingOnALoss()
        {
            // K♣ 7♣ = 17 loses to 19
            var t = With(V("Clover Club"), "Kc", "10h", "7c", "9d");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(Score(t).Mult, Is.EqualTo(0));
        }

        // Heartbreaker

        [Test]
        public void Heartbreaker_Adds15ChipsPerHeart_InAWinningHand()
        {
            var t = With(V("Heartbreaker"), "Kh", "9s", "9c", "8d");
            Play(t, Deal, Stand);
            // 19 + 5 for the win, + 15 for one Heart
            Assert.That(Score(t).Chips, Is.EqualTo(19 + 5 + 15));
        }

        [Test]
        public void Heartbreaker_CountsAWildCard_AsAHeart()
        {
            var t = With(V("Heartbreaker"), "Kh", "9s", Wild("9c"), "8d");
            Play(t, Deal, Stand);
            // 19 + 5 for the win, + 10 for the Suited combo, + 30 for two Hearts
            Assert.That(Score(t).Chips, Is.EqualTo(19 + 5 + 10 + 30));
        }

        [Test]
        public void Heartbreaker_GivesNothingOnALoss()
        {
            var t = With(V("Heartbreaker"), "Kh", "10s", "7h", "9d");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(Score(t).Chips, Is.EqualTo(0));
        }

        // Card Tricks

        [Test]
        public void CardTricks_Adds3MultPerCombo()
        {
            // 9♠ 9♥ is a Pair
            var pair = Played(new[] { V("Card Tricks") }, "9s", "10h", "9h", "7d");
            // a win's x1 + the Pair's +1 + Card Tricks' +3
            Assert.That(pair.Mult, Is.EqualTo(1 + 1 + 3));

            // a Wild 9 makes it a Pair and Suited
            var two = Played(new[] { V("Card Tricks") }, "9s", "10h", Wild("9h"), "7d");
            // a win's x1 + the Pair's +1 + Suited's +1 + Card Tricks' +3 x 2
            Assert.That(two.Mult, Is.EqualTo(1 + 1 + 1 + 6));
        }

        [Test]
        public void CardTricks_GivesNothingWithoutACombo()
        {
            var none = Played(new[] { V("Card Tricks") }, "Ks", "9h", "9c", "8d");
            Assert.That(none.Combos, Is.Empty);
            Assert.That(none.Mult, Is.EqualTo(1));
        }

        // Diamond Mine

        [Test]
        public void DiamondMine_Pays1GoldPerDiamond_InAWinningHand()
        {
            var t = With(V("Diamond Mine"), "Kd", "9h", "9s", "8c");
            Play(t, Deal, Stand);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold + 1));
        }

        [Test]
        public void DiamondMine_CountsAWildCard_AsADiamond()
        {
            var t = With(V("Diamond Mine"), "Kd", "9h", Wild("9s"), "8c");
            Play(t, Deal, Stand);
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold + 2));
        }

        [Test]
        public void DiamondMine_PaysNothingOnALoss()
        {
            var t = With(V("Diamond Mine"), "Kd", "10h", "7d", "9c");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(t.Gold, Is.EqualTo(Balance.StartingGold));
        }

        // Ace of Spades

        [Test]
        public void AceOfSpades_MultipliesBy1Point2PerSpade()
        {
            var one = Played(new[] { V("Ace of Spades") }, "Ks", "9h", "9c", "8d");
            // a win's x1, x1.2 for one Spade
            Assert.That(one.Mult, Is.EqualTo(1 * 1.2).Within(0.001));

            var wild = Played(new[] { V("Ace of Spades") }, "Ks", "9h", Wild("9c"), "8d");
            // a win's x1 + the Suited combo's +1, x1.2 twice (the Wild counts as a Spade)
            Assert.That(wild.Mult, Is.EqualTo((1 + 1) * 1.2 * 1.2).Within(0.001));
        }

        [Test]
        public void AceOfSpades_IgnoresCursedCards()
        {
            // a Dead Card is a spade under the hood, but curses don't count
            var plain = Played(new Vice[0], Card.Cursed(Curse.DeadCard), "10h", "9h", "7d");
            var spades = Played(new[] { V("Ace of Spades") }, Card.Cursed(Curse.DeadCard), "10h", "9h", "7d");
            Assert.That(spades.Outcome, Is.EqualTo(Outcome.Win));
            Assert.That(spades.Mult, Is.EqualTo(plain.Mult));
        }

        [Test]
        public void AceOfSpades_DoesNothingOnALoss()
        {
            var t = With(V("Ace of Spades"), "Ks", "10h", "7s", "9d");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(Score(t).Mult, Is.EqualTo(0));
        }

        // Double Vision

        [Test]
        public void DoubleVision_PaysAPairTwice()
        {
            var pair = Played(new[] { V("Double Vision") }, "9s", "10h", "9h", "7d");
            // 18 + 5 for the win, + the Pair's 10 twice
            Assert.That(pair.Chips, Is.EqualTo(18 + 5 + 10 + 10));
            // a win's x1 + the Pair's +1 twice
            Assert.That(pair.Mult, Is.EqualTo(1 + 1 + 1));
        }

        [Test]
        public void DoubleVision_DoesntRepeatOtherCombos()
        {
            var suited = Played(new[] { V("Double Vision") }, "Kh", "9s", "9h", "8d");
            // 19 + 5 for the win, + Suited's 10 once
            Assert.That(suited.Chips, Is.EqualTo(19 + 5 + 10));
            Assert.That(suited.Mult, Is.EqualTo(1 + 1));
        }

        [Test]
        public void DoubleVision_DoesNothingOnALoss()
        {
            // 8♠ 8♥ = 16 loses to 19
            var t = With(V("Double Vision"), "8s", "10h", "8h", "9d");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(Score(t).Chips, Is.EqualTo(0));
            Assert.That(Score(t).Mult, Is.EqualTo(0));
        }

        // Rainbow

        [Test]
        public void Rainbow_Adds8Mult_ForThreeSuits()
        {
            // 10♠ 5♣, hit 4♦ = 19 beats 18
            var t = With(V("Rainbow"), "10s", "10h", "5c", "8d", "4d");
            Play(t, Deal, Hit, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Win));
            // a win's x1 + Rainbow's +8
            Assert.That(Score(t).Mult, Is.EqualTo(1 + 8));
        }

        [Test]
        public void Rainbow_NeedsThreeSuits()
        {
            var two = Played(new[] { V("Rainbow") }, "Ks", "9h", "9c", "8d");
            Assert.That(two.Mult, Is.EqualTo(1));
        }

        [Test]
        public void Rainbow_IgnoresCursedCards()
        {
            // a Dead Card (a spade under the hood), 5♣ and 4♦: only two real suits
            var plain = With(new Vice[0], Card.Cursed(Curse.DeadCard), "10h", "5c", "8d", "4d");
            var rainbow = With(V("Rainbow"), Card.Cursed(Curse.DeadCard), "10h", "5c", "8d", "4d");
            Play(plain, Deal, Hit, Stand);
            Play(rainbow, Deal, Hit, Stand);
            Assert.That(Score(rainbow).Outcome, Is.EqualTo(Outcome.Win));
            Assert.That(Score(rainbow).Mult, Is.EqualTo(Score(plain).Mult));
        }

        [Test]
        public void Rainbow_DoesNothingOnALoss()
        {
            // 10♠ 3♣, hit 4♦ = 17 loses to 18
            var t = With(V("Rainbow"), "10s", "10h", "3c", "8d", "4d");
            Play(t, Deal, Hit, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(Score(t).Mult, Is.EqualTo(0));
        }

        // Full House

        [Test]
        public void FullHouse_TriplesAPairThatIsAlsoSuited()
        {
            // 9♠ and a Wild 9: a Pair and Suited
            var both = Played(new[] { V("Full House") }, "9s", "10h", Wild("9h"), "7d");
            // (a win's x1 + the Pair's +1 + Suited's +1) x3
            Assert.That(both.Mult, Is.EqualTo((1 + 1 + 1) * 3));
        }

        [Test]
        public void FullHouse_NeedsBothAPairAndASuit()
        {
            var pair = Played(new[] { V("Full House") }, "9s", "10h", "9h", "7d");
            Assert.That(pair.Mult, Is.EqualTo(1 + 1), "a Pair alone");
            var suited = Played(new[] { V("Full House") }, "Kh", "9s", "9h", "8d");
            Assert.That(suited.Mult, Is.EqualTo(1 + 1), "Suited alone");
        }

        [Test]
        public void FullHouse_DoesNothingOnALoss()
        {
            // 8♠ and a Wild 8 = 16 loses to 19
            var t = With(V("Full House"), "8s", "10h", Wild("8h"), "9d");
            Play(t, Deal, Stand);
            Assert.That(Score(t).Outcome, Is.EqualTo(Outcome.Lose));
            Assert.That(Score(t).Mult, Is.EqualTo(0));
        }

        // the Combo set bonus

        // three Combo Vices that do nothing for a plain hard hand that isn't split
        private static Vice[] ComboSet() => new[] { V("Split Personality"), V("Twins"), V("Soft Touch") };

        [Test]
        public void ComboSet_Adds2MultToEveryCombo()
        {
            // 9♠ and a Wild 9: a Pair and Suited
            var set = ComboSet();
            Assert.That(set.All(v => v.Definition.Archetype == Archetype.Combo), Is.True);
            var withSet = Played(set, "9s", "10h", Wild("9h"), "7d");
            // a win's x1 + (the Pair's +1 + 2) + (Suited's +1 + 2)
            Assert.That(withSet.Mult, Is.EqualTo(1 + (1 + Balance.ComboSetBonusMult) + (1 + Balance.ComboSetBonusMult)));
            Assert.That(Balance.ComboSetBonusMult, Is.EqualTo(2));
            // Chips don't change: 18 + 5 for the win, + 10 for each combo
            Assert.That(withSet.Chips, Is.EqualTo(18 + 5 + 10 + 10));
        }

        [Test]
        public void ComboSet_NeedsThreeComboVices()
        {
            var two = Played(new[] { V("Split Personality"), V("Twins") }, "9s", "10h", Wild("9h"), "7d");
            // a win's x1 + the Pair's +1 + Suited's +1
            Assert.That(two.Mult, Is.EqualTo(1 + 1 + 1));
        }

        [Test]
        public void ComboSet_GivesNothingWithoutACombo()
        {
            var none = Played(ComboSet(), "Ks", "9h", "9c", "8d");
            Assert.That(none.Mult, Is.EqualTo(1));
        }

        // re-tuned Heat costs not checked elsewhere

        [Test]
        public void Pickpocket_Adds3Heat_WhenItSteals()
        {
            var t = With(V("Pickpocket"), "10", "10", "8", "6", "K");
            Play(t, Deal, Stand);
            Assert.That(t.Gambler.Heat, Is.EqualTo(3));
        }

        [Test]
        public void RiggedShoe_Adds3HeatPerHand()
        {
            var t = With(V("Rigged Shoe"), "10", "10", "9", "7", "5");
            Play(t, Deal, Stand);
            Assert.That(t.Gambler.Heat, Is.EqualTo(3));
        }

        [Test]
        public void ActionHeat_WasLowered()
        {
            Assert.That(Balance.CardMechanicHeat, Is.EqualTo(6));
            Assert.That(Balance.ColdReadHeat, Is.EqualTo(5));
            Assert.That(Balance.TheDealerHeat, Is.EqualTo(10));
        }
    }

    public class NewStakeTests
    {
        private static Run At(Stake stake, int seed = 1) => new Run(seed, new RunOptions { Stake = stake });

        private static long Rounded(double value) => (long)System.Math.Round(value);

        [Test]
        public void Stakes_GoWhiteToPlatinum()
        {
            Assert.That(Stakes.All, Is.EqualTo(new[]
            {
                Stake.White, Stake.Red, Stake.Green, Stake.Black, Stake.Purple,
                Stake.Gold, Stake.Blue, Stake.Orange, Stake.Silver, Stake.Platinum,
            }));
        }

        [Test]
        public void GreenStake_Targets1Point1x()
        {
            Assert.That(Stakes.TargetFactor(Stake.Red), Is.EqualTo(1));
            Assert.That(Stakes.TargetFactor(Stake.Green), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(At(Stake.Green).Table.Target, Is.EqualTo(Rounded(Balance.Target(1, TableKind.Low) * 1.1)));
            Assert.That(At(Stake.Blue).Table.Target, Is.EqualTo(Rounded(Balance.Target(1, TableKind.Low) * 1.1)), "only 1.1x until Orange");
        }

        [Test]
        public void BlackStake_StartsWith2DeadCards_AndLaterStakesKeepThem()
        {
            Assert.That(At(Stake.Green).Gambler.Deck.Count(c => c.Curse == Curse.DeadCard), Is.EqualTo(0));
            Assert.That(At(Stake.Black).Gambler.Deck.Count(c => c.Curse == Curse.DeadCard), Is.EqualTo(2));
            Assert.That(At(Stake.Platinum).Gambler.Deck.Count(c => c.Curse == Curse.DeadCard), Is.EqualTo(2));
            Assert.That(At(Stake.Black).Gambler.Deck.Count, Is.EqualTo(52 + 2));
        }

        [Test]
        public void PurpleStake_AddsAGoldToEveryShopPrice()
        {
            var offers = new[]
            {
                ShopOffer.ForCocktail(CocktailKind.IceWater),
                ShopOffer.ForVice(ViceCatalog.OfRarity(Rarity.Common).First()),
                ShopOffer.ForVice(ViceCatalog.OfRarity(Rarity.Rare).First()),
            };
            Assert.That(Stakes.PriceBump(Stake.Black), Is.EqualTo(0));
            Assert.That(Stakes.PriceBump(Stake.Purple), Is.EqualTo(1));
            foreach (var offer in offers)
            {
                Assert.That(At(Stake.Black).PriceOf(offer), Is.EqualTo(offer.BasePrice));
                Assert.That(At(Stake.Purple).PriceOf(offer), Is.EqualTo(offer.BasePrice + 1));
                Assert.That(At(Stake.Platinum).PriceOf(offer), Is.EqualTo(offer.BasePrice + 1));
            }
        }

        // Old Pete deals with +1 Burn
        private static int DealerBurns(Run run) => run.Dealer is OldPete ? 1 : 0;

        [Test]
        public void BlueStake_Takes1BurnEveryTable()
        {
            var gold = At(Stake.Gold);
            var blue = At(Stake.Blue);
            var platinum = At(Stake.Platinum);
            Assert.That(gold.Table.BurnsLeft, Is.EqualTo(Balance.StartingBurns + DealerBurns(gold)));
            Assert.That(blue.Table.BurnsLeft, Is.EqualTo(Balance.StartingBurns - 1 + DealerBurns(blue)));
            Assert.That(platinum.Table.BurnsLeft, Is.EqualTo(Balance.StartingBurns - 1 + DealerBurns(platinum)));
        }

        [Test]
        public void BlueStake_TakesTheBurn_AtLaterTablesToo()
        {
            var run = FindRunOnTheMap(new RunOptions { Stake = Stake.Blue });
            AutoPlay.ToNextTable(run);
            Assert.That(run.Phase, Is.EqualTo(RunPhase.AtTable));
            Assert.That(run.Stage, Is.EqualTo(TableKind.High));
            Assert.That(run.Table.BurnsLeft, Is.EqualTo(Balance.StartingBurns - 1 + DealerBurns(run)));
        }

        [Test]
        public void OrangeStake_Targets1Point1xAgain()
        {
            Assert.That(Stakes.TargetFactor(Stake.Orange), Is.EqualTo(1.21).Within(1e-9));
            Assert.That(Stakes.TargetFactor(Stake.Platinum), Is.EqualTo(1.21).Within(1e-9));
            // 130 x 1.21 = 157.3
            Assert.That(At(Stake.Orange).Table.Target, Is.EqualTo(Rounded(Balance.Target(1, TableKind.Low) * 1.1 * 1.1)));
            Assert.That(At(Stake.Orange).Table.Target, Is.EqualTo(157));
        }

        [Test]
        public void SilverStake_BossTablesHave1Point2xTargets_ButNormalTablesDont()
        {
            Assert.That(Stakes.BossFactor(Stake.Orange), Is.EqualTo(1));
            Assert.That(Stakes.BossFactor(Stake.Silver), Is.EqualTo(1.2));

            var low = At(Stake.Silver);
            Assert.That(low.Boss, Is.Null);
            Assert.That(low.Table.Target, Is.EqualTo(low.TargetFor(TableKind.Low)), "no boss factor at a normal table");

            var silver = FindRunAt(new RunOptions { Stake = Stake.Silver }, TableKind.Boss);
            Assert.That(silver.Table.Target,
                Is.EqualTo(Rounded(silver.TargetFor(TableKind.Boss) * silver.Boss.TargetFactor * 1.2)));

            var orange = FindRunAt(new RunOptions { Stake = Stake.Orange }, TableKind.Boss);
            Assert.That(orange.Table.Target,
                Is.EqualTo(Rounded(orange.TargetFor(TableKind.Boss) * orange.Boss.TargetFactor)));
        }

        [Test]
        public void SilverStake_EliteTablesHave1Point2xTargets()
        {
            var run = FindRunOnTheMap(new RunOptions { Stake = Stake.Silver }, r => r.RoomChoices.Contains(RoomKind.Elite));
            var next = run.NextStage.stage;
            run.Apply(RunInput.ChooseRoom(run.RoomChoices.ToList().IndexOf(RoomKind.Elite)));
            Assert.That(run.AtElite, Is.True);
            Assert.That(run.Table.Target,
                Is.EqualTo(Rounded(run.TargetFor(next) * run.Boss.TargetFactor * 1.2 * Balance.EliteTargetFactor)));
        }

        [Test]
        public void PlatinumStake_Takes1HandEveryTable()
        {
            Assert.That(At(Stake.Silver).Table.HandsLeft, Is.EqualTo(Balance.StartingHands));
            Assert.That(At(Stake.Platinum).Table.HandsLeft, Is.EqualTo(Balance.StartingHands - 1));
        }

        [Test]
        public void PlatinumStake_CapsInterestAt2()
        {
            Assert.That(Stakes.PlatinumInterestCap, Is.EqualTo(2));
            // 30 extra gold is 6 interest before any cap
            Assert.That(InterestAfterFirstTable(Stake.Silver), Is.EqualTo(Balance.InterestCap));
            Assert.That(InterestAfterFirstTable(Stake.Platinum), Is.EqualTo(Stakes.PlatinumInterestCap));
        }

        private static int InterestAfterFirstTable(Stake stake)
        {
            for (int seed = 1; seed < 500; seed++)
            {
                var run = At(stake, seed);
                run.Gambler.GainGold(30);
                AutoPlay.UntilThisTableEnds(run);
                if (run.Phase != RunPhase.RoomChoice) continue;
                return run.LastPayout.Interest;
            }
            Assert.Fail($"no seed beat the first table at {stake}");
            return -1;
        }

        // a run standing at the start of a table of this kind
        private static Run FindRunAt(RunOptions options, TableKind kind)
        {
            for (int seed = 1; seed < 400; seed++)
            {
                var run = new Run(seed, options);
                while (!run.IsOver && !(run.Phase == RunPhase.AtTable && run.Stage == kind && run.Table.DealsPlayed == 0))
                {
                    if (run.Phase == RunPhase.AtTable) AutoPlay.Step(run);
                    else AutoPlay.Advance(run);
                }
                if (!run.IsOver) return run;
            }
            Assert.Fail($"no seed reached a {kind} table at {options.Stake}");
            return null;
        }

        // a run that just beat the Low Table and is choosing its first stop
        private static Run FindRunOnTheMap(RunOptions options, System.Func<Run, bool> matches = null)
        {
            for (int seed = 1; seed < 1000; seed++)
            {
                var run = new Run(seed, options);
                AutoPlay.UntilThisTableEnds(run);
                if (run.Phase == RunPhase.RoomChoice && (matches == null || matches(run))) return run;
            }
            Assert.Fail($"no seed got onto the map at {options.Stake}");
            return null;
        }
    }

    public class StakeProgressTests
    {
        // Profile only looks at how a run ended, so mark a fresh run as won rather than playing ten whole runs
        private static Run WonAt(Stake stake)
        {
            var run = new Run(1, new RunOptions { Stake = stake });
            typeof(Run).GetProperty(nameof(Run.Phase)).SetValue(run, RunPhase.Won);
            typeof(Run).GetProperty(nameof(Run.BeatTheHouse)).SetValue(run, true);
            return run;
        }

        [Test]
        public void WinningAtEachStake_UnlocksTheNext_UpToPlatinum()
        {
            var p = new Profile();
            for (int i = 1; i < Stakes.All.Count; i++)
            {
                var next = Stakes.All[i];
                Assert.That(p.IsUnlocked(next), Is.False, next.ToString());
                var unlocked = p.Record(WonAt(p.HighestStake));
                Assert.That(p.HighestStake, Is.EqualTo(next));
                Assert.That(unlocked, Has.Member($"{Stakes.Name(next)} stake"));
            }
            Assert.That(p.HighestStake, Is.EqualTo(Stake.Platinum));

            // nothing past Platinum
            p.Record(WonAt(Stake.Platinum));
            Assert.That(p.HighestStake, Is.EqualTo(Stake.Platinum));
            Assert.That(Profile.Parse(p.Serialize()).HighestStake, Is.EqualTo(Stake.Platinum));
        }

        [Test]
        public void WinningBelowYourHighestStake_DoesntClimb()
        {
            var p = new Profile();
            p.Record(WonAt(Stake.White));
            p.Record(WonAt(Stake.Red));
            Assert.That(p.HighestStake, Is.EqualTo(Stake.Green));
            p.Record(WonAt(Stake.White));
            Assert.That(p.HighestStake, Is.EqualTo(Stake.Green));
        }

        [Test]
        public void RunCodes_EncodeEveryStake_InOneDigit()
        {
            foreach (var stake in Stakes.All)
            {
                var options = new RunOptions { Deck = StartingDeck.MarkedDeck, Stake = stake };
                string code = RunCodes.Encode(12345, options);
                Assert.That(code.Split('-')[1].Length, Is.EqualTo(2), code);
                Assert.That(RunCodes.TryDecode(code, out int seed, out var back), Is.True, code);
                Assert.That(seed, Is.EqualTo(12345));
                Assert.That(back.Stake, Is.EqualTo(stake));
                Assert.That(back.Deck, Is.EqualTo(StartingDeck.MarkedDeck));

                options.Endless = true;
                Assert.That(RunCodes.TryDecode(RunCodes.Encode(-7, options), out _, out var endless), Is.True);
                Assert.That(endless.Stake, Is.EqualTo(stake));
                Assert.That(endless.Endless, Is.True);
            }
        }
    }
}
