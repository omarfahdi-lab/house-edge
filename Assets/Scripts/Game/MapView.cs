using System;
using System.Collections.Generic;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the climb as a winding path: every floor's tables, room forks and Pit Boss, where you are and where you've been
    public sealed class MapView
    {
        private const float Width = 900, Height = 570, Row = 64, Top = 46;
        private static readonly float[] Columns = { 70, 240, 410, 580, 760 };

        private readonly VisualElement _root, _canvas;
        private readonly Label _title, _subtitle;
        private readonly Button _close;
        private readonly Paths _paths;

        public VisualElement Root => _root;
        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public MapView(VisualElement parent)
        {
            _root = Ui.Column(parent, 6);
            Ui.Fill(_root);
            _root.style.backgroundColor = Ui.FeltDark;
            Ui.Stretch(_root, Ui.FeltTexture());
            _root.style.alignItems = Align.Center;
            Ui.Padding(_root, 16, 24);

            _title = Ui.Title(_root, "THE CLIMB", 34, Ui.Gold);
            _subtitle = Ui.Body(_root, "", 15, Ui.Cream, center: true);

            _canvas = new VisualElement();
            _canvas.style.width = Width;
            _canvas.style.height = Height;
            _canvas.style.marginTop = 6;
            _root.Add(_canvas);
            _paths = new Paths();
            Ui.Fill(_paths);
            _canvas.Add(_paths);

            var buttons = Ui.Row(_root, 12);
            _close = Ui.Button(buttons, "Continue", Close, ButtonKind.Gold, "Space");
            Ui.Show(_root, false);
        }

        // where each point of a floor sits: the path snakes, so odd floors run left to right and even floors back
        private static Vector2 At(int floor, int column, int firstFloor)
        {
            int row = floor - firstFloor;
            float y = Top + (7 - row) * Row;
            float x = (floor % 2 == 1 ? Columns[column] : Columns[4 - column]) + 40;
            return new Vector2(x, y);
        }

        // column of the current position: 0 Low, 1 the room after it, 2 High, 3 the room after it, 4 the boss
        private static (int column, bool done) PlaceOf(Run run)
        {
            int table = run.Stage switch { TableKind.Low => 0, TableKind.High => 2, _ => 4 };
            return run.Phase switch
            {
                RunPhase.AtTable => (table, false),
                RunPhase.RoomChoice or RunPhase.InRoom => (table + 1, run.Phase == RunPhase.InRoom),
                _ => (table, true),
            };
        }

        public void Open(Run run, IReadOnlyDictionary<(int floor, int slot), RoomKind> rooms, IReadOnlyDictionary<int, string> bosses)
        {
            Tooltip.HideAll();
            _canvas.Clear();
            _canvas.Add(_paths);
            int floors = Math.Max(Balance.Floors, run.Floor);
            int first = Math.Max(1, floors - 7);
            var (here, hereDone) = PlaceOf(run);
            int Index(int floor, int column) => (floor - 1) * 5 + column;
            int current = Index(run.Floor, here);

            _subtitle.text = Ui.Rich($"Floor {run.Floor} of {Balance.Floors}. Each floor: a Low Table, a room, a High Table, a room, then a Pit Boss. Beat The House at the top to win.");

            var segments = new List<(Vector2 from, Vector2 to, bool done)>();
            for (int floor = first; floor <= floors; floor++)
            {
                // the floor number at the start of its row
                var start = At(floor, 0, first);
                var number = Ui.Text(_canvas, floor.ToString(), 20, floor == run.Floor ? Ui.Gold : Ui.Dim, Weight.Heavy);
                number.style.position = Position.Absolute;
                number.style.left = floor % 2 == 1 ? start.x - 70 : start.x + 40;
                number.style.top = start.y - 13;

                for (int column = 0; column < 5; column++)
                {
                    int index = Index(floor, column);
                    bool done = index < current || index == current && hereDone;
                    bool now = index == current;
                    var point = At(floor, column, first);
                    if (column < 4) segments.Add((point, At(floor, column + 1, first), index < current));
                    else if (floor < floors) segments.Add((point, At(floor + 1, 0, first), index < current));

                    var node = column % 2 == 1
                        ? DoorNode(floor, column, rooms, done, now)
                        : TableNode(run, floor, column, bosses, done, now);
                    node.style.position = Position.Absolute;
                    node.style.left = point.x;
                    node.style.top = point.y;
                    node.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                    if (!done && !now) node.style.opacity = 0.5f;
                    _canvas.Add(node);
                    if (now) Here(node, point);
                }
            }
            _paths.Set(segments);
            Ui.Show(_root, true);
            Ui.Enter(_root, 0, 0, 0.3f);
        }

        private static VisualElement TableNode(Run run, int floor, int column, IReadOnlyDictionary<int, string> bosses, bool done, bool now)
        {
            var kind = column switch { 0 => TableKind.Low, 2 => TableKind.High, _ => TableKind.Boss };
            var node = Ui.Column(null, 2);
            node.style.alignItems = Align.Center;
            var color = kind switch
            {
                TableKind.Low => new Color(0.13f, 0.58f, 0.3f),
                TableKind.High => new Color(0.8f, 0.14f, 0.16f),
                _ => new Color(0.13f, 0.13f, 0.15f),
            };
            var chip = new PokerChip(color, kind == TableKind.Boss ? Ui.Gold : new Color(0.97f, 0.96f, 0.92f), kind == TableKind.Boss ? 52 : 40);
            node.Add(chip);
            if (kind == TableKind.Boss)
            {
                var icon = new SuitIcon(floor == Balance.Floors ? SuitIcon.Shape.Crown : SuitIcon.Shape.Skull, Ui.Gold, 20);
                icon.style.position = Position.Absolute;
                icon.style.top = 16;
                node.Add(icon);
            }
            if (done)
            {
                var check = new SuitIcon(SuitIcon.Shape.Check, Ui.Good, 18);
                check.style.position = Position.Absolute;
                check.style.right = -8;
                check.style.top = -4;
                node.Add(check);
            }

            string name = kind switch
            {
                TableKind.Low => "Low",
                TableKind.High => "High",
                _ => floor == Balance.Floors ? "The House" : bosses.TryGetValue(floor, out var boss) ? boss : "Pit Boss ?",
            };
            long target = (long)Math.Round(Balance.Target(floor, kind) * Stakes.TargetFactor(run.Options.Stake));
            if (now && run.Floor == floor) target = run.Table.Target;
            var label = Ui.Text(node, name, kind == TableKind.Boss ? 13 : 12, now ? Ui.Gold : Ui.Cream, Weight.Bold);
            label.style.position = Position.Absolute;
            label.style.top = kind == TableKind.Boss ? 54 : 42;
            label.style.width = 130;
            label.style.left = (kind == TableKind.Boss ? 26 : 20) - 65;
            label.style.unityTextAlign = TextAnchor.UpperCenter;
            Ui.Tip(node, $"Floor {floor}: {Hud.StageName(kind, floor)}", $"Target {target:N0}." +
                (kind == TableKind.Boss ? " A Pit Boss changes the rules for its table. Beat it for a Rare Vice." : ""));
            node.style.width = kind == TableKind.Boss ? 52 : 40;
            return node;
        }

        private static VisualElement DoorNode(int floor, int column, IReadOnlyDictionary<(int floor, int slot), RoomKind> rooms, bool done, bool now)
        {
            int slot = column == 1 ? 0 : 1;
            bool known = rooms.TryGetValue((floor, slot), out var room);
            var node = new VisualElement();
            node.style.width = node.style.height = 30;
            node.style.rotate = new Rotate(45);
            node.style.backgroundColor = known ? new Color(0.42f, 0.24f, 0.12f) : new Color(0, 0, 0, 0.5f);
            Ui.Border(node, 2, known || now ? Ui.Gold : new Color(1, 1, 1, 0.3f));
            Ui.Rounded(node, 5);
            var inner = new VisualElement();
            inner.style.rotate = new Rotate(-45);
            Ui.Fill(inner);
            inner.style.alignItems = Align.Center;
            inner.style.justifyContent = Justify.Center;
            node.Add(inner);
            if (known) Ui.Add(inner, new SuitIcon(RoomView.RoomIcon(room), Ui.Gold, 16));
            else Ui.Text(inner, "?", 15, Ui.Dim, Weight.Heavy);
            Ui.Tip(node, known ? Rooms.Name(room) : "A fork", known ? Rooms.Describe(room) : "Pick one of two random rooms here: deals, gambles and services.");
            return node;
        }

        // a gold ring that pulses around where you are
        private void Here(VisualElement node, Vector2 point)
        {
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.style.position = Position.Absolute;
            ring.style.width = ring.style.height = 70;
            ring.style.left = point.x - 35;
            ring.style.top = point.y - 35;
            Ui.Rounded(ring, 35);
            Ui.Border(ring, 3, Ui.Gold);
            _canvas.Add(ring);
            var you = Ui.Pill(_canvas, "YOU ARE HERE", Ui.Gold, filled: true, size: 10);
            you.style.position = Position.Absolute;
            // beside the point, on the side away from the floor number
            bool right = point.x > Width / 2;
            you.style.left = right ? point.x - 44 : point.x + 44;
            you.style.top = point.y - 9;
            if (right) you.style.translate = new Translate(Length.Percent(-100), 0);
            float t = 0;
            ring.schedule.Execute(() =>
            {
                t += 0.016f;
                float pulse = 1 + 0.12f * Mathf.Sin(t * 5);
                ring.style.scale = new Scale(new Vector2(pulse, pulse));
                ring.style.opacity = 0.6f + 0.4f * Mathf.Sin(t * 5);
            }).Every(16).Until(() => ring.panel == null);
        }

        public void Close()
        {
            Tooltip.HideAll();
            Ui.Show(_root, false);
        }

        // the lines between the points: gold where you've been, faint ahead
        private sealed class Paths : VisualElement
        {
            private List<(Vector2 from, Vector2 to, bool done)> _segments = new List<(Vector2, Vector2, bool)>();

            public Paths()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void Set(List<(Vector2 from, Vector2 to, bool done)> segments)
            {
                _segments = segments;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                foreach (var (from, to, done) in _segments)
                {
                    p.lineWidth = done ? 4 : 2;
                    p.strokeColor = done ? Ui.Gold : new Color(1, 1, 1, 0.22f);
                    p.BeginPath();
                    p.MoveTo(from);
                    p.LineTo(to);
                    p.Stroke();
                }
            }
        }
    }
}
