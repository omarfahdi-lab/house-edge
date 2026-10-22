using System;
using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the left sidebar: where you are in the run, the score race, Heat, Cocktails and your Vices
    public sealed class Hud
    {
        public const float Width = 280;

        private readonly VisualElement _pips, _stages;
        private readonly Label _floor, _special;
        private readonly Label _score, _target, _scoreState;
        private readonly Counter _scoreCounter, _goldCounter;
        private readonly VisualElement _progressFill;
        private readonly Label _hands, _burns, _gold;
        private readonly VisualElement _heatFill;
        private readonly Label _heatLevel;
        private readonly Label _debt;
        private readonly VisualElement _debtRow;
        private readonly VisualElement _cocktails, _vices, _comps, _sets;
        private readonly Label _vicesTitle;
        private readonly Button _deckButton, _repay;
        private Table _scoreTable;

        public VisualElement GoldAnchor => _gold;

        // while a hand's scoring plays back, the score shows what it was before the hand
        public long? FrozenScore;
        private readonly List<(Vice vice, VisualElement row)> _viceRows = new List<(Vice, VisualElement)>();

        private readonly ScrollView _scroll;

        // the row of a Vice that's about to trigger, scrolled into view
        public VisualElement FocusVice(object vice)
        {
            var row = _viceRows.FirstOrDefault(r => r.vice == vice).row;
            if (row != null) _scroll.ScrollTo(row);
            return row;
        }

        public event Action<RunInput> InputChosen;
        public event Action<int> CocktailClicked;
        public event Action DeckClicked, MapClicked;

        public Hud(VisualElement root)
        {
            var panel = Ui.Column(root, 9);
            panel.style.position = Position.Absolute;
            panel.style.left = panel.style.top = panel.style.bottom = 0;
            panel.style.width = Width;
            panel.style.backgroundColor = new Color(0.01f, 0.03f, 0.025f, 0.72f);
            panel.style.borderRightWidth = 1;
            panel.style.borderRightColor = Ui.Hairline;
            Ui.Padding(panel, 12, 14);

            // where you are: floor pips and the three tables of this floor
            var where = Ui.Glass(panel, 7);
            Ui.Padding(where, 9, 12);
            var top = Ui.Row(where, 6);
            Ui.Title(top, "HOUSE EDGE", 17, Ui.Gold, glow: false).style.flexGrow = 1;
            _floor = Ui.Text(top, "", 12, Ui.Cream, Weight.Bold);
            _pips = Ui.Row(where, 5);
            _stages = Ui.Row(where, 4);
            _special = Ui.Text(where, "", 12, Ui.Bad, Weight.Bold);
            Ui.Tip(where, "The climb", "8 floors. Each one is a Low Table, a room, a High Table, a room, then a Pit Boss with a twist. Beat The House on floor 8 to win.\nClick (or press Tab) for the map.");
            where.RegisterCallback<ClickEvent>(_ => MapClicked?.Invoke());
            Ui.Lift(where, 1.02f);

            // the score race
            var scoreBox = Ui.Glass(panel, 5);
            Ui.Padding(scoreBox, 9, 12);
            var scoreHead = Ui.Row(scoreBox);
            Ui.Caption(scoreHead, "SCORE").style.flexGrow = 1;
            _target = Ui.Text(scoreHead, "", 12, Ui.Dim, Weight.Bold);
            _score = Ui.Text(scoreBox, "0", 34, Ui.Cream, Weight.Heavy);
            _score.style.marginTop = -2;
            _scoreCounter = new Counter(_score, v => v.ToString("N0"));
            _progressFill = Bar(scoreBox, 10, Ui.Gold, out _);
            Ui.Transition(_progressFill, 0.6f, "width", "background-color");
            _scoreState = Ui.Text(scoreBox, "", 11, Ui.Dim, Weight.Medium);
            Ui.Tip(scoreBox, "Score", "Every hand you play adds Chips × Mult to your score. Reach the target before your Hands run out to clear the table.");

            var stats = Ui.Row(panel, 7);
            _hands = Stat(stats, SuitIcon.Shape.Cards, Ui.Cream, "HANDS", "Hands left",
                "Each deal uses one Hand. Run out before reaching the target and the run is over. Unused Hands pay 1 gold each.");
            _burns = Stat(stats, SuitIcon.Shape.Flame, Ui.HeatColor, "BURNS", "Burns",
                "Right after you Hit, Burn throws that card away and draws a new one. Works even if the card busted you. Refills every table.");
            _gold = Stat(stats, SuitIcon.Shape.Coin, Ui.Gold, "GOLD", "Gold",
                $"Spend it in the shop and rooms. Interest: when a table ends you get +1 gold for every {Balance.InterestStep} you hold (up to +{Balance.InterestCap}).");
            _goldCounter = new Counter(_gold, v => v.ToString());

            // Heat
            var heat = Ui.Column(panel, 4);
            var heatHead = Ui.Row(heat, 6);
            Ui.Add(heatHead, new SuitIcon(SuitIcon.Shape.Eye, Ui.Dim, 14));
            Ui.Caption(heatHead, "HEAT").style.flexGrow = 1;
            _heatLevel = Ui.Text(heatHead, "", 11, Ui.Dim, Weight.Bold);
            _heatFill = Bar(heat, 7, Ui.HeatColor, out var heatBar);
            Ui.Transition(_heatFill, 0.5f, "width", "background-color");
            // marks at 25, 50 and 75 where security gets stricter
            foreach (int mark in new[] { 25, 50, 75 })
            {
                var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                tick.style.position = Position.Absolute;
                tick.style.left = Length.Percent(mark);
                tick.style.top = tick.style.bottom = 0;
                tick.style.width = 1;
                tick.style.backgroundColor = new Color(1, 1, 1, 0.3f);
                heatBar.Add(tick);
            }
            Ui.Tip(heat, "Heat: how closely security is watching",
                "Cheater Vices and huge hands add Heat. It cools by 10 after each table.\n25 Watched: reshuffles more often.\n50 Suspected: prices +25%.\n75 Flagged: a Cheater Vice is switched off each table.\n100: security drags you to The Eye in the Sky.");

            _debtRow = Ui.Row(panel, 6);
            _debt = Ui.Text(_debtRow, "", 12, Ui.Bad, Weight.Bold);
            _debt.style.flexGrow = 1;
            _debt.style.flexShrink = 1;
            _debt.style.whiteSpace = WhiteSpace.Normal;
            _repay = Ui.Button(_debtRow, "Repay", () => InputChosen?.Invoke(RunInput.RepayDebt()));
            Compact(_repay);

            // Cocktails and the deck
            var drinksHead = Ui.Row(panel, 6);
            Ui.Caption(drinksHead, "COCKTAILS").style.flexGrow = 1;
            _deckButton = Ui.Button(drinksHead, "Deck", () => DeckClicked?.Invoke(), ButtonKind.Ghost);
            Compact(_deckButton);
            Ui.Tip(_deckButton, "Your deck", "See every card you own. You and the dealer both draw from it.");
            _cocktails = Ui.Row(panel, 6);
            _comps = Ui.Row(panel, 4);
            _comps.style.flexWrap = Wrap.Wrap;

            var vicesHead = Ui.Row(panel, 6);
            _vicesTitle = Ui.Caption(vicesHead, "");
            _vicesTitle.style.flexGrow = 1;
            _sets = Ui.Row(vicesHead, 4);
            var scroll = _scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            Ui.Add(panel, scroll);
            _vices = Ui.Column(null, 5);
            scroll.Add(_vices);
            // there's no theme stylesheet, so stop the column squashing its children when the Vice list is long
            foreach (var child in panel.Children()) if (child != scroll) child.style.flexShrink = 0;
            scroll.contentContainer.style.flexShrink = 0;
            scroll.style.overflow = Overflow.Hidden;
        }

        private static VisualElement Bar(VisualElement parent, float height, Color color, out VisualElement bar)
        {
            bar = new VisualElement();
            bar.style.height = height;
            bar.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            Ui.Rounded(bar, height / 2);
            Ui.Border(bar, 1, new Color(1, 1, 1, 0.06f));
            Ui.Add(parent, bar);
            var fill = new VisualElement();
            fill.style.height = Length.Percent(100);
            fill.style.width = 0;
            fill.style.backgroundColor = color;
            Ui.Rounded(fill, height / 2);
            bar.Add(fill);
            return fill;
        }

        private static Label Stat(VisualElement parent, SuitIcon.Shape icon, Color color, string name, string title, string tip)
        {
            var box = Ui.Glass(parent, 2, radius: 10);
            box.style.flexGrow = 1;
            box.style.flexBasis = 0;
            box.style.alignItems = Align.Center;
            Ui.Padding(box, 6, 4);
            var row = Ui.Row(box, 5);
            Ui.Add(row, new SuitIcon(icon, color, 14));
            var value = Ui.Text(row, "0", 21, Ui.Cream, Weight.Heavy);
            Ui.Caption(box, name);
            Ui.Tip(box, title, tip);
            return value;
        }

        private static void Compact(Button button)
        {
            button.style.height = 24;
            button.style.minWidth = 28;
            Ui.Padding(button, 0, 8);
            button.style.borderBottomWidth = 2;
            var text = button.Q<Label>();
            if (text != null) text.style.fontSize = 12;
        }

        public void Refresh(Run run)
        {
            var table = run.Table;
            var gambler = run.Gambler;
            RefreshWhere(run);

            bool newTable = table != _scoreTable;
            _scoreTable = table;
            long score = FrozenScore ?? table.Score;
            _scoreCounter.Set(score, !newTable);
            _target.text = "TARGET  " + table.Target.ToString("N0");
            float progress = Mathf.Clamp01((float)score / table.Target);
            _progressFill.style.width = Length.Percent(progress * 100);
            bool reached = score >= table.Target;
            _progressFill.style.backgroundColor = reached ? Ui.Good : Ui.Gold;
            long left = Math.Max(0, table.Target - score);
            _scoreState.text = reached ? "Target reached!" : $"{left:N0} to go  ·  {table.HandsLeft} Hand{(table.HandsLeft == 1 ? "" : "s")} left";
            _scoreState.style.color = reached ? Ui.Good : Ui.Dim;

            _hands.text = table.HandsLeft.ToString();
            _hands.style.color = table.HandsLeft <= 1 ? Ui.Bad : Ui.Cream;
            _burns.text = table.Rules.BurnsDisabled ? "-" : table.BurnsLeft.ToString();
            _goldCounter.Set(gambler.Gold);

            var level = gambler.HeatLevel;
            _heatLevel.text = $"{gambler.Heat}  ·  {level.ToString().ToUpperInvariant()}";
            var heatColor = level switch
            {
                HeatLevel.Clean => new Color(0.95f, 0.75f, 0.3f),
                HeatLevel.Watched => Ui.HeatColor,
                _ => Ui.Bad,
            };
            _heatLevel.style.color = level == HeatLevel.Clean ? Ui.Dim : heatColor;
            _heatFill.style.width = Length.Percent(Mathf.Clamp(gambler.Heat, 0, 100));
            _heatFill.style.backgroundColor = heatColor;

            Ui.SetText(_deckButton, $"View deck  {gambler.Deck.Count}");
            Ui.Show(_debtRow, gambler.Debt > 0);
            _debt.text = $"DEBT {gambler.Debt}g · due end of floor {gambler.DebtDueFloor}";
            Ui.SetEnabled(_repay, run.IsLegal(RunInput.RepayDebt()));
            Ui.Tip(_debtRow, "Loan Shark debt", "Debt grows 50% after every table. If it isn't paid by the end of its floor, The Enforcer comes to collect.");

            _cocktails.Clear();
            for (int i = 0; i < gambler.CocktailSlots; i++)
            {
                if (i < gambler.Cocktails.Count) Ui.Add(_cocktails, CocktailChip(run, i));
                else
                {
                    var empty = Ui.Text(null, "empty slot", 11, new Color(1, 1, 1, 0.3f));
                    empty.style.flexGrow = 1;
                    empty.style.flexBasis = 0;
                    empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                    empty.style.height = 34;
                    Ui.Border(empty, 1, new Color(1, 1, 1, 0.12f));
                    Ui.Rounded(empty, 8);
                    Ui.Add(_cocktails, empty);
                    Ui.Tip(empty, "Cocktail slot", "Cocktails are one-use drinks. Buy them in the shop or at The Bar.");
                }
            }

            _comps.Clear();
            foreach (var comp in Comps.All.Where(c => gambler.CompLevel(c) > 0))
            {
                var pill = Ui.Pill(_comps, $"{Comps.Name(comp).ToUpperInvariant()} LV{gambler.CompLevel(comp)}", Ui.Chips, size: 10);
                pill.style.marginBottom = 3;
                pill.style.marginRight = 1;
                var (chips, mult) = Comps.PerLevel(comp);
                Ui.Tip(pill, $"{Comps.Name(comp)} Comp, level {gambler.CompLevel(comp)}",
                    $"{Comps.Describe(comp)} per level. Right now: +{chips * gambler.CompLevel(comp)} Chips, +{mult * gambler.CompLevel(comp)} Mult.");
            }
            Ui.Show(_comps, _comps.childCount > 0);

            _vicesTitle.text = $"VICES  {gambler.Vices.Count}/{gambler.ViceSlots}";
            _sets.Clear();
            foreach (Archetype archetype in Enum.GetValues(typeof(Archetype)))
            {
                if (!ArchetypeBonuses.Has(gambler, archetype)) continue;
                var pill = Ui.Pill(_sets, archetype.ToString().ToUpperInvariant() + " SET", ViceCardView.ArchetypeColor(archetype), filled: true, size: 9);
                Ui.Tip(pill, $"{archetype} set bonus", ArchetypeBonuses.Describe(archetype));
            }

            _vices.Clear();
            _viceRows.Clear();
            bool between = run.Phase == RunPhase.Shop || run.Phase == RunPhase.BossReward || run.Phase == RunPhase.InRoom;
            for (int i = 0; i < gambler.Vices.Count; i++)
            {
                var row = Ui.Add(_vices, ViceRow(run, i, between));
                _viceRows.Add((gambler.Vices[i], row));
            }
            if (gambler.Vices.Count == 0)
            {
                var hint = Ui.Body(_vices, "No Vices yet. They're passive rule-breakers you buy in the shop after each table. Hold 3 of one type for a set bonus.", 12, Ui.Dim);
                Ui.Padding(hint, 8, 10);
                Ui.Border(hint, 1, new Color(1, 1, 1, 0.1f));
                Ui.Rounded(hint, 8);
            }
        }

        private void RefreshWhere(Run run)
        {
            _floor.text = $"FLOOR {run.Floor}" + (run.Floor > Balance.Floors ? "" : $" / {Balance.Floors}");
            _pips.Clear();
            int floors = Math.Max(Balance.Floors, run.Floor);
            for (int f = 1; f <= floors; f++)
            {
                var pip = new VisualElement();
                bool current = f == run.Floor;
                float size = current ? 12 : 9;
                pip.style.width = pip.style.height = size;
                Ui.Rounded(pip, size / 2);
                pip.style.backgroundColor = f < run.Floor ? Ui.Gold : current ? new Color(0.98f, 0.79f, 0.32f, 0.3f) : new Color(1, 1, 1, 0.1f);
                if (current) Ui.Border(pip, 2, Ui.Gold);
                if (f == Balance.Floors) Ui.BorderColor(pip, current ? Ui.Gold : Ui.Bad);
                if (f == Balance.Floors && !current) Ui.Border(pip, 1, Ui.Bad);
                Ui.Add(_pips, pip);
            }

            _stages.Clear();
            foreach (TableKind stage in new[] { TableKind.Low, TableKind.High, TableKind.Boss })
            {
                bool now = stage == run.Stage;
                bool done = stage < run.Stage;
                var color = stage == TableKind.Boss ? Ui.Bad : Ui.Gold;
                string name = stage switch { TableKind.Low => "LOW", TableKind.High => "HIGH", _ => run.Floor == Balance.Floors ? "HOUSE" : "BOSS" };
                var pill = Ui.Pill(_stages, name, now ? color : done ? new Color(1, 1, 1, 0.45f) : new Color(1, 1, 1, 0.3f), filled: now, size: 10);
                pill.style.flexGrow = 1;
                pill.style.unityTextAlign = TextAnchor.MiddleCenter;
            }

            string special = run.AtEyeInTheSky ? "SECURITY: The Eye in the Sky"
                : run.ActiveTable != run.Table ? "IN THE BACK ROOM"
                : run.Stage == TableKind.Boss && run.Boss != null && run.Phase == RunPhase.AtTable && run.Boss.Name != null ? $"vs {run.Boss.Name}"
                : null;
            _special.text = special ?? "";
            Ui.Show(_special, special != null);
        }

        private VisualElement CocktailChip(Run run, int index)
        {
            var cocktail = run.Gambler.Cocktails[index];
            int slot = index;
            var button = Ui.Button(null, cocktail.Name, () => CocktailClicked?.Invoke(slot));
            button.style.height = 34;
            button.style.minWidth = 0;
            Ui.Padding(button, 0, 6);
            button.style.backgroundImage = Ui.Gradient(new Color(0.42f, 0.2f, 0.52f), new Color(0.22f, 0.08f, 0.3f));
            Ui.BorderColor(button, new Color(0.78f, 0.5f, 1f, 0.6f));
            button.style.borderBottomColor = new Color(0, 0, 0, 0.45f);
            var glass = new SuitIcon(SuitIcon.Shape.Glass, Ui.Cocktail, 13) { pickingMode = PickingMode.Ignore };
            button.Insert(1, glass);
            glass.style.marginRight = 5;
            var label = button.Q<Label>();
            label.style.fontSize = 12;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 1;

            // usable now? with targets it opens the deck picker, so check the no-target case loosely
            bool betweenTables = run.Phase == RunPhase.Shop || run.Phase == RunPhase.BossReward
                                 || run.Phase == RunPhase.RoomChoice || run.Phase == RunPhase.InRoom && run.ActiveTable == run.Table;
            bool usable = cocktail.Targets.max > 0 ? betweenTables : run.CanUseCocktail(RunInput.UseCocktail(index));
            string when = cocktail.Use switch
            {
                CocktailUse.OnDeck => "Changes cards in your deck, so it's drunk between tables (in the shop or a room). Click to pick the cards.",
                CocktailUse.AtTable => "Only works while you're at a table.",
                _ => "Click to drink it now.",
            };
            Ui.SetEnabled(button, usable);
            // a disabled button gets no hover, so the tip goes on a wrapper
            var wrap = new VisualElement();
            wrap.style.flexGrow = 1;
            wrap.style.flexBasis = 0;
            wrap.Add(button);
            button.style.flexGrow = 0;
            button.style.width = Length.Percent(100);
            Ui.Tip(wrap, cocktail.Name, $"{cocktail.Description}\n{(usable ? "" : "Not now. ")}{when}");
            return wrap;
        }

        public static string StageName(TableKind stage, int floor) => stage switch
        {
            TableKind.Low => "Low Table",
            TableKind.High => "High Table",
            _ => floor == Balance.Floors ? "The House" : "Pit Boss",
        };

        private VisualElement ViceRow(Run run, int index, bool between)
        {
            var vice = run.Gambler.Vices[index];
            var definition = vice.Definition;
            var row = Ui.Column(null, 2);
            row.style.flexShrink = 0;
            row.style.backgroundColor = new Color(0.02f, 0.05f, 0.04f, 0.75f);
            Ui.Rounded(row, 8);
            Ui.Padding(row, 6, 9);
            Ui.Border(row, 1, new Color(1, 1, 1, 0.07f));
            row.style.borderLeftWidth = 4;
            row.style.borderLeftColor = ViceCardView.RarityColor(definition.Rarity);
            row.style.opacity = vice.Disabled ? 0.45f : 1f;

            var top = Ui.Row(row, 6);
            var dot = new VisualElement();
            dot.style.width = dot.style.height = 8;
            Ui.Rounded(dot, 4);
            dot.style.backgroundColor = ViceCardView.ArchetypeColor(definition.Archetype);
            Ui.Add(top, dot);
            var name = Ui.Text(top, vice.Name, 14, Ui.Cream, Weight.Bold);
            name.style.flexGrow = 1;
            name.style.flexShrink = 1;
            if (vice.Disabled) Ui.Pill(top, "OFF", Ui.Bad, size: 9);
            else if (vice.Status != null) Ui.Pill(top, vice.Status, Ui.Gold, size: 9);
            Ui.Body(row, definition.Description, 11, Ui.Dim);

            if (between)
            {
                var buttons = Ui.Row(row, 4);
                buttons.style.marginTop = 4;
                SmallButton(buttons, "←", RunInput.MoveVice(index, index - 1), run, "Move left", "Vices trigger left to right (top to bottom here). Order matters for x Mult.");
                SmallButton(buttons, "→", RunInput.MoveVice(index, index + 1), run, "Move right", "Vices trigger left to right (top to bottom here). Order matters for x Mult.");
                SmallButton(buttons, $"Sell  +{run.SellValue(vice)}g", RunInput.SellVice(index), run, "Sell", "Sell this Vice to free its slot.");
            }

            Ui.Tip(row, vice.Name,
                $"{definition.Rarity} · {definition.Archetype}\n{definition.Description}" +
                (vice.Disabled ? "\nSwitched off right now." : "") +
                $"\nTriggers {Ordinal(index + 1)}. Hold 3 {definition.Archetype} Vices for: {ArchetypeBonuses.Describe(definition.Archetype)}");
            return row;
        }

        private static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => n + "th" };

        private void SmallButton(VisualElement parent, string text, RunInput input, Run run, string title, string tip)
        {
            var button = Ui.Button(parent, text, () => InputChosen?.Invoke(input), ButtonKind.Ghost);
            Compact(button);
            Ui.SetEnabled(button, run.IsLegal(input));
            Ui.Tip(button, title, tip);
        }
    }
}
