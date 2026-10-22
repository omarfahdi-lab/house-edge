using System;
using System.Collections.Generic;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the end of a run, won or lost: what happened, what unlocked, the run code and a replay
    public sealed class RunOverView
    {
        private readonly VisualElement _root;
        private readonly Label _title, _text, _unlocks, _code;

        public event Action NewRunClicked, MenuClicked, ReplayClicked;

        public VisualElement Root => _root;

        public RunOverView(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.backgroundColor = new Color(0, 0, 0, 0.75f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            parent.Add(_root);

            var card = Ui.Column(_root, 12);
            card.style.alignItems = Align.Center;
            card.style.backgroundColor = Ui.FeltDark;
            card.style.width = 640;
            Ui.Padding(card, 28, 40);
            Ui.Rounded(card, 14);
            Ui.Border(card, 2, Ui.Gold);
            _title = Ui.Text(card, "", 38, Ui.Gold, bold: true);
            _text = Ui.Text(card, "", 17, Ui.Cream);
            _text.style.whiteSpace = WhiteSpace.Normal;
            _text.style.unityTextAlign = TextAnchor.MiddleCenter;
            _unlocks = Ui.Text(card, "", 16, Ui.Good, bold: true);
            _unlocks.style.whiteSpace = WhiteSpace.Normal;
            _unlocks.style.unityTextAlign = TextAnchor.MiddleCenter;
            _code = Ui.Text(card, "", 15, Ui.Dim);
            _code.selection.isSelectable = true;

            var buttons = Ui.Row(card, 10);
            Ui.Button(buttons, "New run  <size=12><color=#FFFFFF88>Space</color></size>", () => NewRunClicked?.Invoke(), accent: true)
                .enableRichText = true;
            Ui.Button(buttons, "Watch replay", () => ReplayClicked?.Invoke());
            Ui.Button(buttons, "Menu", () => MenuClicked?.Invoke());
        }

        public void Refresh(Run run, List<string> unlocks, int dailyPlace)
        {
            bool won = run.Phase == RunPhase.Won;
            _title.text = won ? "YOU BEAT THE HOUSE" : run.BeatTheHouse ? "ENDLESS RUN OVER" : "THE HOUSE WINS";
            _title.style.color = won || run.BeatTheHouse ? Ui.Gold : Ui.Bad;
            var table = run.Table;
            string how = won
                ? $"All {Balance.Floors} floors cleared with {run.Gambler.Gold}g in your pocket."
                : $"Out of Hands on floor {run.Floor} at the {Hud.StageName(run.Stage, run.Floor)}: {table.Score:N0} of {table.Target:N0}.";
            _text.text = $"{how}\nTotal score {run.TotalScore:N0}  ·  best hand {run.BestHand:N0}  ·  tables beaten {run.TablesBeaten}" +
                         (run.Options.Daily ? dailyPlace > 0 ? $"\nDaily Run: #{dailyPlace} on this device today" : "\nDaily Run: not in today's top 5" : "");
            _unlocks.text = unlocks.Count > 0 ? "Unlocked: " + string.Join(", ", unlocks) : "";
            _code.text = $"Run code {run.Code}  ({Decks.Name(run.Options.Deck)}, {Stakes.Name(run.Options.Stake)})";
        }
    }
}
