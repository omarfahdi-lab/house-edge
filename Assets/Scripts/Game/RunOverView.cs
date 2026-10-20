using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the end of a run, won or lost
    public sealed class RunOverView
    {
        private readonly VisualElement _root;
        private readonly Label _title, _text;

        public event Action NewRunClicked;

        public VisualElement Root => _root;

        public RunOverView(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            parent.Add(_root);

            var card = Ui.Column(_root, 12);
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = Ui.FeltDark;
            card.style.maxWidth = 620;
            Ui.Padding(card, 30, 48);
            Ui.Rounded(card, 14);
            Ui.Border(card, 2, Ui.Gold);
            _title = Ui.Text(card, "", 38, Ui.Gold, bold: true);
            _text = Ui.Text(card, "", 18, Ui.Cream);
            _text.style.whiteSpace = WhiteSpace.Normal;
            _text.style.unityTextAlign = TextAnchor.MiddleCenter;
            Ui.Button(card, "New run  <size=12><color=#FFFFFF88>Space</color></size>", () => NewRunClicked?.Invoke(), accent: true)
                .enableRichText = true;
        }

        public void Refresh(Run run)
        {
            bool won = run.Phase == RunPhase.Won;
            _title.text = won ? "YOU BEAT THE HOUSE" : "THE HOUSE WINS";
            _title.style.color = won ? Ui.Gold : Ui.Bad;
            var table = run.Table;
            _text.text = won
                ? $"All {Balance.Floors} floors cleared with {run.Gambler.Gold}g in your pocket."
                : $"Out of Hands on floor {run.Floor} at the {Hud.StageName(run.Stage, run.Floor)}: " +
                  $"{table.Score:N0} of {table.Target:N0}.\nTables beaten: {run.TablesBeaten}. Seed {run.Seed}.";
        }
    }
}
