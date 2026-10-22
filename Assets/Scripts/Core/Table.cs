using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseEdge.Core
{
    // one blackjack table: deal hands until the score reaches the target or the Hands run out.
    // the UI only reads this and calls Apply with an action from LegalActions
    public sealed class Table
    {
        private readonly Shoe _shoe;
        private readonly List<TableEffect> _effects;
        private readonly List<PlayerHand> _hands = new List<PlayerHand>();
        private readonly List<Card> _dealerCards = new List<Card>();
        private readonly List<Card> _secondDealerCards = new List<Card>();
        private readonly List<HandScore> _lastScores = new List<HandScore>();
        private readonly List<PlayerAction> _inputs = new List<PlayerAction>();
        private readonly List<Vice> _jinxed = new List<Vice>();

        private bool _endTurnAfterCard;
        private bool _swapUsedThisDeal;
        private bool _coldReadUsedThisDeal;
        private bool _dealerControlUsedThisTable;
        private bool _dealerStopped;

        public TablePhase Phase { get; private set; } = TablePhase.ReadyToDeal;
        public long Target { get; }
        public long Score { get; private set; }
        public int HandsLeft { get; private set; }
        public int BurnsLeft { get; private set; }
        public TableRules Rules { get; } = new TableRules();

        public Gambler Gambler { get; }
        // the run's one RNG, for anything left to chance during the table
        public GameRandom Random { get; }
        public int Gold => Gambler.Gold;

        public IReadOnlyList<PlayerHand> PlayerHands => _hands;
        public int ActiveHandIndex { get; private set; }
        public PlayerHand ActiveHand => Phase == TablePhase.PlayerTurn ? _hands[ActiveHandIndex] : null;

        public IReadOnlyList<Card> DealerCards => _dealerCards;
        // the Twins' second hand, empty otherwise
        public IReadOnlyList<Card> SecondDealerCards => _secondDealerCards;
        public bool HoleCardHidden { get; private set; }
        public bool InsuranceTaken { get; private set; }
        public bool DealerBusted { get; private set; }

        // bets for the next (or current) deal
        public SideBet SideBet { get; private set; }
        public bool AntedUp { get; private set; }
        public SideBetResult? SideBetResult { get; private set; }
        public bool AnteLost { get; private set; }

        // scores from the last finished deal, one per player hand
        public IReadOnlyList<HandScore> LastScores => _lastScores;
        public IReadOnlyList<PlayerAction> Inputs => _inputs;
        public IReadOnlyList<TableEffect> Effects => _effects;
        public int CardsInShoe => _shoe.Count;
        public int HeatGained { get; private set; }

        // deals started at this table, including the current one
        public int DealsPlayed { get; private set; }

        // a standalone table with a fresh standard deck, for tests and quick play
        public Table(int seed, long target, IEnumerable<TableEffect> effects = null, int gold = Balance.StartingGold)
            : this(new Gambler(gold), new GameRandom(seed), target, effects)
        {
        }

        // a stacked shoe, for tests
        public Table(Shoe shoe, long target, IEnumerable<TableEffect> effects = null, int gold = Balance.StartingGold)
            : this(new Gambler(gold), shoe, new GameRandom(1), target, effects)
        {
        }

        // shuffles the gambler's deck into a new shoe
        public Table(Gambler gambler, GameRandom random, long target, IEnumerable<TableEffect> effects = null)
            : this(gambler, Shoe.Shuffled(gambler.Deck, random), random, target, effects)
        {
        }

        public Table(Gambler gambler, Shoe shoe, GameRandom random, long target, IEnumerable<TableEffect> effects = null)
        {
            Gambler = gambler;
            Random = random;
            _shoe = shoe;
            // cards score first, then Comps, then everything else in the order given
            _effects = new List<TableEffect> { new CardRules(), new CompRules() };
            if (effects != null) _effects.AddRange(effects);
            Target = target;
            HandsLeft = Balance.StartingHands;
            BurnsLeft = Balance.StartingBurns;

            foreach (var card in gambler.Deck) card.TimesDrawnThisTable = 0;
            if (gambler.HeatLevel >= HeatLevel.Watched) Rules.ReshuffleEveryDeals = Balance.WatchedReshuffleEvery;
            ArchetypeBonuses.Apply(this);
            foreach (var effect in ActiveEffects()) effect.OnTableStart(this);
        }

        public bool IsOver => Phase == TablePhase.Won || Phase == TablePhase.Lost;

        // the dealer's face-up card
        public Card DealerUpCard => _dealerCards.Count > 0 ? _dealerCards[0] : null;

        // the dealer's whole hand, hole card included. the dealer always busts over 21
        public HandValue DealerValue => HandValue.Of(_dealerCards);
        public HandValue SecondDealerValue => HandValue.Of(_secondDealerCards);

        // what the player can see of the dealer's hand
        public HandValue DealerVisibleValue => HoleCardHidden
            ? HandValue.Of(new[] { _dealerCards[0] })
            : HandValue.Of(_dealerCards);

        // the next card in the shoe, if something lets you see it (Marked cards, Card Counter, Cold Read)
        public Card NextCardPreview
        {
            get
            {
                var top = _shoe.PeekTop();
                if (top == null) return null;
                bool visible = Rules.SeeNextCard || top.Enhancement == Enhancement.Marked || Phase == TablePhase.DealerDecision;
                return visible ? top : null;
            }
        }

        public void AddHands(int count) => HandsLeft = Math.Max(0, HandsLeft + count);
        public void AddBurns(int count) => BurnsLeft = Math.Max(0, BurnsLeft + count);

        public int AddHeat(int amount)
        {
            int gained = Gambler.AddHeat(amount);
            HeatGained += gained;
            return gained;
        }

        // "chances in outOf", e.g. Roll(1, 4) for a 1 in 4 effect. luck bonuses add chances
        public bool Roll(int chances, int outOf)
        {
            int bonus = ArchetypeBonuses.ExtraChances(Gambler);
            bool rollTwice = false;
            foreach (var vice in Gambler.Vices)
            {
                if (vice.Disabled) continue;
                chances = vice.ModifyChances(chances);
                rollTwice |= vice.RollsTwice;
            }
            chances += bonus;
            bool hit = Random.Next(outOf) < chances;
            if (!hit && rollTwice) hit = Random.Next(outOf) < chances;
            return hit;
        }

        public int SideBetCost(SideBet bet)
        {
            int discount = Gambler.Vices.Where(v => !v.Disabled).Sum(v => v.SideBetDiscount);
            return Math.Max(0, SideBets.Cost(bet) - discount);
        }

        private IEnumerable<TableEffect> ActiveEffects()
        {
            // a copy, so an effect can disable a Vice while the others run
            foreach (var effect in _effects.ToArray())
                if (effect.IsActive) yield return effect;
        }

        private bool HasActiveVice(Func<Vice, bool> test) =>
            _effects.OfType<Vice>().Any(v => v.IsActive && test(v));

        public List<PlayerAction> LegalActions()
        {
            var actions = new List<PlayerAction>();
            switch (Phase)
            {
                case TablePhase.ReadyToDeal:
                    actions.Add(PlayerAction.Deal);
                    if (!AntedUp) actions.Add(PlayerAction.AnteUp);
                    if (SideBet == SideBet.None)
                        foreach (var bet in SideBets.All)
                            if (Gambler.CanAfford(SideBetCost(bet))) actions.Add(SideBets.ActionFor(bet));
                    break;
                case TablePhase.InsuranceOffer:
                    actions.Add(PlayerAction.TakeInsurance);
                    actions.Add(PlayerAction.DeclineInsurance);
                    break;
                case TablePhase.DealerDecision:
                    actions.Add(PlayerAction.LetDealerDraw);
                    actions.Add(PlayerAction.StopDealer);
                    break;
                case TablePhase.PlayerTurn:
                    var hand = ActiveHand;
                    if (hand.Value.IsBust)
                    {
                        // a hit just busted and a Burn could still save it
                        if (CanBurn(hand)) actions.Add(PlayerAction.Burn);
                        actions.Add(PlayerAction.Stand);
                        break;
                    }
                    if (Gambler.CanAfford(Rules.HitCost)) actions.Add(PlayerAction.Hit);
                    // Bad Habit: you have to hit at least once (unless you can't)
                    bool mustHit = !hand.HasActed && HasActiveVice(v => v.MustHitEveryHand) && actions.Contains(PlayerAction.Hit);
                    if (!mustHit) actions.Add(PlayerAction.Stand);
                    if (CanDoubleDown(hand)) actions.Add(PlayerAction.DoubleDown);
                    if (CanSplit(hand)) actions.Add(PlayerAction.Split);
                    if (CanBurn(hand)) actions.Add(PlayerAction.Burn);
                    if (CanSurrender(hand)) actions.Add(PlayerAction.Surrender);
                    if (CanSwap(hand)) actions.Add(PlayerAction.SwapWithDealer);
                    break;
            }
            return actions;
        }

        public bool IsLegal(PlayerAction action) => LegalActions().Contains(action);

        public void Apply(PlayerAction action)
        {
            if (!IsLegal(action)) throw new InvalidOperationException($"{action} isn't allowed during {Phase}.");
            _inputs.Add(action);

            switch (action)
            {
                case PlayerAction.Deal: Deal(); break;
                case PlayerAction.AnteUp: AntedUp = true; break;
                case PlayerAction.BetPerfectPair:
                case PlayerAction.Bet21Plus3:
                case PlayerAction.BetBustIt:
                case PlayerAction.BetLuckyLadies:
                case PlayerAction.BetHotHand:
                    SideBet = SideBets.BetFor(action);
                    Gambler.LoseGold(SideBetCost(SideBet));
                    break;
                case PlayerAction.TakeInsurance:
                    Gambler.LoseGold(Balance.InsuranceCost);
                    InsuranceTaken = true;
                    AfterInsurance();
                    break;
                case PlayerAction.DeclineInsurance: AfterInsurance(); break;
                case PlayerAction.Hit: Hit(ActiveHand); break;
                case PlayerAction.Stand: Stand(ActiveHand); break;
                case PlayerAction.DoubleDown: DoubleDown(ActiveHand); break;
                case PlayerAction.Split: Split(ActiveHand); break;
                case PlayerAction.Burn: Burn(ActiveHand); break;
                case PlayerAction.Surrender: Surrender(); break;
                case PlayerAction.SwapWithDealer: SwapWithDealer(ActiveHand); break;
                case PlayerAction.LetDealerDraw:
                    DrawForDealer();
                    PlayDealer();
                    break;
                case PlayerAction.StopDealer:
                    _dealerStopped = true;
                    PlayDealer();
                    break;
            }
        }

        private bool CanDoubleDown(PlayerHand hand) =>
            !hand.IsDoubled && (hand.Cards.Count == 2 && !hand.HasActed || Rules.DoubleAfterHit && hand.Cards.Count >= 2);

        private bool CanSplit(PlayerHand hand) =>
            hand.Cards.Count == 2 && !hand.HasActed && _hands.Count <= Balance.MaxSplitsPerHand
            && (Rules.SplitAnyTwo || !hand.Cards[0].IsCursed && hand.Cards[0].Rank == hand.Cards[1].Rank && !hand.Cards[1].IsCursed);

        private bool CanBurn(PlayerHand hand) => hand.LastCardWasHit && BurnsLeft > 0 && !Rules.BurnsDisabled;

        private bool CanSurrender(PlayerHand hand) =>
            _hands.Count == 1 && hand.Cards.Count == 2 && !hand.HasActed && Gambler.CanAfford(Balance.SurrenderCost);

        private bool CanSwap(PlayerHand hand) => !_swapUsedThisDeal && HasActiveVice(v => v.GrantsSwap);

        // things a boss (or anyone at the table) says out loud this deal, for the game to show
        public IReadOnlyList<string> Announcements => _announcements;
        // winning deals in a row at this table
        public int WinStreak { get; private set; }
        private readonly List<string> _announcements = new List<string>();
        private bool _dealerRedrawUsed;

        public void Announce(string text) => _announcements.Add(text);

        private void Deal()
        {
            ClearTable();
            _announcements.Clear();
            _dealerRedrawUsed = false;
            foreach (var effect in ActiveEffects()) effect.OnBeforeDeal(this);
            if (Rules.ReshuffleEveryDeals > 0 && DealsPlayed > 0 && DealsPlayed % Rules.ReshuffleEveryDeals == 0)
                _shoe.ReshuffleDiscards();
            DealsPlayed++;
            var hand = new PlayerHand(false, Rules);
            _hands.Add(hand);
            HoleCardHidden = !Rules.HoleCardFaceUp;

            DrawFor(hand);
            DrawForDealer();
            DrawFor(hand);
            DrawForDealer();

            foreach (var effect in ActiveEffects()) effect.OnDeal(this);

            if (SideBet != SideBet.None && SideBet != SideBet.BustIt && SideBet != SideBet.HotHand)
                PaySideBet(SideBets.SettleOnDeal(SideBet, hand.Cards, DealerUpCard));

            if (DealerUpCard.IsAce && Gambler.CanAfford(Balance.InsuranceCost))
            {
                Phase = TablePhase.InsuranceOffer;
                return;
            }
            AfterInsurance();
        }

        private void PaySideBet(SideBetResult result)
        {
            SideBetResult = result;
            if (result.Payout > 0) Gambler.GainGold(result.Payout);
        }

        private void ClearTable()
        {
            foreach (var hand in _hands) Discard(hand.Cards);
            Discard(_dealerCards);
            Discard(_secondDealerCards);
            _hands.Clear();
            _dealerCards.Clear();
            _secondDealerCards.Clear();
            _lastScores.Clear();
            foreach (var vice in _jinxed) vice.Disabled = false;
            _jinxed.Clear();
            ActiveHandIndex = 0;
            InsuranceTaken = false;
            DealerBusted = false;
            SideBetResult = null;
            AnteLost = false;
            _swapUsedThisDeal = false;
            _coldReadUsedThisDeal = false;
            _dealerStopped = false;
            _endTurnAfterCard = false;
        }

        private void Discard(IEnumerable<Card> cards)
        {
            foreach (var card in cards)
                if (!card.Destroyed) _shoe.Discard(card);
        }

        private void AfterInsurance()
        {
            var first = _hands[0];
            if (DealerRules.PeeksFor(DealerUpCard) && DealerValue.IsTwoCard21)
            {
                ResolveDeal(dealerBlackjack: true);
                return;
            }
            Phase = TablePhase.PlayerTurn;
            ActiveHandIndex = 0;
            // a blackjack, Snake Eyes, or two cards already over a lowered bust limit
            if (first.IsBlackjack || _endTurnAfterCard || first.Value.IsBust)
            {
                _endTurnAfterCard = false;
                EndHand(first);
            }
        }

        private void Hit(PlayerHand hand)
        {
            Gambler.LoseGold(Rules.HitCost);
            hand.HasActed = true;
            hand.LastCardWasHit = true;
            DrawFor(hand);
            foreach (var effect in ActiveEffects()) effect.OnHit(this, hand);
            AfterCardTaken(hand);
        }

        private void Burn(PlayerHand hand)
        {
            Discard(new[] { hand.RemoveLast() });
            BurnsLeft--;
            hand.BurnsUsed++;
            DrawFor(hand);
            foreach (var effect in ActiveEffects()) effect.OnBurn(this, hand);
            AfterCardTaken(hand);
        }

        private void AfterCardTaken(PlayerHand hand)
        {
            var value = hand.Value;
            // a bust waits for the player while a Burn could still save it
            bool canStillBurn = value.IsBust && CanBurn(hand);
            bool snakeEyes = _endTurnAfterCard;
            _endTurnAfterCard = false;
            if ((value.IsBust && !canStillBurn) || value.IsCharlie || (snakeEyes && !value.IsBust)) EndHand(hand);
        }

        private void Stand(PlayerHand hand)
        {
            hand.StoodOn = hand.Value.Total;
            if (!hand.Value.IsBust)
                foreach (var effect in ActiveEffects()) effect.OnStand(this, hand);
            EndHand(hand);
        }

        private void DoubleDown(PlayerHand hand)
        {
            hand.HasActed = true;
            hand.IsDoubled = true;
            hand.LastCardWasHit = false;
            DrawFor(hand);
            _endTurnAfterCard = false;
            EndHand(hand);
        }

        private void Split(PlayerHand hand)
        {
            var second = new PlayerHand(true, Rules);
            second.Add(hand.RemoveLast());
            hand.IsFromSplit = true;
            _hands.Insert(ActiveHandIndex + 1, second);
            DrawFor(hand);
            DrawFor(second);
            _endTurnAfterCard = false;
        }

        private void Surrender()
        {
            Gambler.LoseGold(Balance.SurrenderCost);
            HoleCardHidden = false;
            var hand = _hands[0];
            hand.IsDone = true;
            hand.Score = new HandScore(hand, Outcome.Surrender, false);
            _lastScores.Add(hand.Score);
            // surrendering doesn't use up a Hand, and side bets already paid out
            SideBet = SideBet.None;
            AntedUp = false;
            Phase = TablePhase.ReadyToDeal;
        }

        // the Card Mechanic: your last card for the dealer's face-up card
        private void SwapWithDealer(PlayerHand hand)
        {
            _swapUsedThisDeal = true;
            SwapWithDealerUpCard(hand, hand.Cards.Count - 1);
            AddHeat(Balance.CardMechanicHeat);
            AfterCardTaken(hand);
        }

        public void SwapWithDealerUpCard(PlayerHand hand, int cardIndex)
        {
            var mine = hand.Cards[cardIndex];
            hand.Replace(cardIndex, _dealerCards[0]);
            _dealerCards[0] = mine;
        }

        private void EndHand(PlayerHand hand)
        {
            if (hand.Value.IsBust)
                foreach (var effect in ActiveEffects())
                {
                    effect.OnBust(this, hand);
                    if (!hand.Value.IsBust || hand.RescuedAsPush) break;
                }
            hand.IsDone = true;
            for (int i = ActiveHandIndex + 1; i < _hands.Count; i++)
            {
                if (_hands[i].IsDone) continue;
                ActiveHandIndex = i;
                return;
            }
            FinishPlayerTurn();
        }

        private void FinishPlayerTurn()
        {
            HoleCardHidden = false;
            if (DealerNeedsToPlay()) PlayDealer();
            else ResolveDeal(dealerBlackjack: false);
        }

        // the dealer draws to 17 unless a Vice hands you the decision, then the Twins play their second hand
        private void PlayDealer()
        {
            Phase = TablePhase.PlayerTurn;
            while (!_dealerStopped && DealerShouldHit())
            {
                if (OfferDealerDecision()) return;
                DrawForDealer();
                if (Rules.DealerRedrawsFirstBust && !_dealerRedrawUsed && DealerValue.IsBust)
                {
                    // The Cheat palms the card that busted him and tries again
                    _dealerRedrawUsed = true;
                    var palmed = _dealerCards[_dealerCards.Count - 1];
                    _dealerCards.RemoveAt(_dealerCards.Count - 1);
                    _shoe.Discard(palmed);
                    Announce($"Palms the {palmed.RankLabel} that would have busted him, and draws again.");
                }
            }

            if (Rules.DealerPlaysTwoHands && _secondDealerCards.Count == 0)
            {
                DrawForSecondDealer();
                DrawForSecondDealer();
                while (DealerRules.ShouldHit(SecondDealerValue, Rules)) DrawForSecondDealer();
            }

            bool busted = DealerValue.IsBust && (!Rules.DealerPlaysTwoHands || SecondDealerValue.IsBust);
            if (busted)
            {
                DealerBusted = true;
                foreach (var effect in ActiveEffects()) effect.OnDealerBust(this);
                if (Rules.ReshuffleAfterDealerBust) _shoe.ReshuffleDiscards();
            }
            ResolveDeal(dealerBlackjack: false);
        }

        private bool DealerShouldHit()
        {
            var value = DealerValue;
            if (DealerRules.ShouldHit(value, Rules)) return true;
            // Rookie Ray sometimes hits a hard 17 by mistake
            return Rules.DealerMistakeOneIn > 0 && value.Total == Balance.DealerStandsOn && !value.IsSoft
                   && Random.Next(Rules.DealerMistakeOneIn) == 0;
        }

        // Cold Read (once a deal) or The Dealer (once a table) let you decide the dealer's next card
        private bool OfferDealerDecision()
        {
            bool coldRead = !_coldReadUsedThisDeal && HasActiveVice(v => v.DealerControl == DealerControl.ColdRead);
            bool fullControl = !_dealerControlUsedThisTable && HasActiveVice(v => v.DealerControl == DealerControl.Full);
            if (!coldRead && !fullControl) return false;
            if (coldRead)
            {
                _coldReadUsedThisDeal = true;
                AddHeat(Balance.ColdReadHeat);
            }
            else
            {
                _dealerControlUsedThisTable = true;
                AddHeat(Balance.TheDealerHeat);
            }
            Phase = TablePhase.DealerDecision;
            return true;
        }

        // busts, blackjacks and Charlies are settled without the dealer drawing
        private bool DealerNeedsToPlay()
        {
            if (SideBet == SideBet.BustIt) return true;
            foreach (var hand in _hands)
            {
                var value = hand.Value;
                if (!value.IsBust && !value.IsCharlie && !hand.IsBlackjack && !hand.RescuedAsPush) return true;
            }
            return false;
        }

        private void ResolveDeal(bool dealerBlackjack)
        {
            HoleCardHidden = false;
            if (SideBet == SideBet.BustIt) PaySideBet(SideBets.SettleBustIt(DealerBusted, _dealerCards.Count));

            bool anyWin = false;
            bool anyNotLost = false;
            foreach (var hand in _hands)
            {
                hand.IsDone = true;
                var outcome = DecideOutcome(hand, dealerBlackjack);
                hand.Score = ScoreHand(hand, outcome);
                _lastScores.Add(hand.Score);
                Score += hand.Score.Total;
                anyWin |= outcome.IsWin();
                anyNotLost |= outcome != Outcome.Lose && outcome != Outcome.Bust;
                if ((double)hand.Score.Total > (double)Target * Balance.BigHandFactor) AddHeat(Balance.BigHandHeat);
            }
            if (SideBet == SideBet.HotHand)
            {
                SideBetResult = new SideBetResult(SideBet.HotHand, anyWin ? 2 : 0, anyWin ? "Hot hand!" : "Cold hand");
                if (anyWin) Gambler.GainGold(2);
            }
            if (AntedUp && !anyNotLost)
            {
                AnteLost = true;
                Gambler.LoseGold(Balance.AnteUpLoss);
            }

            // wins in a row build a streak; a loss breaks it, a push leaves it alone
            bool anyLoss = _lastScores.Any(s => s.Outcome == Outcome.Lose || s.Outcome == Outcome.Bust);
            if (anyWin) WinStreak++;
            else if (anyLoss) WinStreak = 0;

            foreach (var effect in ActiveEffects()) effect.OnDealEnd(this);

            SideBet = SideBet.None;
            AntedUp = false;
            HandsLeft--;
            if (Score >= Target) EndTable(true);
            else if (HandsLeft <= 0) EndTable(false);
            else Phase = TablePhase.ReadyToDeal;
        }

        private Outcome DecideOutcome(PlayerHand hand, bool dealerBlackjack)
        {
            var value = hand.Value;
            if (hand.RescuedAsPush) return Outcome.Push;
            if (value.IsBust) return Outcome.Bust;
            if (dealerBlackjack)
            {
                if (InsuranceTaken) return Outcome.Push;
                if (hand.IsBlackjack)
                    return HasActiveVice(v => v.BlackjacksCantBeTied) ? Outcome.Blackjack : ApplyPushRule(Outcome.Push, Outcome.Blackjack);
                return Outcome.Lose;
            }
            if (value.IsCharlie) return Outcome.Charlie;
            if (hand.IsBlackjack) return Outcome.Blackjack;

            var winAs = value.Total == Balance.BustLimit && value.CardCount >= 3 ? Outcome.TwentyOne : Outcome.Win;
            var result = Compare(value, DealerValue);
            if (Rules.DealerPlaysTwoHands)
            {
                var second = Compare(value, SecondDealerValue);
                // you have to beat both: the worse result counts
                if (second < result) result = second;
            }
            return result switch
            {
                > 0 => winAs,
                0 => ApplyPushRule(Outcome.Push, winAs),
                _ => Outcome.Lose,
            };
        }

        // 1 = player wins, 0 = tie, -1 = dealer wins
        private static int Compare(HandValue player, HandValue dealer)
        {
            if (dealer.IsBust || player.Total > dealer.Total) return 1;
            return player.Total == dealer.Total ? 0 : -1;
        }

        private Outcome ApplyPushRule(Outcome push, Outcome win) => Rules.Pushes switch
        {
            PushRule.DealerWins => Outcome.Lose,
            PushRule.PlayerWins => win,
            _ => push,
        };

        private HandScore ScoreHand(PlayerHand hand, Outcome outcome)
        {
            bool win = outcome.IsWin();
            var score = new HandScore(hand, outcome, DealerBusted && win);
            if (Balance.ScoresCards(outcome))
            {
                score.AddChips(Math.Min(hand.Value.Total, Rules.PlayerBustLimit));
                score.Mark(ScoreSource.Hand, "Your cards");
                score.AddChips(Balance.BaseChips(outcome));
                score.AddMult(Balance.BaseMult(outcome));
                score.Mark(ScoreSource.Bonus, outcome.Label());
            }
            if (score.DealerBusted)
            {
                score.AddChips(Balance.DealerBustBonusChips);
                score.AddMult(Balance.DealerBustBonusMult);
                score.Mark(ScoreSource.Bonus, "Dealer bust");
            }
            if (SideBet == SideBet.HotHand && win) score.AddMult(Balance.HotHandMult);
            score.Mark(ScoreSource.Bonus, "Hot Hand");

            // combos only pay when you beat the dealer
            if (win)
            {
                var combos = Combos.Find(hand.Cards);
                score.Combos = combos;
                foreach (var combo in combos)
                {
                    var (chips, mult) = Combos.Base(combo);
                    score.AddChips(chips);
                    score.AddMult(mult);
                    score.Mark(ScoreSource.Combo, Combos.Name(combo), combo);
                }
                int streak = System.Math.Min(WinStreak * Balance.StreakMultPerWin, Balance.MaxStreakBonus);
                score.AddMult(streak);
                score.Mark(ScoreSource.Streak, $"{WinStreak + 1} wins in a row");
            }

            foreach (var effect in ActiveEffects())
            {
                int chips = score.Chips;
                double mult = score.Mult;
                effect.OnHandScored(this, score);
                if (effect is Vice && (score.Chips != chips || score.Mult != mult)) score.ViceTriggers++;
                MarkEffect(score, effect);
            }
            foreach (var effect in ActiveEffects())
            {
                effect.OnAfterScoring(this, score);
                MarkEffect(score, effect);
            }

            // applied after the effects so they double the whole hand
            if (hand.IsDoubled && win) score.MultiplyMult(Balance.DoubleDownMult);
            score.Mark(ScoreSource.Double, "Double Down");
            if (AntedUp && score.ScoresCards) score.MultiplyMult(Balance.AnteUpMult);
            score.Mark(ScoreSource.Ante, "Ante Up");
            return score;
        }

        private static void MarkEffect(HandScore score, TableEffect effect)
        {
            switch (effect)
            {
                case Vice vice: score.Mark(ScoreSource.Vice, vice.Name, vice); break;
                case Boss boss: score.Mark(ScoreSource.Boss, boss.Name, boss); break;
                case Dealer dealer: score.Mark(ScoreSource.Dealer, dealer.Name, dealer); break;
                default: score.Mark(ScoreSource.Rule, "House rules", effect); break;
            }
        }

        private void EndTable(bool won)
        {
            Phase = won ? TablePhase.Won : TablePhase.Lost;
            foreach (var effect in ActiveEffects()) effect.OnTableEnd(this, won);
        }

        // effects

        // Snake Eyes: the hand that just drew stops
        public void EndTurnAfterThisCard() => _endTurnAfterCard = true;

        // a Fragile card broke, or something took it: out of the deck for good
        public void DestroyCard(Card card)
        {
            card.Destroyed = true;
            Gambler.Deck.Remove(card);
        }

        // shuffle a card into what's left of the shoe (and keep it in the deck for later tables)
        public void ShuffleIntoShoe(Card card, bool addToDeck)
        {
            _shoe.InsertRandom(card);
            if (addToDeck) Gambler.Deck.Add(card);
        }

        // the Enforcer: a random Vice is gone for the rest of the run
        public void TakeRandomVice()
        {
            if (Gambler.Vices.Count == 0) return;
            var vice = Gambler.Vices[Random.Next(Gambler.Vices.Count)];
            Gambler.Vices.Remove(vice);
            vice.Disabled = true;
            _effects.Remove(vice);
        }

        // Jinx: a random Vice sits out this hand
        public void JinxRandomVice()
        {
            var candidates = _effects.OfType<Vice>().Where(v => v.IsActive).ToList();
            if (candidates.Count == 0) return;
            var vice = candidates[Random.Next(candidates.Count)];
            vice.Disabled = true;
            _jinxed.Add(vice);
        }

        private void DrawFor(PlayerHand hand)
        {
            var card = _shoe.Draw();
            hand.Add(card);
            foreach (var effect in ActiveEffects()) effect.OnCardDrawn(this, card, false);
        }

        // the dealer never draws a Black Stamp: it goes to the bottom of the shoe
        private Card DrawDealerCard()
        {
            var card = _shoe.Draw();
            for (int tries = 0; card.Stamp == Stamp.Black && tries <= _shoe.Count; tries++)
            {
                _shoe.PutBottom(card);
                card = _shoe.Draw();
            }
            return card;
        }

        private void DrawForDealer()
        {
            var card = DrawDealerCard();
            _dealerCards.Add(card);
            foreach (var effect in ActiveEffects()) effect.OnCardDrawn(this, card, true);
        }

        private void DrawForSecondDealer()
        {
            var card = DrawDealerCard();
            _secondDealerCards.Add(card);
            foreach (var effect in ActiveEffects()) effect.OnCardDrawn(this, card, true);
        }
    }
}
