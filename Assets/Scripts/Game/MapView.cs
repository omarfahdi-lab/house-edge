using System;
using System.Collections.Generic;
using System.Linq;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the floor drawn as an old casino plan on parchment, like Slay the Spire's map: Low Table, branching stops,
    // High Table, branching stops, Pit Boss. between tables you pick your next stop here
    public sealed class MapView
    {
        private const float Width = 940, Height = 410;
        // x of each column: Low, two rows of stops, High, two rows of stops, boss
        private static readonly float[] Columns = { 70, 210, 340, 470, 600, 730, 870 };
        private static readonly float[] Lanes = { 75, 205, 335 };

        private static readonly Color InkColor = new Color(0.24f, 0.15f, 0.09f);
        private static readonly Color RedInk = new Color(0.62f, 0.13f, 0.1f);

        // each floor of the casino has its own name and paper
        private static readonly (string name, Color paper, Color accent)[] Themes =
        {
            ("The Lobby", new Color(0.93f, 0.86f, 0.7f), new Color(0.55f, 0.36f, 0.12f)),
            ("The Slot Hall", new Color(0.94f, 0.82f, 0.74f), new Color(0.66f, 0.2f, 0.18f)),
            ("The Velvet Room", new Color(0.86f, 0.8f, 0.86f), new Color(0.42f, 0.18f, 0.48f)),
            ("The Card Pit", new Color(0.84f, 0.88f, 0.76f), new Color(0.18f, 0.42f, 0.24f)),
            ("The Penthouse Bar", new Color(0.95f, 0.84f, 0.7f), new Color(0.72f, 0.38f, 0.1f)),
            ("The Vault", new Color(0.8f, 0.84f, 0.86f), new Color(0.2f, 0.32f, 0.46f)),
            ("The Eye's Nest", new Color(0.86f, 0.78f, 0.64f), new Color(0.5f, 0.12f, 0.12f)),
            ("The House", new Color(0.95f, 0.85f, 0.56f), new Color(0.6f, 0.42f, 0.05f)),
        };

        public static string FloorName(int floor) => Themes[(floor - 1) % Themes.Length].name;

        private readonly VisualElement _root, _scroll, _canvas, _climb, _receipt;
        private readonly Label _title, _subtitle, _floorName;
        private readonly Button _close;
        private readonly Paths _paths;
        private bool _choosing;
        private string _shownKey;
        private Run _run;

        public event Action<RunInput> InputChosen;

        // you, as a chip on the map, and whether it's walking to a stop right now
        private VisualElement _token;
        private Vector2 _tokenAt;
        private bool _walking;
        private List<PathStop> _choices = new List<PathStop>();

        public VisualElement Root => _root;
        public bool IsOpen => _root.style.display == DisplayStyle.Flex;
        // the map is open just to look, not to pick a stop
        public bool Viewing => IsOpen && !_choosing;

        public MapView(VisualElement parent)
        {
            _root = Ui.Column(parent, 6);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            _root.style.backgroundColor = Ui.FeltDark;
            Ui.Stretch(_root, Ui.FeltTexture());
            _root.style.alignItems = Align.Center;
            Ui.Padding(_root, 10, 16);

            _title = Ui.Title(_root, "", 30, Ui.Gold);
            _subtitle = Ui.Body(_root, "", 14, Ui.Cream, center: true);
            _subtitle.style.maxWidth = 860;
            _receipt = Ui.Row(_root, 6);
            _climb = Ui.Row(_root, 4);

            // the parchment
            _scroll = new VisualElement();
            _scroll.style.width = Width + 20;
            _scroll.style.height = Height + 50;
            _scroll.style.marginTop = 4;
            Ui.Rounded(_scroll, 8);
            Ui.Border(_scroll, 3, new Color(0.35f, 0.22f, 0.1f));
            _root.Add(_scroll);
            _floorName = Ui.Title(_scroll, "", 18, InkColor, glow: false);
            _floorName.style.position = Position.Absolute;
            _floorName.style.top = 8;
            _floorName.style.left = 0;
            _floorName.style.right = 0;
            _floorName.style.unityTextAlign = TextAnchor.MiddleCenter;
            _canvas = new VisualElement();
            _canvas.style.position = Position.Absolute;
            _canvas.style.left = 10;
            _canvas.style.top = 40;
            _canvas.style.width = Width;
            _canvas.style.height = Height;
            _scroll.Add(_canvas);
            _paths = new Paths();
            Ui.Fill(_paths);

            _close = Ui.Button(_root, "Continue", Close, ButtonKind.Gold, "Space");
            _close.style.marginTop = 6;
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
            // nothing changed: leave it exactly where it is, so it doesn't jump
            string key = $"{run.Floor}|{run.Phase}|{run.Here?.Section},{run.Here?.Row},{run.Here?.Lane}|{run.Stage}|{choosing}|{run.Map.All().Count(s => s.Visited)}";
            if (IsOpen && key == _shownKey) return;
            bool wasOpen = IsOpen;
            _shownKey = key;
            _run = run;
            Tooltip.HideAll();
            _choosing = choosing;
            Ui.Show(_close, !choosing);
            var map = run.Map;
            var next = run.NextStage;
            var theme = Themes[(run.Floor - 1) % Themes.Length];

            _title.text = choosing ? "PICK YOUR PATH" : "THE MAP";
            _subtitle.text = Ui.Rich(choosing
                ? $"Choose your next stop. Paths only go forward, so plan your route. Every path ends at the {Hud.StageName(next.stage, next.floor)}. Hover a stop to see where it leads."
                : "Shops and rooms sit on branching paths between the tables. After each table you pick where to go.");
            _floorName.text = $"FLOOR {run.Floor}  ·  {theme.name.ToUpperInvariant()}";
            _floorName.style.color = theme.accent;
            Ui.Stretch(_scroll, Parchment(theme.paper));

            RefreshReceipt(run, choosing);
            RefreshClimb(run, bosses);

            _canvas.Clear();
            _canvas.Add(new Doodles(theme.accent, run.Floor));
            _canvas.Add(_paths);
            var nextStops = choosing ? run.NextStops.ToList() : new List<PathStop>();
            int progress = Progress(run);
            var nodes = new List<VisualElement>();

            foreach (var (column, kind) in new[] { (0, TableKind.Low), (3, TableKind.High), (6, TableKind.Boss) })
                nodes.Add(Place(TableNode(run, kind, bosses, TableDone(run, column), progress == column && run.Phase == RunPhase.AtTable), TableAt(column)));

            for (int section = 0; section < FloorMap.Sections; section++)
            {
                int from = section * 3;
                for (int row = 0; row < FloorMap.Rows; row++)
                    for (int lane = 0; lane < FloorMap.Lanes; lane++)
                    {
                        var stop = map.Stop(section, row, lane);
                        bool here = run.Here == stop && run.Phase != RunPhase.RoomChoice;
                        nodes.Add(Place(StopNode(stop, nextStops.IndexOf(stop), here), At(from + 1 + row, lane)));
                    }
            }
            _paths.Set(Segments(run, null));
            _choices = nextStops;

            // the token stands where you are: the stop you're at, or the table you just left
            _walking = false;
            _tokenAt = run.Here != null ? At(run.Here.Section * 3 + 1 + run.Here.Row, run.Here.Lane)
                : TableAt(progress >= 6 ? 6 : progress >= 3 ? 3 : 0);
            _token = Token();
            _token.style.left = _tokenAt.x - 25;
            _token.style.top = _tokenAt.y - 86;
            _canvas.Add(_token);

            Ui.Show(_root, true);
            if (wasOpen) return;
            // unroll: the parchment opens, then the stops appear left to right
            Ui.Enter(_root, 0, 0, 0.25f);
            Ui.Transition(_scroll, 0, "scale", "opacity");
            _scroll.style.scale = new Scale(new Vector2(0.94f, 0.94f));
            _scroll.style.opacity = 0;
            _scroll.schedule.Execute(() =>
            {
                Ui.Transition(_scroll, 0.35f, EasingMode.EaseOutBack, "scale", "opacity");
                _scroll.style.scale = new Scale(Vector2.one);
                _scroll.style.opacity = 1;
            }).StartingIn(30);
            foreach (var node in nodes)
            {
                var inner = node.ElementAt(0);
                inner.style.opacity = 0;
                inner.schedule.Execute(() =>
                {
                    Ui.Transition(inner, 0.3f, EasingMode.EaseOutBack, "opacity", "scale");
                    inner.style.opacity = 1;
                    Ui.Pop(inner, 0.6f);
                }).StartingIn(120 + (long)(node.style.left.value.value * 0.6f));
            }
        }

        // every path on the floor; the walked ones in red ink, and the ones a hovered stop leads to highlighted
        private static List<Segment> Segments(Run run, PathStop preview)
        {
            var map = run.Map;
            var list = new List<Segment>();
            int progress = Progress(run);
            var reach = new HashSet<PathStop>();
            if (preview != null)
            {
                reach.Add(preview);
                foreach (var after in map.NextFrom(preview.Section, preview.Row, preview.Lane)) reach.Add(after);
            }
            for (int section = 0; section < FloorMap.Sections; section++)
            {
                int from = section * 3, to = from + 3;
                for (int lane = 0; lane < FloorMap.Lanes; lane++)
                {
                    var first = map.Stop(section, 0, lane);
                    var last = map.Stop(section, FloorMap.Rows - 1, lane);
                    bool startHere = run.Phase == RunPhase.RoomChoice && run.Here == null && progress == from;
                    list.Add(new Segment(TableAt(from), At(from + 1, lane), first.Visited, startHere && reach.Contains(first)));
                    list.Add(new Segment(At(to - 1, lane), TableAt(to), last.Visited && progress >= to, reach.Contains(last)));
                    for (int row = 0; row < FloorMap.Rows - 1; row++)
                        for (int toLane = 0; toLane < FloorMap.Lanes; toLane++)
                            if (map.Linked(section, row, lane, toLane))
                            {
                                var a = map.Stop(section, row, lane);
                                var b = map.Stop(section, row + 1, toLane);
                                bool walked = a.Visited && b.Visited;
                                bool lit = reach.Contains(a) && reach.Contains(b) || run.Here == a && reach.Contains(b);
                                list.Add(new Segment(At(from + 1 + row, lane), At(from + 2 + row, toLane), walked, lit));
                            }
                }
            }
            return list;
        }

        // what the last table paid, when you've just come from it
        private void RefreshReceipt(Run run, bool choosing)
        {
            _receipt.Clear();
            bool fresh = choosing && run.Here == null;
            bool eliteLost = choosing && run.Here?.Kind == RoomKind.Elite && run.EliteResult != null;
            Ui.Show(_receipt, fresh || eliteLost);
            if (eliteLost)
            {
                Ui.Pill(_receipt, run.EliteResult.ToUpperInvariant(), Ui.Bad, filled: true, size: 12);
                return;
            }
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

        // walk the token along the path to a stop, then go in
        public void Choose(int index)
        {
            if (_walking || index < 0 || index >= _choices.Count) return;
            var stop = _choices[index];
            var to = At(stop.Section * 3 + 1 + stop.Row, stop.Lane);
            if (Settings.ReducedMotion || _token == null)
            {
                InputChosen?.Invoke(RunInput.ChooseRoom(index));
                return;
            }
            _walking = true;
            var from = _tokenAt;
            var token = _token;
            const int frames = 22;
            Ui.Transition(token, 0, "left", "top");
            for (int f = 1; f <= frames + 6; f++)
            {
                int frame = f;
                token.schedule.Execute(() =>
                {
                    if (frame <= frames)
                    {
                        // three little hops along the dotted line
                        float t = frame / (float)frames;
                        float eased = t * t * (3 - 2 * t);
                        var at = Vector2.Lerp(from, to, eased);
                        float hop = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3)) * 16;
                        token.style.left = at.x - 25;
                        token.style.top = at.y - 86 - hop;
                        if (frame % 7 == 0) Footstep?.Invoke();
                    }
                    else if (frame == frames + 1) Ui.Pop(token, 1.3f);
                    else if (frame == frames + 6)
                    {
                        _walking = false;
                        InputChosen?.Invoke(RunInput.ChooseRoom(index));
                    }
                }).StartingIn(frame * 24);
            }
        }

        // a footstep while the token walks, for the sound
        public event Action Footstep;

        // you on the map: a gold chip on a pin, bobbing gently
        private static VisualElement Token()
        {
            var token = Ui.Column(null);
            token.style.position = Position.Absolute;
            token.style.width = 50;
            token.style.alignItems = Align.Center;
            token.pickingMode = PickingMode.Ignore;
            var chip = new PokerChip(new Color(0.85f, 0.12f, 0.16f), Ui.Gold, 48);
            chip.pickingMode = PickingMode.Ignore;
            token.Add(chip);
            var you = Ui.Text(chip, "YOU", 10, Ui.Cream, Weight.Heavy);
            you.style.position = Position.Absolute;
            you.style.left = you.style.right = 0;
            you.style.top = 17;
            you.style.unityTextAlign = TextAnchor.MiddleCenter;
            var pin = new VisualElement { pickingMode = PickingMode.Ignore };
            pin.style.width = 3;
            pin.style.height = 22;
            pin.style.backgroundColor = new Color(0.25f, 0.15f, 0.1f);
            token.Add(pin);
            float t = 0;
            chip.schedule.Execute(() =>
            {
                if (Settings.ReducedMotion) return;
                t += 0.05f;
                chip.style.translate = new Translate(0, Mathf.Sin(t * 2.4f) * 2.5f);
            }).Every(50);
            return token;
        }

        // nodes are an outer box that holds the position and an inner one that animates,
        // so hover effects never fight the centering
        private VisualElement Place(VisualElement inner, Vector2 point)
        {
            var outer = new VisualElement { pickingMode = PickingMode.Ignore };
            outer.style.position = Position.Absolute;
            outer.style.left = point.x;
            outer.style.top = point.y;
            outer.style.width = 0;
            outer.style.height = 0;
            outer.style.alignItems = Align.Center;
            outer.style.justifyContent = Justify.Center;
            outer.style.overflow = Overflow.Visible;
            inner.style.position = Position.Absolute;
            inner.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.style.position = Position.Absolute;
            holder.Add(inner);
            outer.Add(holder);
            _canvas.Add(outer);
            return outer;
        }

        private static VisualElement TableNode(Run run, TableKind kind, IReadOnlyDictionary<int, string> bosses, bool done, bool now)
        {
            var node = Ui.Column(null, 3);
            node.style.alignItems = Align.Center;
            node.style.width = 130;
            float size = kind == TableKind.Boss ? 66 : 54;
            var holder = new VisualElement();
            holder.style.width = holder.style.height = size + 10;
            holder.style.alignItems = Align.Center;
            holder.style.justifyContent = Justify.Center;
            Ui.Rounded(holder, (size + 10) / 2);
            Ui.Border(holder, 3, now ? Ui.Gold : InkColor);
            holder.style.backgroundColor = new Color(0.98f, 0.94f, 0.84f);
            node.Add(holder);
            if (kind == TableKind.Boss && (run.Floor == Balance.Floors || bosses.ContainsKey(run.Floor)))
            {
                var face = new BossPortrait(run.Floor == Balance.Floors ? "The House" : bosses[run.Floor], size);
                Ui.Rounded(face, size / 2);
                holder.Add(face);
            }
            else
            {
                var color = kind switch
                {
                    TableKind.Low => new Color(0.13f, 0.58f, 0.3f),
                    TableKind.High => new Color(0.8f, 0.14f, 0.16f),
                    _ => new Color(0.13f, 0.13f, 0.15f),
                };
                holder.Add(new PokerChip(color, new Color(0.97f, 0.96f, 0.92f), size));
            }
            if (done) holder.Add(Stamp());
            string name = kind == TableKind.Boss
                ? run.Floor == Balance.Floors ? "The House" : bosses.TryGetValue(run.Floor, out var boss) ? boss : "Pit Boss"
                : Hud.StageName(kind, run.Floor);
            var label = Ui.Text(node, name, 13, now ? RedInk : InkColor, Weight.Heavy);
            label.style.unityTextAlign = TextAnchor.UpperCenter;
            long target = run.Stage == kind ? run.CurrentTarget : run.TargetFor(kind);
            Ui.Text(node, $"target {target:N0}", 11, new Color(0.35f, 0.25f, 0.15f), Weight.Medium);
            string tip = $"Score {target:N0} to clear it.";
            if (kind == TableKind.Boss && run.FloorBoss != null)
                tip = $"{run.FloorBoss.Ability}: {run.FloorBoss.Twist}\n{tip} Beat them for a Rare Vice, then a Shop.";
            Ui.Tip(node, name, tip);
            if (!done && !now) node.style.opacity = 0.85f;
            return node;
        }

        // a red ink tick over somewhere you've been
        private static VisualElement Stamp()
        {
            var check = new SuitIcon(SuitIcon.Shape.Check, RedInk, 26);
            check.style.position = Position.Absolute;
            check.style.right = -8;
            check.style.top = -8;
            check.style.rotate = new Rotate(-12);
            return check;
        }

        private VisualElement StopNode(PathStop stop, int choice, bool here)
        {
            bool open = choice >= 0;
            var node = Ui.Column(null, 3);
            node.style.alignItems = Align.Center;
            node.style.width = 110;
            bool shop = stop.Kind == RoomKind.Shop;
            bool elite = stop.Kind == RoomKind.Elite;
            var ink = shop ? new Color(0.14f, 0.28f, 0.52f) : elite ? RedInk : InkColor;
            var disc = new VisualElement();
            disc.style.width = disc.style.height = 48;
            disc.style.alignItems = Align.Center;
            disc.style.justifyContent = Justify.Center;
            Ui.Rounded(disc, 24);
            disc.style.backgroundColor = open ? new Color(1f, 0.93f, 0.7f) : new Color(0.97f, 0.92f, 0.8f, 0.85f);
            Ui.Border(disc, open || here ? 3 : 2, open ? Ui.Gold : here ? RedInk : ink);
            if (stop.Kind == RoomKind.Mystery || stop.Kind == RoomKind.Slots)
            {
                bool slots = stop.Kind == RoomKind.Slots;
                var mark = Ui.Title(disc, slots ? "7" : "?", 28, slots ? RedInk : ink, glow: false);
                mark.pickingMode = PickingMode.Ignore;
            }
            else disc.Add(new SuitIcon(StopIcon(stop.Kind), ink, 24));
            if (elite) Ui.Border(disc, 3, RedInk);
            if (stop.Visited) disc.Add(Stamp());
            node.Add(disc);
            var label = Ui.Text(node, Rooms.Name(stop.Kind), 12, open ? RedInk : InkColor, Weight.Bold);
            label.style.unityTextAlign = TextAnchor.UpperCenter;
            if (open) Ui.Keycap(node, (choice + 1).ToString(), dark: true);

            Ui.Tip(node, Rooms.Name(stop.Kind), Rooms.Describe(stop.Kind) + (open ? "\nClick to go here." : ""));
            if (!open && !stop.Visited && !here) node.style.opacity = _choosing ? 0.55f : 0.85f;
            if (!open) return node;

            int index = choice;
            // the disc lifts, not the node: the node's translate keeps it centred on its spot
            Ui.Lift(disc, 1.15f);
            node.RegisterCallback<ClickEvent>(_ => Choose(index));
            // hovering a choice lights up the paths it opens
            node.RegisterCallback<PointerEnterEvent>(_ => _paths.Set(Segments(_run, stop)));
            node.RegisterCallback<PointerLeaveEvent>(_ => _paths.Set(Segments(_run, null)));
            // the choices shimmer so they read as clickable
            float t = choice * 0.6f;
            disc.schedule.Execute(() =>
            {
                t += 0.016f;
                float glow = 0.5f + 0.5f * Mathf.Sin(t * 4);
                Ui.BorderColor(disc, Color.Lerp(Ui.Gold, RedInk, glow * 0.5f));
            }).Every(16).Until(() => disc.panel == null);
            return node;
        }

        public static SuitIcon.Shape StopIcon(RoomKind kind) => kind == RoomKind.Shop ? SuitIcon.Shape.Diamond : RoomView.RoomIcon(kind);

        public void Close()
        {
            Tooltip.HideAll();
            _shownKey = null;
            Ui.Show(_root, false);
        }

        // old paper: warm grain, darker edges and a few stains, tinted per floor
        private static readonly Dictionary<Color, Texture2D> Papers = new Dictionary<Color, Texture2D>();

        private static Texture2D Parchment(Color paper)
        {
            if (Papers.TryGetValue(paper, out var cached)) return cached;
            const int w = 480, h = 240;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[w * h];
            var random = new System.Random(5);
            var stains = Enumerable.Range(0, 7).Select(_ => (x: random.Next(w), y: random.Next(h), r: 12 + random.Next(40))).ToList();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x) / (w * 0.12f), Mathf.Min(y, h - 1 - y) / (h * 0.16f));
                    float burn = 1 - Mathf.Clamp01(edge);
                    float grain = (float)(random.NextDouble() - 0.5) * 0.05f;
                    float fiber = Mathf.Sin(x * 0.9f + y * 0.13f) * 0.012f;
                    float stain = 0;
                    foreach (var s in stains)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(s.x, s.y)) / s.r;
                        if (d < 1) stain += (1 - d) * 0.06f;
                    }
                    var c = paper * (1 - 0.45f * burn * burn - stain) + new Color(grain + fiber, grain + fiber, grain * 0.6f, 0);
                    c.a = 1;
                    pixels[y * w + x] = c;
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Papers[paper] = texture;
        }

        private readonly struct Segment
        {
            public readonly Vector2 From, To;
            public readonly bool Walked, Lit;

            public Segment(Vector2 from, Vector2 to, bool walked, bool lit)
            {
                From = from;
                To = to;
                Walked = walked;
                Lit = lit;
            }
        }

        // ink paths: dotted where you could go, solid red where you went, gold where a hovered stop leads
        private sealed class Paths : VisualElement
        {
            private List<Segment> _segments = new List<Segment>();

            public Paths()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            public void Set(List<Segment> segments)
            {
                _segments = segments;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                p.lineCap = LineCap.Round;
                foreach (var s in _segments.OrderBy(s => s.Walked || s.Lit))
                {
                    var color = s.Lit ? new Color(0.85f, 0.6f, 0.1f) : s.Walked ? RedInk : new Color(InkColor.r, InkColor.g, InkColor.b, 0.55f);
                    p.strokeColor = color;
                    p.lineWidth = s.Walked || s.Lit ? 4 : 2.5f;
                    var dir = s.To - s.From;
                    float length = dir.magnitude;
                    // keep clear of the circles at both ends
                    var a = s.From + dir.normalized * 30;
                    var b = s.To - dir.normalized * 30;
                    if (s.Walked || s.Lit)
                    {
                        p.BeginPath();
                        p.MoveTo(a);
                        p.LineTo(b);
                        p.Stroke();
                        continue;
                    }
                    // dashes, like a pencilled route
                    float span = (b - a).magnitude;
                    var unit = (b - a) / Mathf.Max(1, span);
                    p.BeginPath();
                    for (float d = 0; d < span; d += 11)
                    {
                        p.MoveTo(a + unit * d);
                        p.LineTo(a + unit * Mathf.Min(span, d + 5));
                    }
                    p.Stroke();
                }
            }
        }

        // faint suit marks and a compass in the corner, so each floor's paper looks hand-drawn
        private sealed class Doodles : VisualElement
        {
            private readonly Color _ink;
            private readonly int _seed;

            public Doodles(Color ink, int seed)
            {
                _ink = ink;
                _seed = seed;
                Ui.Fill(this);
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                var random = new System.Random(_seed * 31);
                p.fillColor = new Color(_ink.r, _ink.g, _ink.b, 0.08f);
                for (int i = 0; i < 14; i++)
                {
                    var c = new Vector2(random.Next(20, (int)Width - 20), random.Next(10, (int)Height - 10));
                    float r = 6 + random.Next(8);
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(0, -r));
                    p.LineTo(c + new Vector2(r * 0.8f, 0));
                    p.LineTo(c + new Vector2(0, r));
                    p.LineTo(c + new Vector2(-r * 0.8f, 0));
                    p.ClosePath();
                    p.Fill();
                }
                // a compass rose
                var centre = new Vector2(Width - 40, Height - 34);
                p.fillColor = new Color(_ink.r, _ink.g, _ink.b, 0.25f);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI / 2;
                    var tip = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 24;
                    var left = centre + new Vector2(Mathf.Cos(a + 0.5f), Mathf.Sin(a + 0.5f)) * 7;
                    var right = centre + new Vector2(Mathf.Cos(a - 0.5f), Mathf.Sin(a - 0.5f)) * 7;
                    p.BeginPath();
                    p.MoveTo(tip);
                    p.LineTo(left);
                    p.LineTo(centre);
                    p.LineTo(right);
                    p.ClosePath();
                    p.Fill();
                }
                p.strokeColor = new Color(_ink.r, _ink.g, _ink.b, 0.25f);
                p.lineWidth = 1.5f;
                p.BeginPath();
                p.Arc(centre, 16, 0, 360);
                p.Stroke();
            }
        }
    }
}
