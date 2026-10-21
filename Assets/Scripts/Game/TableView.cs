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
            (PlayerAction.SwapWithDealer, "Swap", "W"),
            (PlayerAction.TakeInsurance, "Insure (2g)", "I"),
            (PlayerAction.DeclineInsurance, "No insurance", "N"),
            (PlayerAction.LetDealerDraw, "Let him draw", "H"),
            (PlayerAction.StopDealer, "Make him stand", "S"),
        };

        private readonly VisualElement _root;
        private readonly VisualElement _bossBanner;
        private readonly Label _bossName, _bossTwist;
        private readonly Label _dealerLabel, _tell, _secondDealerLabel, _playerLabel, _message, _breakdown;
        private readonly VisualElement _dealerCards, _secondDealer, _secondDealerCards, _playerHands;
        private readonly VisualElement _nextCard, _nextCardSlot;
        private readonly VisualElement _bets;
        private readonly Label _betStatus;
        private readonly Dictionary<PlayerAction, Button> _buttons = new Dictionary<PlayerAction, Button>();
        private readonly Dictionary<PlayerAction, Button> _betButtons = new Dictionary<PlayerAction, Button>();

        public event Action<PlayerAction> ActionChosen;

        public VisualElement Root => _root;

        public TableView(VisualElement parent)
        {
            _root = Ui.Column(parent);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            Ui.Padding(_root, 14, 24);
            _root.style.alignItems = Align.Center;
            _root.pickingMode = PickingMode.Ignore;

            // boss banner
            _bossBanner = Ui.Column(_root, 2);
            _bossBanner.style.alignItems = Align.Center;
            _bossBanner.style.backgroundColor = new Color(0.45f, 0.06f, 0.08f, 0.85f);
            Ui.Rounded(_bossBanner, 10);
            Ui.Padding(_bossBanner, 6, 18);
            _bossBanner.style.marginBottom = 8;
            _bossName = Ui.Text(_bossBanner, "", 18, Ui.Gold, bold: true);
            _bossTwist = Ui.Text(_bossBanner, "", 14, Ui.Cream);

            // dealer, the Twins' second hand and the next-card peek
            var dealerRow = Ui.Row(_root, 28);
            dealerRow.style.alignItems = Align.FlexStart;
            var dealer = Ui.Column(dealerRow, 8);
            dealer.style.alignItems = Align.Center;
            _dealerLabel = Ui.Text(dealer, "Dealer", 17, Ui.Dim);
            _dealerCards = Ui.Row(dealer, 10);
            _tell = Ui.Text(dealer, "", 14, new Color(0.85f, 0.75f, 1f));
            _tell.style.unityFontStyleAndWeight = FontStyle.Italic;
            _dealerCards.style.height = CardView.Height;

            _secondDealer = Ui.Column(dealerRow, 8);
            _secondDealer.style.alignItems = Align.Center;
            _secondDealerLabel = Ui.Text(_secondDealer, "Second hand", 17, Ui.Dim);
            _secondDealerCards = Ui.Row(_secondDealer, 10);
            _secondDealerCards.style.height = CardView.Height;

            _nextCard = Ui.Column(dealerRow, 6);
            _nextCard.style.alignItems = Align.Center;
            Ui.Text(_nextCard, "NEXT CARD", 11, Ui.Gold, bold: true);
            _nextCardSlot = Ui.Row(_nextCard);

            // result
            var middle = Ui.Column(_root, 4);
            middle.style.alignItems = Align.Center;
            middle.style.flexGrow = 1;
            middle.style.justifyContent = Justify.Center;
            _message = Ui.Text(middle, "", 28, Ui.Gold, bold: true);
            _breakdown = Ui.Text(middle, "", 18, Ui.Cream);
            _breakdown.enableRichText = true;
            _breakdown.style.unityTextAlign = TextAnchor.MiddleCenter;
            _breakdown.style.whiteSpace = WhiteSpace.Normal;

            // player
            var player = Ui.Column(_root, 6);
            player.style.alignItems = Align.Center;
            _playerHands = Ui.Row(player, 36);
            _playerHands.style.minHeight = CardView.Height + 12;
            _playerLabel = Ui.Text(player, "", 15, Ui.Dim);

            // side bets
            _bets = Ui.Row(_root, 6);
            _bets.style.marginTop = 8;
            _bets.style.flexWrap = Wrap.Wrap;
            _bets.style.justifyContent = Justify.Center;
            _betStatus = Ui.Text(_bets, "SIDE BET", 12, Ui.Dim, bold: true);
            foreach (var bet in SideBets.All)
            {
                var action = SideBets.ActionFor(bet);
                var button = SmallButton(_bets, "", () => ActionChosen?.Invoke(action));
                button.tooltip = SideBets.Description(bet);
                _betButtons[action] = button;
            }
            var ante = SmallButton(_bets, "Ante Up x2", () => ActionChosen?.Invoke(PlayerAction.AnteUp));
            ante.tooltip = "x2 Mult this hand. If you lose, you also lose 3 gold.";
            _betButtons[PlayerAction.AnteUp] = ante;

            // actions
            var actions = Ui.Row(_root, 8);
            actions.style.marginTop = 10;
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

        private static Button SmallButton(VisualElement parent, string text, Action onClick)
        {
            var button = Ui.Button(parent, text, onClick);
            button.style.height = 30;
            button.style.minWidth = 60;
            button.style.fontSize = 13;
            Ui.Padding(button, 0, 10);
            return button;
        }

        public void Refresh(Run run)
        {
            var table = run.ActiveTable;
            RefreshBoss(run);
            RefreshDealer(table);
            RefreshTell(run, table);
            RefreshPlayer(table);
            RefreshResult(table);
            RefreshBets(table);

            var legal = table.LegalActions();
            foreach (var pair in _buttons)
            {
                bool show = pair.Key switch
                {
                    PlayerAction.TakeInsurance or PlayerAction.DeclineInsurance => table.Phase == TablePhase.InsuranceOffer,
                    PlayerAction.LetDealerDraw or PlayerAction.StopDealer => table.Phase == TablePhase.DealerDecision,
                    PlayerAction.SwapWithDealer => legal.Contains(PlayerAction.SwapWithDealer),
                    _ => table.Phase != TablePhase.InsuranceOffer && table.Phase != TablePhase.DealerDecision,
                };
                Ui.Show(pair.Value, show);
                Ui.SetEnabled(pair.Value, legal.Contains(pair.Key));
            }
        }

        private void RefreshBoss(Run run)
        {
            bool backRoom = run.ActiveTable != run.Table;
            if (backRoom || run.Boss == null)
            {
                Ui.Show(_bossBanner, backRoom || run.Dealer != null);
                _bossBanner.style.backgroundColor = new Color(0, 0, 0, 0.35f);
                _bossName.text = backRoom ? "THE BACK ROOM" : run.Dealer?.Name.ToUpperInvariant();
                _bossTwist.text = backRoom ? $"One hand. Win and your {run.Gambler.Gold}g doubles. Lose and it's gone." : run.Dealer?.Style;
                return;
            }
            Ui.Show(_bossBanner, true);
            _bossBanner.style.backgroundColor = new Color(0.45f, 0.06f, 0.08f, 0.85f);
            _bossName.text = run.Boss.Name.ToUpperInvariant();
            string twist = run.Boss.Definition.Twist;
            if (run.Boss is TheHouse house && house.CurrentTwist != null)
                twist = $"Now playing as {house.CurrentTwist.Name}: {house.CurrentTwist.Definition.Twist}";
            _bossTwist.text = twist;
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
            else _dealerLabel.text = DealerText("Dealer", table.DealerValue);

            bool twins = table.SecondDealerCards.Count > 0;
            Ui.Show(_secondDealer, twins || table.Rules.DealerPlaysTwoHands);
            _secondDealerCards.Clear();
            foreach (var card in table.SecondDealerCards) Ui.Add(_secondDealerCards, CardView.Create(card));
            _secondDealerLabel.text = twins ? DealerText("Second hand", table.SecondDealerValue) : "Second hand (plays last)";

            var next = table.NextCardPreview;
            Ui.Show(_nextCard, next != null);
            _nextCardSlot.Clear();
            if (next != null) _nextCardSlot.Add(CardView.Create(next, 0.75f));
        }

        // a subtle hint about the hole card, spelled out if you hold Tell Reader
        private void RefreshTell(Run run, Table table)
        {
            var dealer = table == run.Table ? run.Dealer : null;
            string tell = dealer != null && table.HoleCardHidden && table.DealerCards.Count > 1 ? dealer.Tell(table) : null;
            if (tell != null && run.Gambler.Vices.Any(v => !v.Disabled && v.RevealsTells)) tell += "  " + dealer.TellMeaning(table);
            _tell.text = tell ?? "";
            Ui.Show(_tell, tell != null);
        }

        private static string DealerText(string who, HandValue value) =>
            value.IsBust ? $"{who} busts with {value.Total}" : $"{who} has {value.Total}";

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

                // the Mime keeps your second card face down until the hand is over
                bool mimed = table.Rules.SecondCardFaceDown && !hand.IsDone;
                var cards = Ui.Row(group);
                for (int i = 0; i < hand.Cards.Count; i++)
                {
                    var view = mimed && i == 1 ? CardView.CreateFaceDown() : CardView.Create(hand.Cards[i]);
                    // overlap a little so five-card hands fit
                    if (i > 0) view.style.marginLeft = -18;
                    cards.Add(view);
                }

                var label = Ui.Text(group, mimed ? "?" : Describe(hand, table), 16, Ui.Cream);
                if (hand.IsDoubled) label.text += "  x2";
            }

            _playerLabel.text = active == null ? "" : ActiveHint(table, active);
        }

        private static string Describe(PlayerHand hand, Table table)
        {
            var value = hand.Value;
            if (hand.IsBlackjack) return "Blackjack!";
            if (hand.RescuedAsPush) return $"Bust ({value.Total}), saved as a push";
            if (value.IsBust) return $"Bust ({value.Total})";
            if (value.IsCharlie) return "Charlie!";
            string limit = table.Rules.PlayerBustLimit != Balance.BustLimit ? $" (bust over {table.Rules.PlayerBustLimit})" : "";
            return (value.IsSoft ? $"Soft {value.Total}" : value.Total.ToString()) + limit;
        }

        private static string ActiveHint(Table table, PlayerHand hand)
        {
            if (hand.Value.IsBust) return "Bust! Burn the card to redraw it, or Stand to take the loss.";
            if (table.PlayerHands.Count > 1) return $"Playing hand {table.ActiveHandIndex + 1} of {table.PlayerHands.Count}";
            if (table.Rules.HitCost > 0) return $"Every hit costs {table.Rules.HitCost} gold.";
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
            if (table.Phase == TablePhase.DealerDecision)
            {
                var next = table.NextCardPreview;
                _message.text = "Your call";
                _breakdown.text = $"The dealer has {table.DealerValue.Total} and wants to hit. The next card is {next}. Let him take it?";
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

            var lines = table.LastScores.Select(Breakdown).Where(l => l.Length > 0).ToList();
            if (table.SideBetResult is SideBetResult bet)
                lines.Add($"{SideBets.Name(bet.Bet)}: {bet.Label}" + (bet.Won ? $" <color=#{Hex(Ui.Gold)}>+{bet.Payout}g</color>" : ""));
            if (table.AnteLost) lines.Add($"<color=#{Hex(Ui.Bad)}>Ante lost: -{Balance.AnteUpLoss}g</color>");
            _breakdown.text = string.Join("\n", lines);
        }

        private void RefreshBets(Table table)
        {
            bool open = table.Phase == TablePhase.ReadyToDeal;
            Ui.Show(_bets, open);
            if (!open) return;
            var legal = table.LegalActions();
            _betStatus.text = table.SideBet != SideBet.None
                ? $"SIDE BET: {SideBets.Name(table.SideBet).ToUpperInvariant()}" + (table.AntedUp ? " + ANTE" : "")
                : table.AntedUp ? "ANTED UP" : "SIDE BET";
            foreach (var bet in SideBets.All)
            {
                var button = _betButtons[SideBets.ActionFor(bet)];
                button.text = $"{SideBets.Name(bet)} {table.SideBetCost(bet)}g";
                Ui.Show(button, table.SideBet == SideBet.None);
                Ui.SetEnabled(button, legal.Contains(SideBets.ActionFor(bet)));
            }
            var ante = _betButtons[PlayerAction.AnteUp];
            Ui.Show(ante, !table.AntedUp);
            Ui.SetEnabled(ante, legal.Contains(PlayerAction.AnteUp));
        }

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

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
            string chips = $"<color=#{Hex(Ui.Chips)}>{score.Chips} Chips</color>";
            string mult = $"<color=#{Hex(Ui.Mult)}>{score.Mult:0.##} Mult</color>";
            return $"{score.Outcome.Label()}: {chips} x {mult} = <b>{score.Total:N0}</b>";
        }
    }
}
