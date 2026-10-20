using System;
using System.Collections.Generic;

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
        private readonly List<HandScore> _lastScores = new List<HandScore>();
        private readonly List<PlayerAction> _inputs = new List<PlayerAction>();

        public TablePhase Phase { get; private set; } = TablePhase.ReadyToDeal;
        public long Target { get; }
        public long Score { get; private set; }
        public int HandsLeft { get; private set; }
        public int BurnsLeft { get; private set; }

        public IReadOnlyList<PlayerHand> PlayerHands => _hands;
        public int ActiveHandIndex { get; private set; }
        public PlayerHand ActiveHand => Phase == TablePhase.PlayerTurn ? _hands[ActiveHandIndex] : null;

        public IReadOnlyList<Card> DealerCards => _dealerCards;
        public bool HoleCardHidden { get; private set; }
        public bool InsuranceTaken { get; private set; }
        public bool DealerBusted { get; private set; }

        // scores from the last finished deal, one per player hand
        public IReadOnlyList<HandScore> LastScores => _lastScores;
        public IReadOnlyList<PlayerAction> Inputs => _inputs;
        public IReadOnlyList<TableEffect> Effects => _effects;
        public int CardsInShoe => _shoe.Count;

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
            _effects = effects == null ? new List<TableEffect>() : new List<TableEffect>(effects);
            Target = target;
            HandsLeft = Balance.StartingHands;
            BurnsLeft = Balance.StartingBurns;
            foreach (var effect in ActiveEffects()) effect.OnTableStart(this);
        }

        public Gambler Gambler { get; }
        // the run's one RNG, for anything left to chance during the table
        public GameRandom Random { get; }
        public int Gold => Gambler.Gold;

        public bool IsOver => Phase == TablePhase.Won || Phase == TablePhase.Lost;

        // deals started at this table, including the current one
        public int DealsPlayed { get; private set; }

        // the dealer's whole hand, hole card included
        public HandValue DealerValue => HandValue.Of(_dealerCards);

        public void AddHands(int count) => HandsLeft = Math.Max(0, HandsLeft + count);
        public void AddBurns(int count) => BurnsLeft = Math.Max(0, BurnsLeft + count);

        // "chances in outOf", e.g. Roll(1, 4) for a 1 in 4 effect
        public bool Roll(int chances, int outOf) => Random.Next(outOf) < chances;

        private IEnumerable<TableEffect> ActiveEffects()
        {
            foreach (var effect in _effects)
                if (effect.IsActive) yield return effect;
        }

        // the dealer's face-up card
        public Card DealerUpCard => _dealerCards.Count > 0 ? _dealerCards[0] : null;

        // what the player can see of the dealer's hand
        public HandValue DealerVisibleValue => HoleCardHidden
            ? HandValue.Of(new[] { _dealerCards[0] })
            : HandValue.Of(_dealerCards);

        public List<PlayerAction> LegalActions()
        {
            var actions = new List<PlayerAction>();
            switch (Phase)
            {
                case TablePhase.ReadyToDeal:
                    actions.Add(PlayerAction.Deal);
                    break;
                case TablePhase.InsuranceOffer:
                    actions.Add(PlayerAction.TakeInsurance);
                    actions.Add(PlayerAction.DeclineInsurance);
                    break;
                case TablePhase.PlayerTurn:
                    var hand = ActiveHand;
                    if (hand.Value.IsBust)
                    {
                        // a hit just busted and a Burn could still save it
                        actions.Add(PlayerAction.Burn);
                        actions.Add(PlayerAction.Stand);
                        break;
                    }
                    actions.Add(PlayerAction.Hit);
                    actions.Add(PlayerAction.Stand);
                    if (CanDoubleDown(hand)) actions.Add(PlayerAction.DoubleDown);
                    if (CanSplit(hand)) actions.Add(PlayerAction.Split);
                    if (CanBurn(hand)) actions.Add(PlayerAction.Burn);
                    if (CanSurrender(hand)) actions.Add(PlayerAction.Surrender);
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
            }
        }

        private bool CanDoubleDown(PlayerHand hand) => hand.Cards.Count == 2 && !hand.HasActed;

        private bool CanSplit(PlayerHand hand) =>
            hand.Cards.Count == 2 && !hand.HasActed && _hands.Count <= Balance.MaxSplitsPerHand
            && hand.Cards[0].Rank == hand.Cards[1].Rank;

        private bool CanBurn(PlayerHand hand) => hand.LastCardWasHit && BurnsLeft > 0;

        private bool CanSurrender(PlayerHand hand) =>
            _hands.Count == 1 && hand.Cards.Count == 2 && !hand.HasActed && Gambler.CanAfford(Balance.SurrenderCost);

        private void Deal()
        {
            ClearTable();
            DealsPlayed++;
            var hand = new PlayerHand(false);
            _hands.Add(hand);
            HoleCardHidden = true;

            DrawFor(hand);
            DrawForDealer();
            DrawFor(hand);
            DrawForDealer();

            if (DealerUpCard.IsAce && Gambler.CanAfford(Balance.InsuranceCost))
            {
                Phase = TablePhase.InsuranceOffer;
                return;
            }
            AfterInsurance();
        }

        private void ClearTable()
        {
            foreach (var hand in _hands) _shoe.Discard(hand.Cards);
            _shoe.Discard(_dealerCards);
            _hands.Clear();
            _dealerCards.Clear();
            _lastScores.Clear();
            ActiveHandIndex = 0;
            InsuranceTaken = false;
            DealerBusted = false;
        }

        private void AfterInsurance()
        {
            if (DealerRules.PeeksFor(DealerUpCard) && HandValue.Of(_dealerCards).IsTwoCard21)
            {
                ResolveDeal(dealerBlackjack: true);
                return;
            }
            if (_hands[0].IsBlackjack)
            {
                _hands[0].IsDone = true;
                FinishPlayerTurn();
                return;
            }
            Phase = TablePhase.PlayerTurn;
            ActiveHandIndex = 0;
        }

        private void Hit(PlayerHand hand)
        {
            hand.HasActed = true;
            hand.LastCardWasHit = true;
            DrawFor(hand);
            foreach (var effect in ActiveEffects()) effect.OnHit(this, hand);
            AfterCardTaken(hand);
        }

        private void Burn(PlayerHand hand)
        {
            _shoe.Discard(hand.RemoveLast());
            BurnsLeft--;
            DrawFor(hand);
            AfterCardTaken(hand);
        }

        private void AfterCardTaken(PlayerHand hand)
        {
            var value = hand.Value;
            // a bust waits for the player while a Burn could still save it
            bool canStillBurn = value.IsBust && CanBurn(hand);
            if ((value.IsBust && !canStillBurn) || value.IsCharlie) EndHand(hand);
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
            EndHand(hand);
        }

        private void Split(PlayerHand hand)
        {
            var second = new PlayerHand(true);
            second.Add(hand.RemoveLast());
            hand.IsFromSplit = true;
            _hands.Insert(ActiveHandIndex + 1, second);
            DrawFor(hand);
            DrawFor(second);
        }

        private void Surrender()
        {
            Gambler.LoseGold(Balance.SurrenderCost);
            HoleCardHidden = false;
            var hand = _hands[0];
            hand.IsDone = true;
            hand.Score = new HandScore(hand, Outcome.Surrender, false);
            _lastScores.Add(hand.Score);
            // surrendering doesn't use up a Hand
            Phase = TablePhase.ReadyToDeal;
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
            if (DealerNeedsToPlay())
            {
                while (DealerRules.ShouldHit(HandValue.Of(_dealerCards))) DrawForDealer();
                if (HandValue.Of(_dealerCards).IsBust)
                {
                    DealerBusted = true;
                    foreach (var effect in ActiveEffects()) effect.OnDealerBust(this);
                }
            }
            ResolveDeal(dealerBlackjack: false);
        }

        // busts, blackjacks and Charlies are settled without the dealer drawing
        private bool DealerNeedsToPlay()
        {
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
            foreach (var hand in _hands)
            {
                hand.IsDone = true;
                hand.Score = ScoreHand(hand, DecideOutcome(hand, dealerBlackjack));
                _lastScores.Add(hand.Score);
                Score += hand.Score.Total;
            }

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
            if (dealerBlackjack) return hand.IsBlackjack || InsuranceTaken ? Outcome.Push : Outcome.Lose;
            if (value.IsCharlie) return Outcome.Charlie;
            if (hand.IsBlackjack) return Outcome.Blackjack;

            int dealerTotal = HandValue.Of(_dealerCards).Total;
            if (DealerBusted || value.Total > dealerTotal)
                return value.Total == Balance.BustLimit && value.CardCount >= 3 ? Outcome.TwentyOne : Outcome.Win;
            return value.Total == dealerTotal ? Outcome.Push : Outcome.Lose;
        }

        private HandScore ScoreHand(PlayerHand hand, Outcome outcome)
        {
            bool win = outcome.IsWin();
            var score = new HandScore(hand, outcome, DealerBusted && win);
            if (Balance.ScoresCards(outcome))
            {
                score.AddChips(Math.Min(hand.Value.Total, Balance.BustLimit) + Balance.BaseChips(outcome));
                score.AddMult(Balance.BaseMult(outcome));
            }
            if (score.DealerBusted)
            {
                score.AddChips(Balance.DealerBustBonusChips);
                score.AddMult(Balance.DealerBustBonusMult);
            }

            foreach (var effect in ActiveEffects()) effect.OnHandScored(this, score);

            // applied after the effects so it doubles the whole hand
            if (hand.IsDoubled && win) score.MultiplyMult(Balance.DoubleDownMult);
            return score;
        }

        private void EndTable(bool won)
        {
            Phase = won ? TablePhase.Won : TablePhase.Lost;
            foreach (var effect in ActiveEffects()) effect.OnTableEnd(this, won);
        }

        private void DrawFor(PlayerHand hand)
        {
            var card = _shoe.Draw();
            hand.Add(card);
            foreach (var effect in ActiveEffects()) effect.OnCardDrawn(this, card, false);
        }

        private void DrawForDealer()
        {
            var card = _shoe.Draw();
            _dealerCards.Add(card);
            foreach (var effect in ActiveEffects()) effect.OnCardDrawn(this, card, true);
        }
    }
}
