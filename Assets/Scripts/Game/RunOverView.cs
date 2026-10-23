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
        private readonly VisualElement _root, _card, _stats, _graphBox;
        private readonly ScoreGraph _graph;
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

            // every table of the run as a bar against its target
            _graphBox = Ui.Column(_card, 4);
            _graphBox.style.alignSelf = Align.Stretch;
            var graphHead = Ui.Row(_graphBox, 12);
            Ui.Caption(graphHead, "TABLE BY TABLE", Ui.Gold).style.flexGrow = 1;
            Legend(graphHead, "Low", ScoreGraph.LowColor);
            Legend(graphHead, "High", ScoreGraph.HighColor);
            Legend(graphHead, "Boss", ScoreGraph.BossColor);
            Legend(graphHead, "Elite", ScoreGraph.EliteColor);
            _graph = new ScoreGraph();
            _graph.style.height = 120;
            _graphBox.Add(_graph);
            Ui.Text(_graphBox, "Each bar is your score at one table. The dashed line is the target: past it you cleared the table.", 11, Ui.Dim);
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

            _graph.Show(run.History, !wasShown);
            Ui.Show(_graphBox, run.History.Count > 0);

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

        private static void Legend(VisualElement parent, string name, Color color)
        {
            var row = Ui.Row(parent, 4);
            var swatch = new VisualElement();
            swatch.style.width = swatch.style.height = 9;
            swatch.style.backgroundColor = color;
            Ui.Rounded(swatch, 2);
            row.Add(swatch);
            Ui.Text(row, name, 11, Ui.Dim);
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
    // bars of score over target, one per table, that grow in from the left
    public sealed class ScoreGraph : VisualElement
    {
        public static readonly Color LowColor = new Color(0.86f, 0.84f, 0.76f);
        public static readonly Color HighColor = new Color(0.98f, 0.79f, 0.32f);
        public static readonly Color BossColor = new Color(1f, 0.43f, 0.38f);
        public static readonly Color EliteColor = new Color(1f, 0.55f, 0.2f);

        // three times the target fills the graph
        private const float Ceiling = 3;

        private IReadOnlyList<TableRecord> _history = new List<TableRecord>();
        private float _grown = 1;

        public ScoreGraph()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            schedule.Execute(() =>
            {
                if (_grown >= 1) return;
                _grown = Mathf.Min(1, _grown + 0.03f);
                MarkDirtyRepaint();
            }).Every(16);
        }

        public void Show(IReadOnlyList<TableRecord> history, bool animate)
        {
            _history = history;
            _grown = animate && !Settings.ReducedMotion ? -0.6f : 1;
            Clear();
            // floor numbers under the first table of each floor
            for (int i = 0; i < history.Count; i++)
            {
                if (i > 0 && history[i - 1].Floor == history[i].Floor) continue;
                var label = Ui.Text(this, $"F{history[i].Floor}", 10, Ui.Dim, Weight.Bold);
                label.style.position = Position.Absolute;
                label.style.bottom = -2;
                label.style.left = Length.Percent(100f * i / history.Count);
            }
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height - 14;
            int n = _history.Count;
            if (w <= 0 || n == 0) return;
            var p = context.painter2D;
            float slot = w / n;
            float bar = Mathf.Min(26, slot * 0.7f);
            for (int i = 0; i < n; i++)
            {
                var record = _history[i];
                // bars rise one after another
                float grow = Mathf.Clamp01(_grown * 1.6f - (float)i / n * 0.6f);
                float share = record.Target > 0 ? Mathf.Min(Ceiling, record.Score / (float)record.Target) : 0;
                float height = Mathf.Max(2, h * share / Ceiling) * EaseOut(grow);
                var color = record.Elite ? EliteColor : record.Stage == TableKind.Boss ? BossColor : record.Stage == TableKind.High ? HighColor : LowColor;
                if (!record.Won) color = new Color(color.r, color.g, color.b, 0.35f);
                float x = slot * i + (slot - bar) / 2;
                p.fillColor = color;
                p.BeginPath();
                p.MoveTo(new Vector2(x, h));
                p.LineTo(new Vector2(x, h - height));
                p.LineTo(new Vector2(x + bar, h - height));
                p.LineTo(new Vector2(x + bar, h));
                p.ClosePath();
                p.Fill();
                if (!record.Won)
                {
                    p.strokeColor = BossColor;
                    p.lineWidth = 2;
                    p.Stroke();
                }
            }

            // the target line, dashed
            float y = h - h / Ceiling;
            p.strokeColor = new Color(1, 1, 1, 0.55f);
            p.lineWidth = 1.5f;
            p.BeginPath();
            for (float x = 0; x < w; x += 10)
            {
                p.MoveTo(new Vector2(x, y));
                p.LineTo(new Vector2(Mathf.Min(w, x + 5), y));
            }
            p.Stroke();
            p.strokeColor = new Color(1, 1, 1, 0.2f);
            p.BeginPath();
            p.MoveTo(new Vector2(0, h));
            p.LineTo(new Vector2(w, h));
            p.Stroke();
        }

        private static float EaseOut(float t) => 1 - (1 - t) * (1 - t) * (1 - t);
    }
}
