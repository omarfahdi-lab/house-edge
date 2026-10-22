using System;
using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the floor laid out like Slay the Spire: Low Table, branching stops, High Table, branching stops, Pit Boss.
    // between tables you pick your next stop here; any other time it's just a look at the road ahead
    public sealed class MapView
    {
        private const float Width = 960, Height = 420;
        // x of each column: Low, two rows of stops, High, two rows of stops, boss
        private static readonly float[] Columns = { 70, 215, 345, 480, 615, 745, 885 };
        private static readonly float[] Lanes = { 70, 210, 350 };

        private readonly VisualElement _root, _canvas, _climb, _receipt;
        private readonly Label _title, _subtitle;
        private readonly Button _close;
        private readonly Paths _paths;
        private bool _choosing;

        public event Action<RunInput> InputChosen;

        public VisualElement Root => _root;
        public bool IsOpen => _root.style.display == DisplayStyle.Flex;
        // the map is open just to look, not to pick a stop
        public bool Viewing => IsOpen && !_choosing;

        public MapView(VisualElement parent)
        {
            _root = Ui.Column(parent, 6);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            _root.style.backgroundColor = new Color(0.01f, 0.05f, 0.035f, 0.98f);
            _root.style.alignItems = Align.Center;
            Ui.Padding(_root, 14, 20);

            _title = Ui.Title(_root, "", 32, Ui.Gold);
            _subtitle = Ui.Body(_root, "", 15, Ui.Cream, center: true);
            _subtitle.style.maxWidth = 820;
            _receipt = Ui.Row(_root, 6);
            _climb = Ui.Row(_root, 4);
            _climb.style.marginTop = 2;

            _canvas = new VisualElement();
            _canvas.style.width = Width;
            _canvas.style.height = Height;
            _canvas.style.marginTop = 4;
            _root.Add(_canvas);
            _paths = new Paths();
            Ui.Fill(_paths);

            _close = Ui.Button(_root, "Continue", Close, ButtonKind.Gold, "Space");
            Ui.Show(_root, false);
        }

        private static Vector2 At(int column, int lane) => new Vector2(Columns[column], Lanes[lane]);

        // a table sits in the middle lane
        private static Vector2 TableAt(int column) => new Vector2(Columns[column], Lanes[1]);

        // how far along the floor you are: 0 Low, 1-2 stops, 3 High, 4-5 stops, 6 boss
        private static int Progress(Run run)
        {
            if (run.Here != null) return 1 + run.Here.Section * 3 + run.Here.Row;
            if (run.Phase == RunPhase.RoomChoice) return run.Stage == TableKind.Low ? 0 : 3;
            return run.Stage switch { TableKind.Low => 0, TableKind.High => 3, _ => 6 };
        }

        // a table is behind you once it's been played
        private static bool TableDone(Run run, int column)
        {
            int progress = Progress(run);
            if (progress > column) return true;
            return progress == column && run.Phase != RunPhase.AtTable;
        }

        public void Open(Run run, IReadOnlyDictionary<int, string> bosses, bool choosing)
        {
            Tooltip.HideAll();
            _choosing = choosing;
            Ui.Show(_close, !choosing);
            var map = run.Map;
            var next = run.NextStage;
            _title.text = $"FLOOR {run.Floor}" + (choosing ? "  ·  PICK YOUR PATH" : "  ·  THE MAP");
            _subtitle.text = Ui.Rich(choosing
                ? $"Choose your next stop. Paths only go forward, so plan your route. Every path ends at the {Hud.StageName(next.stage, next.floor)}."
                : "Shops and rooms sit on branching paths between the tables. After each table you pick where to go.");

            RefreshReceipt(run, choosing);
            RefreshClimb(run, bosses);

            _canvas.Clear();
            _canvas.Add(_paths);
            var segments = new List<(Vector2, Vector2, bool)>();
            var nextStops = choosing ? run.NextStops.ToList() : new List<PathStop>();
            int progress = Progress(run);

            // the three tables
            foreach (var (column, kind) in new[] { (0, TableKind.Low), (3, TableKind.High), (6, TableKind.Boss) })
                Place(TableNode(run, kind, bosses, TableDone(run, column), progress == column && run.Phase == RunPhase.AtTable), TableAt(column));

            for (int section = 0; section < FloorMap.Sections; section++)
            {
                int from = section * 3, to = from + 3;
                for (int lane = 0; lane < FloorMap.Lanes; lane++)
                {
                    var first = map.Stop(section, 0, lane);
                    var last = map.Stop(section, FloorMap.Rows - 1, lane);
                    segments.Add((TableAt(from), At(from + 1, lane), first.Visited));
                    segments.Add((At(to - 1, lane), TableAt(to), last.Visited && TableDone(run, to) || last.Visited && progress >= to));
                    for (int row = 0; row < FloorMap.Rows - 1; row++)
                        for (int toLane = 0; toLane < FloorMap.Lanes; toLane++)
                            if (map.Linked(section, row, lane, toLane))
                                segments.Add((At(from + 1 + row, lane), At(from + 2 + row, toLane),
                                    map.Stop(section, row, lane).Visited && map.Stop(section, row + 1, toLane).Visited));
                }
                for (int row = 0; row < FloorMap.Rows; row++)
                    for (int lane = 0; lane < FloorMap.Lanes; lane++)
                    {
                        var stop = map.Stop(section, row, lane);
                        bool here = run.Here == stop && run.Phase != RunPhase.RoomChoice;
                        Place(StopNode(stop, nextStops.IndexOf(stop), here), At(from + 1 + row, lane));
                    }
            }
            _paths.Set(segments);
            bool wasOpen = IsOpen;
            Ui.Show(_root, true);
            if (!wasOpen) Ui.Enter(_root, 16, 0, 0.3f);
        }

        // what the last table paid, when you've just come from it
        private void RefreshReceipt(Run run, bool choosing)
        {
            _receipt.Clear();
            bool fresh = choosing && run.Here == null;
            Ui.Show(_receipt, fresh);
            if (!fresh) return;
            var p = run.LastPayout;
            Ui.Pill(_receipt, "TABLE CLEARED", Ui.Good, filled: true, size: 12);
            foreach (var (name, gold) in new[] { ("Table", p.Base), ("Unused Hands", p.UnusedHands), ("Interest", p.Interest) })
            {
                var pill = Ui.Glass(_receipt, 5, row: true, radius: 12);
                Ui.Padding(pill, 3, 9);
                Ui.Text(pill, name, 12, Ui.Dim, Weight.Medium);
                Ui.Text(pill, $"+{gold}g", 13, gold > 0 ? Ui.Gold : Ui.Dim, Weight.Heavy);
            }
            var total = Ui.Pill(_receipt, $"+{p.Total}g", Ui.Gold, filled: true, size: 14);
            Ui.Pop(total, 1.4f, 300);
        }

        // the whole climb in one line: eight floors and their bosses
        private void RefreshClimb(Run run, IReadOnlyDictionary<int, string> bosses)
        {
            _climb.Clear();
            int floors = Math.Max(Balance.Floors, run.Floor);
            for (int floor = Math.Max(1, floors - 7); floor <= floors; floor++)
            {
                bool now = floor == run.Floor;
                var pip = Ui.Glass(_climb, 3, row: true, radius: 10);
                Ui.Padding(pip, 3, 8);
                pip.style.backgroundColor = now ? new Color(0.98f, 0.79f, 0.32f, 0.2f) : floor < run.Floor ? new Color(0.98f, 0.79f, 0.32f, 0.08f) : new Color(0, 0, 0, 0.3f);
                Ui.BorderColor(pip, now ? Ui.Gold : Ui.Hairline);
                Ui.Text(pip, floor.ToString(), 12, now ? Ui.Gold : floor < run.Floor ? Ui.Cream : Ui.Dim, Weight.Heavy);
                string boss = floor == Balance.Floors ? "The House" : bosses.TryGetValue(floor, out var name) ? name : "?";
                Ui.Text(pip, boss, 11, floor <= run.Floor ? Ui.Cream : Ui.Dim);
                if (floor < run.Floor) Ui.Add(pip, new SuitIcon(SuitIcon.Shape.Check, Ui.Good, 11));
            }
        }

        private void Place(VisualElement node, Vector2 point)
        {
            node.style.position = Position.Absolute;
            node.style.left = point.x;
            node.style.top = point.y;
            node.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            _canvas.Add(node);
        }

        private static VisualElement TableNode(Run run, TableKind kind, IReadOnlyDictionary<int, string> bosses, bool done, bool now)
        {
            var node = Ui.Column(null, 4);
            node.style.alignItems = Align.Center;
            node.style.width = 120;
            var color = kind switch
            {
                TableKind.Low => new Color(0.13f, 0.58f, 0.3f),
                TableKind.High => new Color(0.8f, 0.14f, 0.16f),
                _ => new Color(0.13f, 0.13f, 0.15f),
            };
            float size = kind == TableKind.Boss ? 64 : 54;
            var holder = new VisualElement();
            holder.style.width = holder.style.height = size + 10;
            holder.style.alignItems = Align.Center;
            holder.style.justifyContent = Justify.Center;
            Ui.Rounded(holder, (size + 10) / 2);
            if (now) Ui.Border(holder, 3, Ui.Gold);
            node.Add(holder);
            holder.Add(new PokerChip(color, kind == TableKind.Boss ? Ui.Gold : new Color(0.97f, 0.96f, 0.92f), size));
            if (kind == TableKind.Boss)
            {
                var icon = new SuitIcon(run.Floor == Balance.Floors ? SuitIcon.Shape.Crown : SuitIcon.Shape.Skull, Ui.Gold, 24);
                icon.style.position = Position.Absolute;
                holder.Add(icon);
            }
            if (done)
            {
                var check = new SuitIcon(SuitIcon.Shape.Check, Ui.Good, 20);
                check.style.position = Position.Absolute;
                check.style.right = 0;
                check.style.top = 0;
                holder.Add(check);
            }
            string name = kind == TableKind.Boss
                ? run.Floor == Balance.Floors ? "The House" : bosses.TryGetValue(run.Floor, out var boss) ? boss : "Pit Boss"
                : Hud.StageName(kind, run.Floor);
            Ui.Text(node, name, 13, now ? Ui.Gold : Ui.Cream, Weight.Bold).style.unityTextAlign = TextAnchor.UpperCenter;
            long target = run.Stage == kind ? run.CurrentTarget : run.TargetFor(kind);
            Ui.Text(node, $"target {target:N0}", 11, Ui.Dim);
            string tip = $"Score {target:N0} to clear it.";
            if (kind == TableKind.Boss && run.FloorBoss != null) tip = $"{run.FloorBoss.Name}: {run.FloorBoss.Twist}\n{tip} Beat it for a Rare Vice, then a Shop.";
            Ui.Tip(node, name, tip);
            if (!done && !now) node.style.opacity = 0.75f;
            return node;
        }

        private VisualElement StopNode(PathStop stop, int choice, bool here)
        {
            bool open = choice >= 0;
            var node = Ui.Column(null, 3);
            node.style.alignItems = Align.Center;
            node.style.width = 110;
            bool shop = stop.Kind == RoomKind.Shop;
            var disc = new VisualElement();
            disc.style.width = disc.style.height = 50;
            disc.style.alignItems = Align.Center;
            disc.style.justifyContent = Justify.Center;
            Ui.Rounded(disc, 25);
            var tint = shop ? new Color(0.2f, 0.36f, 0.62f) : new Color(0.42f, 0.24f, 0.12f);
            disc.style.backgroundImage = Ui.Gradient(Color.Lerp(tint, Color.white, 0.15f), Color.Lerp(tint, Color.black, 0.35f));
            disc.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Ui.Border(disc, open || here ? 3 : 2, open ? Ui.Gold : here ? Ui.Good : stop.Visited ? new Color(0.98f, 0.79f, 0.32f, 0.7f) : new Color(1, 1, 1, 0.25f));
            disc.Add(new SuitIcon(StopIcon(stop.Kind), shop ? new Color(0.75f, 0.88f, 1f) : Ui.Gold, 24));
            node.Add(disc);
            var label = Ui.Text(node, Rooms.Name(stop.Kind), 12, open ? Ui.Gold : Ui.Cream, Weight.Bold);
            label.style.unityTextAlign = TextAnchor.UpperCenter;
            if (open) Ui.Keycap(node, (choice + 1).ToString());

            Ui.Tip(node, Rooms.Name(stop.Kind), Rooms.Describe(stop.Kind) + (open ? "\nClick to go here." : ""));
            if (!open && !stop.Visited && !here) node.style.opacity = _choosing ? 0.45f : 0.8f;
            if (open)
            {
                Ui.Lift(node, 1.1f);
                int index = choice;
                node.RegisterCallback<ClickEvent>(_ => InputChosen?.Invoke(RunInput.ChooseRoom(index)));
                // the choices shimmer so they read as clickable
                float t = choice * 0.6f;
                disc.schedule.Execute(() =>
                {
                    t += 0.016f;
                    float glow = 0.5f + 0.5f * Mathf.Sin(t * 4);
                    Ui.BorderColor(disc, Color.Lerp(Ui.Gold, Color.white, glow * 0.6f));
                }).Every(16).Until(() => disc.panel == null);
            }
            return node;
        }

        public static SuitIcon.Shape StopIcon(RoomKind kind) => kind == RoomKind.Shop ? SuitIcon.Shape.Diamond : RoomView.RoomIcon(kind);

        public void Close()
        {
            Tooltip.HideAll();
            Ui.Show(_root, false);
        }

        // the lines between the points: gold where you've walked, faint elsewhere
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
                foreach (var (from, to, done) in _segments.OrderBy(s => s.done))
                {
                    p.lineWidth = done ? 5 : 2;
                    p.strokeColor = done ? Ui.Gold : new Color(1, 1, 1, 0.2f);
                    p.BeginPath();
                    p.MoveTo(from);
                    p.LineTo(to);
                    p.Stroke();
                }
            }
        }
    }
}
