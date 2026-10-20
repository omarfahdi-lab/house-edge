using System;
using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the blackjack table in the main area. reads a Table and turns button presses into PlayerActions
    public sealed class TableView
    {
        private static readonly (PlayerAction action, string label, string key)[] ActionButtons =
        {
            (PlayerAction.Deal, "Deal", "Space"),
            (PlayerAction.Hit, "Hit", "H"),
            (PlayerAction.Stand, "Stand", "S"),
            (PlayerAction.DoubleDown, "Double", "D"),
            (PlayerAction.Split, "Split", "P"),
            (PlayerAction.Burn, "Burn", "B"),
            (PlayerAction.Surrender, "Surrender", "R"),
            (PlayerAction.TakeInsurance, "Insure (2g)", "I"),
            (PlayerAction.DeclineInsurance, "No insurance", "N"),
        };

        private readonly VisualElement _root;
        private readonly Label _dealerLabel, _playerLabel, _message, _breakdown;
        private readonly VisualElement _dealerCards, _playerHands;
        private readonly Dictionary<PlayerAction, Button> _buttons = new Dictionary<PlayerAction, Button>();

        public event Action<PlayerAction> ActionChosen;

        public VisualElement Root => _root;

        public TableView(VisualElement parent)
        {
            _root = Ui.Column(parent);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            Ui.Padding(_root, 22, 24);
            _root.style.alignItems = Align.Center;
            _root.pickingMode = PickingMode.Ignore;

            // dealer
            var dealer = Ui.Column(_root, 8);
            dealer.style.alignItems = Align.Center;
            _dealerLabel = Ui.Text(dealer, "Dealer", 18, Ui.Dim);
            _dealerCards = Ui.Row(dealer, 10);
            _dealerCards.style.height = CardView.Height;

            // result
            var middle = Ui.Column(_root, 4);
            middle.style.alignItems = Align.Center;
            middle.style.flexGrow = 1;
            middle.style.justifyContent = Justify.Center;
            _message = Ui.Text(middle, "", 30, Ui.Gold, bold: true);
            _breakdown = Ui.Text(middle, "", 20, Ui.Cream);
            _breakdown.enableRichText = true;
            _breakdown.style.unityTextAlign = TextAnchor.MiddleCenter;
            _breakdown.style.whiteSpace = WhiteSpace.Normal;

            // player
            var player = Ui.Column(_root, 8);
            player.style.alignItems = Align.Center;
            _playerHands = Ui.Row(player, 36);
            _playerHands.style.minHeight = CardView.Height + 12;
            _playerLabel = Ui.Text(player, "", 16, Ui.Dim);

            // actions
            var actions = Ui.Row(_root, 8);
            actions.style.marginTop = 14;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.justifyContent = Justify.Center;
            foreach (var (action, label, key) in ActionButtons)
            {
                var chosen = action;
                var button = Ui.Button(actions, $"{label}  <size=12><color=#FFFFFF88>{key}</color></size>",
                    () => ActionChosen?.Invoke(chosen), accent: action == PlayerAction.Deal);
                button.enableRichText = true;
                _buttons[action] = button;
            }
        }

        public void Refresh(Table table)
        {
            RefreshDealer(table);
            RefreshPlayer(table);
            RefreshResult(table);

            var legal = table.LegalActions();
            bool insurance = table.Phase == TablePhase.InsuranceOffer;
            foreach (var pair in _buttons)
            {
                bool isInsurance = pair.Key == PlayerAction.TakeInsurance || pair.Key == PlayerAction.DeclineInsurance;
                Ui.Show(pair.Value, isInsurance == insurance);
                Ui.SetEnabled(pair.Value, legal.Contains(pair.Key));
            }
        }

        private void RefreshDealer(Table table)
        {
            _dealerCards.Clear();
            for (int i = 0; i < table.DealerCards.Count; i++)
            {
                bool hidden = i == 1 && table.HoleCardHidden;
                Ui.Add(_dealerCards, hidden ? CardView.CreateFaceDown() : CardView.Create(table.DealerCards[i]));
            }
            if (table.DealerCards.Count == 0) _dealerLabel.text = "Dealer";
            else if (table.HoleCardHidden) _dealerLabel.text = $"Dealer shows {table.DealerVisibleValue.Total}";
            else
            {
                var value = table.DealerValue;
                _dealerLabel.text = value.IsBust ? $"Dealer busts with {value.Total}" : $"Dealer has {value.Total}";
            }
        }

        private void RefreshPlayer(Table table)
        {
            _playerHands.Clear();
            var active = table.ActiveHand;
            foreach (var hand in table.PlayerHands)
            {
                var group = Ui.Column(_playerHands, 6);
                group.style.alignItems = Align.Center;
                Ui.Padding(group, 6, 8);
                Ui.Rounded(group, 10);
                bool highlight = active == hand && table.PlayerHands.Count > 1;
                Ui.Border(group, 2, highlight ? Ui.Gold : new Color(0, 0, 0, 0));

                var cards = Ui.Row(group);
                for (int i = 0; i < hand.Cards.Count; i++)
                {
                    var view = CardView.Create(hand.Cards[i]);
                    // overlap a little so five-card hands fit
                    if (i > 0) view.style.marginLeft = -18;
                    cards.Add(view);
                }

                var label = Ui.Text(group, Describe(hand), 16, Ui.Cream);
                if (hand.IsDoubled) label.text += "  x2";
            }

            _playerLabel.text = active == null ? "" : ActiveHint(table, active);
        }

        private static string Describe(PlayerHand hand)
        {
            var value = hand.Value;
            if (hand.IsBlackjack) return "Blackjack!";
            if (hand.RescuedAsPush) return $"Bust ({value.Total}), saved as a push";
            if (value.IsBust) return $"Bust ({value.Total})";
            if (value.IsCharlie) return "Charlie!";
            return value.IsSoft ? $"Soft {value.Total}" : value.Total.ToString();
        }

        private static string ActiveHint(Table table, PlayerHand hand)
        {
            if (hand.Value.IsBust) return "Bust! Burn the card to redraw it, or Stand to take the loss.";
            if (table.PlayerHands.Count > 1) return $"Playing hand {table.ActiveHandIndex + 1} of {table.PlayerHands.Count}";
            return "";
        }

        private void RefreshResult(Table table)
        {
            _message.style.color = Ui.Gold;
            if (table.Phase == TablePhase.InsuranceOffer)
            {
                _message.text = "Dealer shows an Ace";
                _breakdown.text = $"Insurance costs {Balance.InsuranceCost}g. If the dealer has blackjack, your hand becomes a push.";
                return;
            }
            if (table.LastScores.Count == 0)
            {
                bool fresh = table.Phase == TablePhase.ReadyToDeal;
                _message.text = fresh ? "Deal when you're ready" : "";
                _breakdown.text = fresh
                    ? $"Reach {table.Target:N0} points in {table.HandsLeft} Hands. Score = Chips x Mult."
                    : "";
                return;
            }

            long total = table.LastScores.Sum(s => s.Total);
            var best = table.LastScores.OrderByDescending(s => s.Total).First();
            _message.text = table.LastScores.Count > 1 ? $"Split hands score {total:N0}" : Headline(best);
            _message.style.color = total > 0 ? Ui.Gold : best.Outcome == Outcome.Push ? Ui.Cream : Ui.Bad;
            _breakdown.text = string.Join("\n", table.LastScores.Select(Breakdown));
        }

        private static string Headline(HandScore score) => score.Outcome switch
        {
            Outcome.Blackjack => "Blackjack!",
            Outcome.Charlie => "Five-card Charlie!",
            Outcome.TwentyOne => "Twenty-one!",
            Outcome.Win => score.DealerBusted ? "Dealer busts, you win!" : "You win!",
            Outcome.Push => "Push",
            Outcome.Bust => "Bust",
            Outcome.Surrender => $"Surrendered (-{Balance.SurrenderCost}g, Hand kept)",
            _ => "Dealer wins",
        };

        private static string Breakdown(HandScore score)
        {
            if (score.Outcome == Outcome.Surrender) return "";
            string chips = $"<color=#{ColorUtility.ToHtmlStringRGB(Ui.Chips)}>{score.Chips} Chips</color>";
            string mult = $"<color=#{ColorUtility.ToHtmlStringRGB(Ui.Mult)}>{score.Mult:0.##} Mult</color>";
            return $"{score.Outcome.Label()}: {chips} x {mult} = <b>{score.Total:N0}</b>";
        }
    }
}
