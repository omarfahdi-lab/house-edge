using System;
using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the blackjack table. reads a Table and turns button presses into PlayerActions
    public sealed class TableScreen
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

        private readonly Label _score, _target, _hands, _burns, _gold;
        private readonly VisualElement _progressFill;
        private readonly Label _dealerLabel, _playerLabel, _message, _breakdown;
        private readonly VisualElement _dealerCards, _playerHands;
        private readonly Dictionary<PlayerAction, Button> _buttons = new Dictionary<PlayerAction, Button>();
        private readonly VisualElement _overlay;
        private readonly Label _overlayTitle, _overlayText;

        public event Action<PlayerAction> ActionChosen;
        public event Action NewTableClicked;

        public TableScreen(Transform parent)
        {
            var root = Ui.CreateDocument(parent, "Table UI", 0).rootVisualElement;

            var layout = Ui.Column(root);
            Ui.Fill(layout);
            Ui.Padding(layout, 18, 28);
            layout.style.alignItems = Align.Center;
            layout.pickingMode = PickingMode.Ignore;

            // top bar
            var top = Ui.Row(layout, 28);
            top.style.alignSelf = Align.Stretch;
            top.style.justifyContent = Justify.SpaceBetween;
            var title = Ui.Text(top, "HOUSE EDGE", 26, Ui.Gold, bold: true);
            title.style.letterSpacing = 4;

            var stats = Ui.Row(top, 26);
            var scoreBox = Ui.Column(stats, 4);
            var scoreRow = Ui.Row(scoreBox, 6);
            _score = Ui.Text(scoreRow, "0", 26, Ui.Cream, bold: true);
            _target = Ui.Text(scoreRow, "/ 150", 18, Ui.Dim);
            var bar = new VisualElement();
            bar.style.width = 200;
            bar.style.height = 8;
            bar.style.backgroundColor = Ui.Panel;
            Ui.Rounded(bar, 4);
            scoreBox.Add(bar);
            _progressFill = new VisualElement();
            _progressFill.style.height = 8;
            _progressFill.style.backgroundColor = Ui.Gold;
            Ui.Rounded(_progressFill, 4);
            bar.Add(_progressFill);
            _hands = Stat(stats, "Hands");
            _burns = Stat(stats, "Burns");
            _gold = Stat(stats, "Gold");

            // dealer
            var dealer = Ui.Column(layout, 8);
            dealer.style.alignItems = Align.Center;
            dealer.style.marginTop = 18;
            _dealerLabel = Ui.Text(dealer, "Dealer", 18, Ui.Dim);
            _dealerCards = Ui.Row(dealer, 10);
            _dealerCards.style.height = CardView.Height;

            // result
            var middle = Ui.Column(layout, 4);
            middle.style.alignItems = Align.Center;
            middle.style.flexGrow = 1;
            middle.style.justifyContent = Justify.Center;
            _message = Ui.Text(middle, "", 30, Ui.Gold, bold: true);
            _breakdown = Ui.Text(middle, "", 20, Ui.Cream);
            _breakdown.enableRichText = true;
            _breakdown.style.unityTextAlign = TextAnchor.MiddleCenter;

            // player
            var player = Ui.Column(layout, 8);
            player.style.alignItems = Align.Center;
            _playerHands = Ui.Row(player, 36);
            _playerHands.style.minHeight = CardView.Height + 12;
            _playerLabel = Ui.Text(player, "", 18, Ui.Dim);

            // actions
            var actions = Ui.Row(layout, 8);
            actions.style.marginTop = 16;
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

            // table over
            _overlay = new VisualElement();
            Ui.Fill(_overlay);
            _overlay.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            root.Add(_overlay);
            var card = Ui.Column(_overlay, 12);
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = Ui.FeltDark;
            Ui.Padding(card, 28, 48);
            Ui.Rounded(card, 14);
            Ui.Border(card, 2, Ui.Gold);
            _overlayTitle = Ui.Text(card, "", 34, Ui.Gold, bold: true);
            _overlayText = Ui.Text(card, "", 20, Ui.Cream);
            Ui.Button(card, "New table  <size=12><color=#FFFFFF88>Space</color></size>", () => NewTableClicked?.Invoke(), accent: true)
                .enableRichText = true;
            Ui.Show(_overlay, false);
        }

        private static Label Stat(VisualElement parent, string name)
        {
            var box = Ui.Column(parent);
            box.style.alignItems = Align.Center;
            var value = Ui.Text(box, "0", 24, Ui.Cream, bold: true);
            Ui.Text(box, name.ToUpperInvariant(), 11, Ui.Dim);
            return value;
        }

        public void Refresh(Table table)
        {
            _score.text = table.Score.ToString("N0");
            _target.text = "/ " + table.Target.ToString("N0");
            float progress = table.Target <= 0 ? 1 : Mathf.Clamp01((float)table.Score / table.Target);
            _progressFill.style.width = Length.Percent(progress * 100);
            _hands.text = table.HandsLeft.ToString();
            _burns.text = table.BurnsLeft.ToString();
            _gold.text = table.Gold + "g";

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

            Ui.Show(_overlay, table.IsOver);
            if (table.IsOver)
            {
                bool won = table.Phase == TablePhase.Won;
                _overlayTitle.text = won ? "Table cleared!" : "Out of Hands";
                _overlayTitle.style.color = won ? Ui.Gold : Ui.Bad;
                _overlayText.text = $"Scored {table.Score:N0} of {table.Target:N0}";
            }
        }

        private void RefreshDealer(Table table)
        {
            _dealerCards.Clear();
            for (int i = 0; i < table.DealerCards.Count; i++)
            {
                bool hidden = i == 1 && table.HoleCardHidden;
                _dealerCards.Add(hidden ? CardView.CreateFaceDown() : CardView.Create(table.DealerCards[i]));
            }
            if (table.DealerCards.Count == 0) _dealerLabel.text = "Dealer";
            else if (table.HoleCardHidden) _dealerLabel.text = $"Dealer shows {table.DealerVisibleValue.Total}";
            else
            {
                var value = HandValue.Of(table.DealerCards);
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
            if (table.Phase == TablePhase.InsuranceOffer)
            {
                _message.text = "Dealer shows an Ace";
                _breakdown.text = $"Insurance costs {Balance.InsuranceCost}g. If the dealer has blackjack, your hand becomes a push.";
                return;
            }
            if (table.LastScores.Count == 0)
            {
                _message.text = table.Phase == TablePhase.ReadyToDeal ? "Place your bet" : "";
                _breakdown.text = table.Phase == TablePhase.ReadyToDeal
                    ? $"Reach {table.Target:N0} points in {table.HandsLeft} Hands. Score = Chips x Mult."
                    : "";
                return;
            }

            long total = table.LastScores.Sum(s => s.Total);
            var best = table.LastScores.OrderByDescending(s => s.Total).First();
            _message.text = table.LastScores.Count > 1
                ? $"Split hands score {total:N0}"
                : Headline(best);
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
