using System;
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

        private readonly Label _floor, _stage, _score, _target, _hands, _burns, _gold;
        private readonly VisualElement _progressFill, _heatFill;
        private readonly Label _heatLabel;
        private readonly VisualElement _cocktails, _vices;
        private readonly Label _vicesTitle, _comps;
        private readonly Button _deckButton;

        public event Action<RunInput> InputChosen;
        public event Action<int> CocktailClicked;
        public event Action DeckClicked;

        public Hud(VisualElement root)
        {
            var panel = Ui.Column(root, 8);
            panel.style.position = Position.Absolute;
            panel.style.left = panel.style.top = panel.style.bottom = 0;
            panel.style.width = Width;
            panel.style.backgroundColor = new Color(0, 0, 0, 0.32f);
            Ui.Padding(panel, 14, 16);

            var title = Ui.Text(panel, "HOUSE EDGE", 24, Ui.Gold, bold: true);
            title.style.letterSpacing = 3;
            var where = Ui.Row(panel, 8);
            _stage = Ui.Text(where, "", 18, Ui.Cream, bold: true);
            _floor = Ui.Text(where, "", 13, Ui.Dim);

            var scoreBox = Ui.Column(panel, 4);
            scoreBox.style.backgroundColor = Ui.Panel;
            Ui.Rounded(scoreBox, 10);
            Ui.Padding(scoreBox, 8, 12);
            var scoreRow = Ui.Row(scoreBox, 6);
            _score = Ui.Text(scoreRow, "0", 28, Ui.Cream, bold: true);
            _target = Ui.Text(scoreRow, "", 15, Ui.Dim);
            _progressFill = Bar(scoreBox, Ui.Gold);

            var stats = Ui.Row(panel, 8);
            _hands = Stat(stats, "HANDS");
            _burns = Stat(stats, "BURNS");
            _gold = Stat(stats, "GOLD");

            var heat = Ui.Column(panel, 3);
            _heatLabel = Ui.Text(heat, "", 12, Ui.Dim, bold: true);
            _heatFill = Bar(heat, new Color(1f, 0.4f, 0.2f));

            var row = Ui.Row(panel, 8);
            Ui.Text(row, "COCKTAILS", 12, Ui.Dim, bold: true).style.flexGrow = 1;
            _deckButton = Ui.Button(row, "Deck", () => DeckClicked?.Invoke());
            Compact(_deckButton);
            _cocktails = Ui.Row(panel, 6);
            _comps = Ui.Text(panel, "", 12, Ui.Chips);
            _comps.style.whiteSpace = WhiteSpace.Normal;

            _vicesTitle = Ui.Text(panel, "", 12, Ui.Dim, bold: true);
            _vices = Ui.Column(panel, 5);
        }

        private static VisualElement Bar(VisualElement parent, Color color)
        {
            var bar = new VisualElement();
            bar.style.height = 8;
            bar.style.backgroundColor = new Color(0, 0, 0, 0.4f);
            Ui.Rounded(bar, 4);
            Ui.Add(parent, bar);
            var fill = new VisualElement();
            fill.style.height = 8;
            fill.style.backgroundColor = color;
            Ui.Rounded(fill, 4);
            bar.Add(fill);
            return fill;
        }

        private static Label Stat(VisualElement parent, string name)
        {
            var box = Ui.Column(parent);
            box.style.flexGrow = 1;
            box.style.alignItems = Align.Center;
            box.style.backgroundColor = Ui.Panel;
            Ui.Rounded(box, 8);
            Ui.Padding(box, 5, 4);
            var value = Ui.Text(box, "0", 20, Ui.Cream, bold: true);
            Ui.Text(box, name, 10, Ui.Dim);
            return value;
        }

        private static void Compact(Button button)
        {
            button.style.height = 24;
            button.style.minWidth = 30;
            button.style.fontSize = 12;
            Ui.Padding(button, 0, 8);
        }

        public void Refresh(Run run)
        {
            var table = run.Table;
            var gambler = run.Gambler;
            _floor.text = $"FLOOR {run.Floor}/{Balance.Floors}";
            _stage.text = run.AtEyeInTheSky ? "Security!" : StageName(run.Stage, run.Floor);
            _score.text = table.Score.ToString("N0");
            _target.text = "/ " + table.Target.ToString("N0");
            _progressFill.style.width = Length.Percent(Mathf.Clamp01((float)table.Score / table.Target) * 100);
            _hands.text = table.HandsLeft.ToString();
            _burns.text = table.Rules.BurnsDisabled ? "-" : table.BurnsLeft.ToString();
            _gold.text = gambler.Gold + "g";

            _heatLabel.text = $"HEAT {gambler.Heat}  ·  {gambler.HeatLevel.ToString().ToUpperInvariant()}";
            _heatLabel.tooltip = HeatLevels.Describe(gambler.HeatLevel);
            _heatLabel.style.color = gambler.HeatLevel >= HeatLevel.Suspected ? Ui.Bad : Ui.Dim;
            _heatFill.style.width = Length.Percent(gambler.Heat);
            _deckButton.text = $"Deck {gambler.Deck.Count}";

            _cocktails.Clear();
            for (int i = 0; i < gambler.CocktailSlots; i++)
            {
                if (i < gambler.Cocktails.Count) Ui.Add(_cocktails, CocktailChip(run, i));
                else
                {
                    var empty = Ui.Text(null, "empty", 12, Ui.Dim);
                    empty.style.flexGrow = 1;
                    empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                    empty.style.height = 30;
                    Ui.Border(empty, 1, new Color(1, 1, 1, 0.15f));
                    Ui.Rounded(empty, 6);
                    Ui.Add(_cocktails, empty);
                }
            }

            var comps = Comps.All.Where(c => gambler.CompLevel(c) > 0).Select(c => $"{Comps.Name(c)} Lv{gambler.CompLevel(c)}").ToList();
            _comps.text = comps.Count > 0 ? "COMPS  " + string.Join(" · ", comps) : "";

            _vicesTitle.text = $"VICES  {gambler.Vices.Count}/{gambler.ViceSlots}" + SetBonuses(gambler);
            _vices.Clear();
            bool between = run.Phase == RunPhase.Shop || run.Phase == RunPhase.BossReward;
            for (int i = 0; i < gambler.Vices.Count; i++) Ui.Add(_vices, ViceRow(run, i, between));
            if (gambler.Vices.Count == 0)
                Ui.Text(_vices, "None yet. Buy them in the shop after each table.", 12, Ui.Dim).style.whiteSpace = WhiteSpace.Normal;
        }

        private static string SetBonuses(Gambler gambler)
        {
            var sets = Enum.GetValues(typeof(Archetype)).Cast<Archetype>().Where(a => ArchetypeBonuses.Has(gambler, a)).ToList();
            return sets.Count > 0 ? "  ·  SET: " + string.Join(", ", sets).ToUpperInvariant() : "";
        }

        private VisualElement CocktailChip(Run run, int index)
        {
            var cocktail = run.Gambler.Cocktails[index];
            int slot = index;
            var button = Ui.Button(null, cocktail.Name, () => CocktailClicked?.Invoke(slot));
            button.style.flexGrow = 1;
            button.style.height = 30;
            button.style.minWidth = 0;
            button.style.fontSize = 12;
            button.style.whiteSpace = WhiteSpace.Normal;
            Ui.Padding(button, 0, 4);
            button.style.backgroundColor = new Color(0.25f, 0.12f, 0.3f, 0.95f);
            button.tooltip = cocktail.Description;
            // usable now? with targets it opens the deck picker, so check the no-target case loosely
            bool usable = cocktail.Targets.max > 0 ? run.Phase == RunPhase.Shop || run.Phase == RunPhase.BossReward
                : run.CanUseCocktail(RunInput.UseCocktail(index));
            Ui.SetEnabled(button, usable);
            return button;
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
            var row = Ui.Column(null, 1);
            row.style.backgroundColor = Ui.Panel;
            Ui.Rounded(row, 8);
            Ui.Padding(row, 5, 8);
            row.style.borderLeftWidth = 4;
            row.style.borderLeftColor = ViceCardView.RarityColor(vice.Definition.Rarity);
            row.style.opacity = vice.Disabled ? 0.4f : 1f;

            var top = Ui.Row(row, 6);
            var name = Ui.Text(top, $"{index + 1}. {vice.Name}", 14, Ui.Cream, bold: true);
            name.style.flexGrow = 1;
            if (vice.Disabled) Ui.Text(top, "OFF", 11, Ui.Bad, bold: true);
            else if (vice.Status != null) Ui.Text(top, vice.Status, 11, Ui.Gold);
            var text = Ui.Text(row, vice.Definition.Description, 11, Ui.Dim);
            text.style.whiteSpace = WhiteSpace.Normal;

            if (between)
            {
                var buttons = Ui.Row(row, 4);
                buttons.style.marginTop = 3;
                SmallButton(buttons, "◀", RunInput.MoveVice(index, index - 1), run);
                SmallButton(buttons, "▶", RunInput.MoveVice(index, index + 1), run);
                SmallButton(buttons, $"Sell {run.SellValue(vice)}g", RunInput.SellVice(index), run);
            }
            return row;
        }

        private void SmallButton(VisualElement parent, string text, RunInput input, Run run)
        {
            var button = Ui.Button(parent, text, () => InputChosen?.Invoke(input));
            Compact(button);
            Ui.SetEnabled(button, run.IsLegal(input));
        }
    }
}
