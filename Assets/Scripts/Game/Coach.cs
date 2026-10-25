using System;
using System.Collections.Generic;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the guided first run. on the stacked first table it says what to do and only lets that button through;
    // after it, a few one-time tips for the map, the shop and the next table
    public sealed class Coach
    {
        private readonly struct Step
        {
            public readonly string Key, Text;
            public readonly PlayerAction? Action;

            public Step(string key, string text, PlayerAction? action = null)
            {
                Key = key;
                Text = text;
                Action = action;
            }
        }

        // the table lessons, in order, for the step counter
        private static readonly string[] Lessons = { "deal1", "stand1", "deal2", "hit2", "burn2", "stand2", "deal3", "hit3", "stand3" };

        private readonly VisualElement _root, _panel;
        private readonly Label _count, _text;
        private readonly Button _gotIt;
        private readonly TableView _table;
        private readonly HashSet<string> _dismissed = new HashSet<string>();
        private string _shown;
        private Step? _step;
        private bool _skipped;

        public event Action Finished;

        public Coach(VisualElement parent, TableView table)
        {
            _table = table;
            _root = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            parent.Add(_root);

            _panel = Ui.Column(_root, 8);
            _panel.style.backgroundColor = new Color(0.07f, 0.06f, 0.04f, 0.96f);
            Ui.Rounded(_panel, 16);
            Ui.Border(_panel, 2, Ui.Gold);
            Ui.Padding(_panel, 14, 20);
            _panel.pickingMode = PickingMode.Position;

            var head = Ui.Row(_panel, 8);
            Ui.Pill(head, "TUTORIAL", Ui.Gold, filled: true, size: 11);
            _count = Ui.Caption(head, "");
            _count.style.flexGrow = 1;
            var skip = Ui.Button(head, "Skip tutorial", Skip, ButtonKind.Ghost);
            skip.style.height = 26;
            Ui.Padding(skip, 0, 10);
            skip.Q<Label>().style.fontSize = 12;

            _text = Ui.Body(_panel, "", 16, Ui.Cream);
            // under the tip, not in the header: squeezed in beside Skip it clipped to "ot"
            _gotIt = Ui.Button(_panel, "Got it", Dismiss, ButtonKind.Gold, "Enter");
            _gotIt.style.alignSelf = Align.FlexEnd;
            _gotIt.style.flexShrink = 0;
            Ui.Show(_root, false);
        }

        public bool Active(Run run) => run.Options.Tutorial && !_skipped && !run.IsOver;

        // a new tutorial run starts from the first lesson
        public void Reset()
        {
            _dismissed.Clear();
            _shown = null;
            _skipped = false;
        }

        // the scripted table only takes the button the coach is pointing at
        public bool Allows(Run run, RunInput input)
        {
            if (!Active(run) || !run.IsTutorialTable) return true;
            if (input.Kind == RunInputKind.PlayTrick) return false;
            if (input.Kind != RunInputKind.Table) return true;
            var step = StepFor(run);
            return step?.Action == null || step.Value.Action == input.Action;
        }

        // Enter closes a tip that doesn't wait for a button
        public bool DismissKey()
        {
            if (!IsShowingTip) return false;
            Dismiss();
            return true;
        }

        private bool IsShowingTip => Ui.IsShown(_root) && _step?.Action == null;

        public void Refresh(Run run, bool busy)
        {
            _step = Active(run) && !busy ? StepFor(run) : null;
            _table.Spotlight(_step?.Action);
            if (_step == null)
            {
                _shown = null;
                Ui.Show(_root, false);
                return;
            }
            var step = _step.Value;
            if (step.Key == _shown) return;
            _shown = step.Key;
            int lesson = Array.IndexOf(Lessons, step.Key);
            _count.text = lesson >= 0 ? $"STEP {lesson + 1} OF {Lessons.Length}" : "";
            _text.text = Ui.Rich(step.Text);
            Ui.Show(_gotIt, step.Action == null);
            // at a table the coach stands on the empty right side of the felt, so your cards stay in view.
            // over the map and the shop it's a strip along the bottom
            bool side = AtTable(step);
            _root.style.alignItems = side ? Align.FlexEnd : Align.Center;
            _root.style.justifyContent = side ? Justify.Center : Justify.FlexEnd;
            _root.style.paddingRight = side ? 16 : 0;
            _root.style.paddingBottom = side ? 60 : 8;
            _panel.style.width = side ? 300 : 940;
            Ui.Show(_root, true);
            Ui.Enter(_panel, 18, 0, 0.3f);
        }

        private static bool AtTable(Step step) => step.Action != null || step.Key == "alone";

        private Step? StepFor(Run run)
        {
            if (run.IsTutorialTable && run.Phase == RunPhase.AtTable) return TableStep(run.Table);
            if (run.Phase == RunPhase.RoomChoice) return Tip("map",
                "That Flush alone cleared the table: combos are where the big points are. This is the floor map. Click a stop to walk there: " +
                "Shops sell Vices, ? is a surprise, flames are optional Elite fights, and every floor ends with a Pit Boss.");
            if (run.Phase == RunPhase.Shop) return Tip("shop",
                "Vices are permanent rule-breakers, and they're how runs get big. Look for ones that boost combos or Mult. Hover anything to see what it does.");
            if (run.Phase == RunPhase.AtTable && run.Table.DealsPlayed == 0) return Tip("alone",
                "You're on your own now. Each table has a higher target, so build your deck and Vices as you climb. Good luck.");
            return null;
        }

        private Step? Tip(string key, string text) => _dismissed.Contains(key) ? (Step?)null : new Step(key, text);

        private static Step? TableStep(Table table)
        {
            var hand = table.ActiveHand;
            switch (table.DealsPlayed)
            {
                case 0 when table.Phase == TablePhase.ReadyToDeal:
                    return new Step("deal1",
                        "Welcome to House Edge. It's blackjack: get closer to 21 than the dealer without going over. You and the dealer each get two cards. Press Deal.",
                        PlayerAction.Deal);
                case 1 when hand != null:
                    return new Step("stand1", "You have 19 and the dealer shows a 10. 19 is a strong hand, so keep it: press Stand.", PlayerAction.Stand);
                case 1 when table.Phase == TablePhase.ReadyToDeal:
                    return new Step("deal2",
                        $"You won! Every win scores Chips x Mult, and that plain win scored {table.Score}. You need {table.Target} points to clear this table, so you'll need more than plain wins. Deal again.",
                        PlayerAction.Deal);
                case 2 when hand != null && hand.Value.IsBust:
                    return new Step("burn2",
                        "Bust! Over 21 loses the hand... unless you Burn. A Burn throws away the card you just drew and gives you a new one. You get a few per table. Press Burn.",
                        PlayerAction.Burn);
                case 2 when hand != null && hand.Cards.Count >= 3:
                    return new Step("stand2", "Saved: 20. Press Stand.", PlayerAction.Stand);
                case 2 when hand != null:
                    return new Step("hit2", "16 is weak against the dealer's 9. Take another card: press Hit.", PlayerAction.Hit);
                case 2 when table.Phase == TablePhase.ReadyToDeal:
                    return new Step("deal3",
                        "Now the fun part: combos. Poker patterns in your hand, like a Pair, a Flush or a Run, add big Chips and Mult, but only when you beat the dealer. Deal.",
                        PlayerAction.Deal);
                case 3 when hand != null && hand.Cards.Count >= 3:
                    return new Step("stand3", "Three Hearts is a Flush, and 20 beats the dealer's 17. The combo shows under your hand. Press Stand and watch it score.", PlayerAction.Stand);
                case 3 when hand != null:
                    return new Step("hit3", "Two Hearts. Hit: if the next card is a Heart too, that's a Flush.", PlayerAction.Hit);
            }
            return null;
        }

        private void Dismiss()
        {
            if (_shown == null) return;
            _dismissed.Add(_shown);
            bool last = _shown == "alone";
            _shown = null;
            Ui.Show(_root, false);
            if (last)
            {
                _skipped = true;
                Finished?.Invoke();
            }
        }

        private void Skip()
        {
            _skipped = true;
            _table.Spotlight(null);
            Ui.Show(_root, false);
            Finished?.Invoke();
        }
    }
}
