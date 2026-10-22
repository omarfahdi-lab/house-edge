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
        private readonly VisualElement _root, _card, _stats;
        private readonly Label _title, _text, _unlocks, _code;

        public event Action NewRunClicked, MenuClicked, ReplayClicked;

        public VisualElement Root => _root;

        public RunOverView(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.backgroundColor = new Color(0, 0, 0, 0.78f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            parent.Add(_root);

            _card = Ui.Column(_root, 14);
            _card.style.alignItems = Align.Center;
            _card.style.backgroundColor = Ui.PanelSolid;
            _card.style.width = 680;
            Ui.Padding(_card, 30, 40);
            Ui.Rounded(_card, 18);
            Ui.Border(_card, 2, Ui.Gold);
            _title = Ui.Title(_card, "", 44, Ui.Gold);
            _title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _title.style.whiteSpace = WhiteSpace.Normal;
            _text = Ui.Body(_card, "", 16, Ui.Cream, center: true);
            _stats = Ui.Row(_card, 10);
            _unlocks = Ui.Text(_card, "", 15, Ui.Good, Weight.Bold);
            _unlocks.style.whiteSpace = WhiteSpace.Normal;
            _unlocks.style.unityTextAlign = TextAnchor.MiddleCenter;
            _code = Ui.Text(_card, "", 13, Ui.Dim);
            _code.selection.isSelectable = true;

            var buttons = Ui.Row(_card, 10);
            buttons.style.marginTop = 4;
            Ui.Button(buttons, "New run", () => NewRunClicked?.Invoke(), ButtonKind.Gold, "Space");
            var replay = Ui.Button(buttons, "Watch replay", () => ReplayClicked?.Invoke());
            Ui.Tip(replay, "Replay", "Watch this whole run play back, move by move.");
            Ui.Button(buttons, "Menu", () => MenuClicked?.Invoke(), ButtonKind.Ghost);
        }

        public void Refresh(Run run, List<string> unlocks, int dailyPlace)
        {
            bool wasShown = _root.resolvedStyle.display == DisplayStyle.Flex && _title.text.Length > 0;
            bool won = run.Phase == RunPhase.Won;
            _title.text = won ? "YOU BEAT THE HOUSE" : run.BeatTheHouse ? "ENDLESS RUN OVER" : "THE HOUSE WINS";
            _title.style.color = won || run.BeatTheHouse ? Ui.Gold : Ui.Bad;
            Ui.BorderColor(_card, won || run.BeatTheHouse ? Ui.Gold : Ui.Bad);
            var table = run.Table;
            string how = won
                ? $"All {Balance.Floors} floors cleared with {run.Gambler.Gold}g in your pocket."
                : $"You ran out of Hands on floor {run.Floor}, at the {Hud.StageName(run.Stage, run.Floor)}: {table.Score:N0} of {table.Target:N0}.";
            if (run.Options.Daily) how += dailyPlace > 0 ? $"\nDaily Run: #{dailyPlace} on this device today." : "\nDaily Run: not in today's top 5.";
            _text.text = Ui.Rich(how);

            _stats.Clear();
            Stat("TOTAL SCORE", run.TotalScore.ToString("N0"));
            Stat("BEST HAND", run.BestHand.ToString("N0"));
            Stat("TABLES BEATEN", run.TablesBeaten.ToString());
            Stat("FLOOR", run.Floor.ToString());

            _unlocks.text = unlocks.Count > 0 ? "UNLOCKED: " + string.Join(", ", unlocks) : "";
            Ui.Show(_unlocks, unlocks.Count > 0);
            _code.text = $"Run code {run.Code}   ({Decks.Name(run.Options.Deck)} deck, {Stakes.Name(run.Options.Stake)})";
            if (!wasShown)
            {
                // the card rises after a beat, then the verdict stamps down
                Ui.Enter(_card, 60, 700, 0.5f);
                Ui.Pop(_title, won || run.BeatTheHouse ? 1.8f : 2.6f, 1050);
            }
        }

        private void Stat(string name, string value)
        {
            var box = Ui.Glass(_stats, 2, radius: 10);
            box.style.alignItems = Align.Center;
            box.style.minWidth = 130;
            Ui.Padding(box, 8, 12);
            Ui.Text(box, value, 22, Ui.Cream, Weight.Heavy);
            Ui.Caption(box, name);
        }
    }
}
