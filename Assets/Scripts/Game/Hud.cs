using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the left sidebar: where you are in the run, the score race and your Vices
    public sealed class Hud
    {
        public const float Width = 270;

        private readonly Label _floor, _stage, _score, _target, _hands, _burns, _gold;
        private readonly VisualElement _progressFill, _vices;
        private readonly Label _vicesTitle;

        public event Action<RunInput> InputChosen;

        public Hud(VisualElement root)
        {
            var panel = Ui.Column(root, 10);
            panel.style.position = Position.Absolute;
            panel.style.left = panel.style.top = panel.style.bottom = 0;
            panel.style.width = Width;
            panel.style.backgroundColor = new Color(0, 0, 0, 0.32f);
            Ui.Padding(panel, 18, 18);

            var title = Ui.Text(panel, "HOUSE EDGE", 26, Ui.Gold, bold: true);
            title.style.letterSpacing = 3;
            _floor = Ui.Text(panel, "", 15, Ui.Dim);
            _stage = Ui.Text(panel, "", 20, Ui.Cream, bold: true);

            var scoreBox = Ui.Column(panel, 4);
            scoreBox.style.backgroundColor = Ui.Panel;
            Ui.Rounded(scoreBox, 10);
            Ui.Padding(scoreBox, 10, 12);
            Ui.Text(scoreBox, "SCORE", 11, Ui.Dim);
            var scoreRow = Ui.Row(scoreBox, 6);
            _score = Ui.Text(scoreRow, "0", 30, Ui.Cream, bold: true);
            _target = Ui.Text(scoreRow, "", 16, Ui.Dim);
            var bar = new VisualElement();
            bar.style.height = 8;
            bar.style.backgroundColor = new Color(0, 0, 0, 0.4f);
            Ui.Rounded(bar, 4);
            Ui.Add(scoreBox, bar);
            _progressFill = new VisualElement();
            _progressFill.style.height = 8;
            _progressFill.style.backgroundColor = Ui.Gold;
            Ui.Rounded(_progressFill, 4);
            bar.Add(_progressFill);

            var stats = Ui.Row(panel, 10);
            _hands = Stat(stats, "HANDS");
            _burns = Stat(stats, "BURNS");
            _gold = Stat(stats, "GOLD");

            _vicesTitle = Ui.Text(panel, "", 13, Ui.Dim, bold: true);
            _vicesTitle.style.marginTop = 6;
            _vices = Ui.Column(panel, 6);
        }

        private static Label Stat(VisualElement parent, string name)
        {
            var box = Ui.Column(parent);
            box.style.flexGrow = 1;
            box.style.alignItems = Align.Center;
            box.style.backgroundColor = Ui.Panel;
            Ui.Rounded(box, 8);
            Ui.Padding(box, 6, 4);
            var value = Ui.Text(box, "0", 22, Ui.Cream, bold: true);
            Ui.Text(box, name, 10, Ui.Dim);
            return value;
        }

        public void Refresh(Run run)
        {
            var table = run.Table;
            _floor.text = $"FLOOR {run.Floor} OF {Balance.Floors}";
            _stage.text = StageName(run.Stage, run.Floor);
            _score.text = table.Score.ToString("N0");
            _target.text = "/ " + table.Target.ToString("N0");
            float progress = Mathf.Clamp01((float)table.Score / table.Target);
            _progressFill.style.width = Length.Percent(progress * 100);
            _hands.text = table.HandsLeft.ToString();
            _burns.text = table.BurnsLeft.ToString();
            _gold.text = run.Gambler.Gold + "g";

            var gambler = run.Gambler;
            _vicesTitle.text = $"VICES  {gambler.Vices.Count}/{gambler.ViceSlots}";
            _vices.Clear();
            bool inShop = run.Phase == RunPhase.Shop;
            for (int i = 0; i < gambler.Vices.Count; i++) Ui.Add(_vices, ViceRow(run, i, inShop));
            if (gambler.Vices.Count == 0)
                Ui.Text(_vices, "None yet. Buy them in the shop after each table.", 13, Ui.Dim).style.whiteSpace = WhiteSpace.Normal;
        }

        public static string StageName(TableKind stage, int floor) => stage switch
        {
            TableKind.Low => "Low Table",
            TableKind.High => "High Table",
            _ => floor == Balance.Floors ? "The House" : "Pit Boss",
        };

        private VisualElement ViceRow(Run run, int index, bool inShop)
        {
            var vice = run.Gambler.Vices[index];
            var row = Ui.Column(null, 2);
            row.style.backgroundColor = Ui.Panel;
            Ui.Rounded(row, 8);
            Ui.Padding(row, 6, 8);
            row.style.borderLeftWidth = 4;
            row.style.borderLeftColor = ViceCardView.RarityColor(vice.Definition.Rarity);
            row.style.opacity = vice.Disabled ? 0.45f : 1f;
            row.tooltip = vice.Definition.Description;

            var top = Ui.Row(row, 6);
            var name = Ui.Text(top, $"{index + 1}. {vice.Name}", 15, Ui.Cream, bold: true);
            name.style.flexGrow = 1;
            if (vice.Status != null) Ui.Text(top, vice.Status, 12, Ui.Gold);
            var text = Ui.Text(row, vice.Definition.Description, 12, Ui.Dim);
            text.style.whiteSpace = WhiteSpace.Normal;

            if (inShop)
            {
                var buttons = Ui.Row(row, 4);
                buttons.style.marginTop = 4;
                SmallButton(buttons, "◀", RunInput.MoveVice(index, index - 1), run);
                SmallButton(buttons, "▶", RunInput.MoveVice(index, index + 1), run);
                SmallButton(buttons, $"Sell {run.SellValue(vice)}g", RunInput.SellVice(index), run);
            }
            return row;
        }

        private void SmallButton(VisualElement parent, string text, RunInput input, Run run)
        {
            var button = Ui.Button(parent, text, () => InputChosen?.Invoke(input));
            button.style.height = 26;
            button.style.minWidth = 34;
            button.style.fontSize = 12;
            Ui.Padding(button, 0, 8);
            Ui.SetEnabled(button, run.IsLegal(input));
        }
    }
}
