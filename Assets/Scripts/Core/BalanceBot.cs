using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    // a simple, deterministic player for balance runs. it uses the same Run API you do:
    // close-to-basic strategy at the table, buys Vices it likes (optionally one archetype),
    // and walks into the safer rooms
    public sealed class BalanceBot
    {
        private readonly Archetype? _focus;

        public BalanceBot(Archetype? focus = null) => _focus = focus;

        // plays until the run ends (or a safety cap). returns the number of inputs
        public int Play(Run run, int maxInputs = 5000)
        {
            int inputs = 0;
            while (!run.IsOver && inputs < maxInputs)
            {
                run.Apply(Choose(run));
                inputs++;
            }
            return inputs;
        }

        public RunInput Choose(Run run)
        {
            switch (run.Phase)
            {
                case RunPhase.AtTable:
                    return CocktailToDrink(run) ?? RunInput.Table(ChooseAction(run.Table, run.Gambler));
                case RunPhase.BossReward:
                    return ChooseReward(run);
                case RunPhase.Shop:
                    return CocktailToDrink(run) ?? ChooseShop(run);
                case RunPhase.RoomChoice:
                    return RunInput.ChooseRoom(ChooseRoom(run));
                case RunPhase.InRoom:
                    return ChooseInRoom(run);
            }
            return RunInput.LeaveShop();
        }

        // the table

        public static PlayerAction ChooseAction(Table table, Gambler gambler)
        {
            var legal = table.LegalActions();
            if (legal.Contains(PlayerAction.Deal)) return PlayerAction.Deal;
            if (legal.Contains(PlayerAction.DeclineInsurance)) return PlayerAction.DeclineInsurance;
            if (table.Phase == TablePhase.DealerDecision)
            {
                var next = table.NextCardPreview;
                bool busts = next != null && HandValue.Of(table.DealerCards.Append(next).ToList()).IsBust;
                return busts ? PlayerAction.LetDealerDraw : PlayerAction.StopDealer;
            }

            var hand = table.ActiveHand;
            var value = hand.Value;
            if (value.IsBust) return legal.Contains(PlayerAction.Burn) ? PlayerAction.Burn : PlayerAction.Stand;

            int limit = table.Rules.PlayerBustLimit;
            int up = table.DealerUpCard.Value;
            // with the hole card showing, play against the dealer's real total
            if (!table.HoleCardHidden && table.DealerCards.Count > 1) up = table.DealerValue.Total;

            if (legal.Contains(PlayerAction.Split) && hand.Cards.Count == 2)
            {
                var rank = hand.Cards[0].Rank;
                if (rank == Rank.Ace || rank == Rank.Eight) return PlayerAction.Split;
            }

            // Card Mechanic: trade your last card for the dealer's up card when it helps you and hurts him
            if (legal.Contains(PlayerAction.SwapWithDealer) && hand.Cards.Count >= 2)
            {
                var swapped = hand.Cards.Take(hand.Cards.Count - 1).Append(table.DealerUpCard).ToList();
                var mine = HandValue.Of(swapped, limit, table.Rules.AcesAlwaysOne);
                if (!mine.IsBust && mine.Total > value.Total && table.DealerUpCard.Value > hand.Cards[hand.Cards.Count - 1].Value)
                    return PlayerAction.SwapWithDealer;
            }

            // a known next card (Card Counter, Marked): take it if it's safe and we're not already great
            var known = table.NextCardPreview;
            if (known != null && legal.Contains(PlayerAction.Hit))
            {
                var after = HandValue.Of(hand.Cards.Append(known).ToList(), limit, table.Rules.AcesAlwaysOne);
                if (!after.IsBust && value.Total < 20 && after.Total > value.Total) return PlayerAction.Hit;
                if (after.IsBust && value.Total >= 12) return legal.Contains(PlayerAction.Stand) ? PlayerAction.Stand : legal[0];
            }

            int total = value.Total + (Balance.BustLimit - limit);
            bool canDouble = legal.Contains(PlayerAction.DoubleDown) && hand.Cards.Count == 2;
            if (canDouble && !value.IsSoft && (total == 11 || total == 10 && up <= 9)) return PlayerAction.DoubleDown;

            bool hit;
            if (value.IsSoft) hit = total <= 17 || total == 18 && up >= 9;
            else if (total <= 11) hit = true;
            else if (total == 12) hit = up < 4 || up > 6;
            else if (total <= 16) hit = up >= 7;
            else hit = false;

            if (hit && legal.Contains(PlayerAction.Hit)) return PlayerAction.Hit;
            return legal.Contains(PlayerAction.Stand) ? PlayerAction.Stand : legal[0];
        }

        // between tables

        private RunInput? CocktailToDrink(Run run)
        {
            for (int i = 0; i < run.Gambler.Cocktails.Count; i++)
            {
                var kind = run.Gambler.Cocktails[i].Kind;
                bool want = kind switch
                {
                    CocktailKind.ColdBrew => run.Gambler.Heat >= 40,
                    CocktailKind.IceWater => run.Phase == RunPhase.AtTable,
                    CocktailKind.WhiskeySour => run.Gambler.Vices.Count >= 3,
                    CocktailKind.WildCard => run.Gambler.HasFreeViceSlot,
                    _ => false,
                };
                if (want && run.IsLegal(RunInput.UseCocktail(i))) return RunInput.UseCocktail(i);
            }
            return null;
        }

        // held back so interest keeps paying the full 5 a table
        private const int InterestReserve = Balance.InterestStep * Balance.InterestCap;

        private RunInput ChooseShop(Run run)
        {
            if (run.IsLegal(RunInput.RepayDebt()) && run.Gambler.Gold >= run.Gambler.Debt) return RunInput.RepayDebt();

            var offers = run.Shop.Offers;
            int best = -1;
            double bestValue = 0;
            for (int i = 0; i < offers.Count; i++)
            {
                if (!run.IsLegal(RunInput.Buy(i))) continue;
                double value = Value(offers[i], run);
                if (value > bestValue)
                {
                    bestValue = value;
                    best = i;
                }
            }
            if (best >= 0) return RunInput.Buy(best);

            // rich: look for a better Vice than the weakest one held
            if (!run.Gambler.HasFreeViceSlot && run.Gambler.Gold >= InterestReserve + 15 && run.IsLegal(RunInput.Reroll())
                && run.Shop.RerollCost <= Balance.RerollBaseCost + 3)
                return RunInput.Reroll();

            // a full set of Vices: swap the weakest for a better one in the shop
            if (!run.Gambler.HasFreeViceSlot)
            {
                var weakest = run.Gambler.Vices.Select((v, i) => (v, i)).OrderBy(p => ViceValue(p.v.Definition)).First();
                var better = offers.Where(o => !o.Sold && o.Kind == OfferKind.Vice)
                    .FirstOrDefault(o => ViceValue(o.Vice) > ViceValue(weakest.v.Definition) + 2
                                         && run.Gambler.Gold + run.SellValue(weakest.v) >= run.PriceOf(o));
                if (better != null) return RunInput.SellVice(weakest.i);
            }
            return RunInput.LeaveShop();
        }

        private double Value(ShopOffer offer, Run run)
        {
            switch (offer.Kind)
            {
                case OfferKind.Vice:
                    // early on every Vice helps; later only spend the reserve on good ones
                    if (run.Gambler.Gold - run.PriceOf(offer) < InterestReserve && run.Floor > 2 && ViceValue(offer.Vice) < 7) return 0;
                    return ViceValue(offer.Vice);
                case OfferKind.Comp:
                    // with gold to spare above the interest reserve. the common outcomes first
                    if (run.Gambler.Gold < run.PriceOf(offer) + InterestReserve) return 0;
                    return offer.Comp switch
                    {
                        Comp.Win => 3,
                        Comp.DealerBust => 2.5,
                        Comp.TwentyOne => 2,
                        Comp.Blackjack => 1.5,
                        _ => 1,
                    };
                default:
                    return offer.Cocktail == CocktailKind.ColdBrew && run.Gambler.Heat >= 40 ? 3 : 0;
            }
        }

        private double ViceValue(ViceDefinition vice)
        {
            if (vice.IsCursed) return 0;
            double value = vice.Rarity switch
            {
                Rarity.Common => 4,
                Rarity.Uncommon => 5,
                Rarity.Rare => 7,
                _ => 9,
            };
            // Heat is a cost the bot doesn't manage well
            if (vice.IsCheater) value -= 2;
            if (_focus.HasValue) value += vice.Archetype == _focus.Value ? 5 : -3;
            return value;
        }

        private RunInput ChooseReward(Run run)
        {
            if (!run.Gambler.HasFreeViceSlot)
            {
                var weakest = run.Gambler.Vices.Select((v, i) => (v, i)).OrderBy(p => ViceValue(p.v.Definition)).First();
                if (run.Rewards.Any(r => ViceValue(r) > ViceValue(weakest.v.Definition))) return RunInput.SellVice(weakest.i);
                return RunInput.SkipReward();
            }
            int best = 0;
            for (int i = 1; i < run.Rewards.Count; i++)
                if (ViceValue(run.Rewards[i]) > ViceValue(run.Rewards[best])) best = i;
            return RunInput.PickReward(best);
        }

        // plans a little like a player on the map: a stop's worth plus the best stop it leads to
        private static int ChooseRoom(Run run)
        {
            var stops = run.NextStops;
            int best = 0;
            double bestValue = double.MinValue;
            for (int i = 0; i < stops.Count; i++)
            {
                var stop = stops[i];
                var after = run.Map.NextFrom(stop.Section, stop.Row, stop.Lane);
                double value = StopValue(run, stop.Kind) + (after.Count > 0 ? after.Max(next => StopValue(run, next.Kind)) * 0.8 : 0);
                if (value > bestValue)
                {
                    best = i;
                    bestValue = value;
                }
            }
            return best;
        }

        private static double StopValue(Run run, RoomKind kind)
        {
            var g = run.Gambler;
            return kind switch
            {
                RoomKind.Shop => g.Gold >= 8 ? 6 : g.Gold >= 4 ? 3 : 0.5,
                RoomKind.SecurityOffice => g.Heat >= 50 ? 8 : 1,
                RoomKind.LostAndFound => g.HasFreeViceSlot ? 5 : 1,
                RoomKind.Bar => g.HasFreeCocktailSlot ? 4 : 1,
                RoomKind.CardRoom => 3,
                RoomKind.PawnShop => 2,
                RoomKind.BackRoom => 1,
                RoomKind.LoanShark => 0.5,
                _ => 0,
            };
        }

        private static RunInput ChooseInRoom(Run run)
        {
            var options = run.RoomOptions();
            int Find(RoomActionKind kind) => options.FindIndex(o => o.Kind == kind);
            RunInput? Try(int index) => index >= 0 && run.IsLegal(RunInput.RoomOption(index)) ? RunInput.RoomOption(index) : (RunInput?)null;

            switch (run.Room.Kind)
            {
                case RoomKind.LostAndFound:
                    return Try(Find(RoomActionKind.TakeLostVice)) ?? RunInput.LeaveRoom();
                case RoomKind.Bar:
                    return Try(Find(RoomActionKind.FreeCocktail)) ?? RunInput.LeaveRoom();
                case RoomKind.SecurityOffice:
                    return run.Gambler.Heat >= 30 ? Try(Find(RoomActionKind.PayOffHeat)) ?? RunInput.LeaveRoom() : RunInput.LeaveRoom();
                case RoomKind.CardRoom:
                    // thin out a low card, or a curse
                    int curse = run.Gambler.Deck.FindIndex(c => c.IsCursed);
                    int target = curse >= 0 ? curse : run.Gambler.Deck.FindIndex(c => !c.IsCursed && c.Rank == Rank.Two);
                    int remove = Find(RoomActionKind.RemoveCard);
                    if (target >= 0 && remove >= 0 && run.IsLegal(RunInput.RoomOption(remove, target))) return RunInput.RoomOption(remove, target);
                    return RunInput.LeaveRoom();
            }
            return RunInput.LeaveRoom();
        }
    }

    public sealed class SimulationReport
    {
        public int Runs;
        public int Wins;
        // [floor-1][stage] runs that reached / won that table
        public readonly int[,] Reached = new int[Balance.Floors, 3];
        public readonly int[,] Beat = new int[Balance.Floors, 3];
        // for the tables that were beaten: deals it took, and how far past the target the score went
        public readonly int[,] HandsToClear = new int[Balance.Floors, 3];
        public readonly double[,] Overshoot = new double[Balance.Floors, 3];
        public long GoldAtShops;
        public int ShopVisits;
        public int Busted;
        public long TotalScore;

        public double WinRate => Runs == 0 ? 0 : (double)Wins / Runs;
    }

    // plays many seeded runs with BalanceBot. Tools/BalanceSim prints the report
    public static class BalanceSimulation
    {
        public static SimulationReport Run(int runs, int firstSeed, Archetype? focus = null, RunOptions options = null)
        {
            var report = new SimulationReport();
            var bot = new BalanceBot(focus);
            for (int i = 0; i < runs; i++)
            {
                var run = new Run(firstSeed + i, options);
                int lastFloor = 0;
                var lastStage = TableKind.Low;
                bool counted = false;
                for (int guard = 0; guard < 5000 && !run.IsOver; guard++)
                {
                    if (run.Phase == RunPhase.AtTable && !run.AtEyeInTheSky && !run.AtEnforcer && run.Table.DealsPlayed == 0
                        && (!counted || run.Floor != lastFloor || run.Stage != lastStage))
                    {
                        report.Reached[run.Floor - 1, (int)run.Stage]++;
                        lastFloor = run.Floor;
                        lastStage = run.Stage;
                        counted = true;
                    }
                    var phase = run.Phase;
                    var table = run.Table;
                    run.Apply(bot.Choose(run));
                    if (phase == RunPhase.AtTable && table.Phase == TablePhase.Won && run.Table != table || phase == RunPhase.AtTable && run.Phase != RunPhase.AtTable && table.Phase == TablePhase.Won)
                    {
                        report.Beat[lastFloor - 1, (int)lastStage]++;
                        report.HandsToClear[lastFloor - 1, (int)lastStage] += table.DealsPlayed;
                        report.Overshoot[lastFloor - 1, (int)lastStage] += (double)table.Score / table.Target;
                    }
                    if (run.Phase == RunPhase.Shop && phase != RunPhase.Shop)
                    {
                        report.GoldAtShops += run.Gambler.Gold;
                        report.ShopVisits++;
                    }
                }
                report.Runs++;
                if (run.Phase == RunPhase.Won) report.Wins++;
                if (run.WasBusted) report.Busted++;
                report.TotalScore += run.TotalScore;
            }
            return report;
        }

        public static string Format(SimulationReport r, string title)
        {
            var lines = new List<string>
            {
                $"{title}: {r.Runs} runs, bot won {r.WinRate:P1}. Busted by security in {(double)r.Busted / r.Runs:P0} of runs. " +
                $"Avg gold entering a shop {(r.ShopVisits == 0 ? 0 : (double)r.GoldAtShops / r.ShopVisits):0.0}.",
                "| Floor | Low reached | Low win | High reached | High win | Boss reached | Boss win |",
                "| ----: | ----------: | ------: | -----------: | -------: | -----------: | -------: |",
            };
            for (int f = 0; f < Balance.Floors; f++)
            {
                string Cell(int stage) => r.Reached[f, stage] == 0 ? "-" : $"{(double)r.Beat[f, stage] / r.Reached[f, stage]:P0}";
                lines.Add($"| {f + 1} | {r.Reached[f, 0]} | {Cell(0)} | {r.Reached[f, 1]} | {Cell(1)} | {r.Reached[f, 2]} | {Cell(2)} |");
            }
            lines.Add("");
            lines.Add("| Floor | Low: deals to clear | Low: score / target | High: deals | High: score / target | Boss: deals | Boss: score / target |");
            lines.Add("| ----: | ------------------: | ------------------: | ----------: | -------------------: | ----------: | -------------------: |");
            for (int f = 0; f < Balance.Floors; f++)
            {
                string Deals(int stage) => r.Beat[f, stage] == 0 ? "-" : $"{(double)r.HandsToClear[f, stage] / r.Beat[f, stage]:0.0}";
                string Over(int stage) => r.Beat[f, stage] == 0 ? "-" : $"{r.Overshoot[f, stage] / r.Beat[f, stage]:0.0}x";
                lines.Add($"| {f + 1} | {Deals(0)} | {Over(0)} | {Deals(1)} | {Over(1)} | {Deals(2)} | {Over(2)} |");
            }
            return string.Join("\n", lines);
        }
    }
}
