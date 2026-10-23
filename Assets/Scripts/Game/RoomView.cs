using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // a room you walked into from the map: a drawn scene, the person who runs it talking to you, and what they
    // offer laid out as things on the counter. some choices play a little moment (a coin flip, a box opening)
    // before the result
    public sealed class RoomView
    {
        private readonly VisualElement _root, _sceneSlot, _hostFrame, _props, _stage;
        private readonly Label _sign, _hostName, _hostRole, _speech, _info;
        private readonly VisualElement _bubble;
        private readonly Button _leave;
        private readonly SlotMachine _slots;
        private Run _run;
        private object _shownFor;
        private string _said;
        private string _typing = "";
        private int _typed;
        private bool _busy;

        public event Action<RunInput> InputChosen;
        // a room option that works on a card: (option index, label)
        public event Action<int, string> CardOptionClicked;

        public VisualElement Root => _root;

        public RoomView(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            _root.style.backgroundColor = new Color(0.03f, 0.02f, 0.04f);
            parent.Add(_root);

            _sceneSlot = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(_sceneSlot);
            _root.Add(_sceneSlot);
            _root.Add(new Ambience());

            var content = Ui.Column(_root, 8);
            Ui.Fill(content);
            content.style.alignItems = Align.Center;
            Ui.Padding(content, 16, 30);
            content.pickingMode = PickingMode.Ignore;

            // the room's name on a hanging sign
            var sign = Ui.Column(content);
            sign.style.backgroundColor = new Color(0.04f, 0.03f, 0.03f, 0.85f);
            Ui.Border(sign, 2, Ui.Gold);
            Ui.Rounded(sign, 10);
            Ui.Padding(sign, 4, 22);
            _sign = Ui.Title(sign, "", 32, Ui.Gold);

            // the host and what they're saying
            var talk = Ui.Row(content, 16);
            talk.style.alignItems = Align.FlexStart;
            talk.style.marginTop = 6;
            var host = Ui.Column(talk, 4);
            host.style.alignItems = Align.Center;
            _hostFrame = new VisualElement();
            Ui.Border(_hostFrame, 4, Ui.Gold);
            Ui.Rounded(_hostFrame, 20);
            _hostFrame.style.backgroundColor = Ui.Gold;
            host.Add(_hostFrame);
            _hostName = Ui.Text(host, "", 15, Ui.Cream, Weight.Heavy);
            _hostRole = Ui.Caption(host, "", Ui.Gold);

            _bubble = Ui.Column(talk, 6);
            _bubble.style.width = 470;
            _bubble.style.minHeight = 110;
            _bubble.style.backgroundColor = new Color(0.98f, 0.95f, 0.87f);
            Ui.Rounded(_bubble, 18);
            Ui.Padding(_bubble, 14, 18);
            Ui.Border(_bubble, 3, Ui.Gold);
            _bubble.style.marginTop = 16;
            var tail = new VisualElement { pickingMode = PickingMode.Ignore };
            tail.style.position = Position.Absolute;
            tail.style.left = -14;
            tail.style.top = 26;
            tail.style.width = tail.style.height = 22;
            tail.style.backgroundColor = new Color(0.98f, 0.95f, 0.87f);
            tail.style.rotate = new Rotate(45);
            tail.style.borderLeftWidth = tail.style.borderBottomWidth = 3;
            tail.style.borderLeftColor = tail.style.borderBottomColor = Ui.Gold;
            _bubble.Add(tail);
            _speech = Ui.Text(_bubble, "", 17, Ui.Ink, Weight.Medium);
            _speech.style.whiteSpace = WhiteSpace.Normal;
            // clicking the bubble finishes the line
            _bubble.RegisterCallback<ClickEvent>(_ => _typed = _typing.Length);

            _info = Ui.Text(content, "", 12, new Color(1, 1, 1, 0.6f), Weight.Medium);
            _info.style.whiteSpace = WhiteSpace.Normal;
            _info.style.maxWidth = 700;
            _info.style.unityTextAlign = TextAnchor.MiddleCenter;

            // the props sit down on the counter
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
            spacer.style.flexGrow = 1;
            content.Add(spacer);

            _slots = new SlotMachine();
            _slots.Pulled = () => Pick(0);
            content.Add(_slots);
            _slots.Stopped = () => Say(_run?.Room?.Result);

            // where the coin flips and boxes open
            _stage = new VisualElement { pickingMode = PickingMode.Ignore };
            _stage.style.position = Position.Absolute;
            _stage.style.left = Length.Percent(50);
            _stage.style.top = Length.Percent(42);
            _root.Add(_stage);

            _props = Ui.Row(content, 16);
            _props.style.marginTop = 6;
            _props.style.flexWrap = Wrap.Wrap;
            _props.style.justifyContent = Justify.Center;
            _props.style.alignItems = Align.FlexEnd;
            _props.pickingMode = PickingMode.Ignore;

            _leave = Ui.Button(content, "Leave", () => InputChosen?.Invoke(RunInput.LeaveRoom()), ButtonKind.Primary, "Space");
            _leave.style.marginTop = 4;
            foreach (var child in content.Children()) if (child != spacer) child.style.flexShrink = 0;

            // type out whatever the host is saying
            _root.schedule.Execute(() =>
            {
                if (_typed >= _typing.Length) return;
                _typed = Math.Min(_typing.Length, _typed + 2);
                _speech.text = Ui.Rich(_typing.Substring(0, _typed));
            }).Every(22);
        }

        public void Refresh(Run run)
        {
            _run = run;
            object key = (run.Phase, run.Room);
            bool opened = !Equals(key, _shownFor);
            _shownFor = key;
            var room = run.Room;
            if (opened) Enter(room);

            bool slots = room.Kind == RoomKind.Slots;
            Ui.Show(_slots, slots);
            if (slots) _slots.Set(room);
            // a new result: the host says it (the slot machine waits for its reels to stop)
            if (!string.IsNullOrEmpty(room.Result) && room.Result != _said && !(slots && _slots.Spinning))
            {
                Say(room.Result);
                Ui.Pop(_hostFrame, 1.08f);
            }
            Ui.SetEnabled(_leave, run.IsLegal(RunInput.LeaveRoom()));
            Props();
        }

        private void Enter(Room room)
        {
            var host = Host.For(room);
            _sceneSlot.Clear();
            _sceneSlot.Add(new RoomScene(room.Kind));
            _sign.text = room.Name.ToUpperInvariant();
            _hostFrame.Clear();
            _hostFrame.Add(new BossPortrait(host.Name, room.Kind == RoomKind.Slots ? 104 : 150));
            _hostName.text = host.Name;
            _hostRole.text = host.Role.ToUpperInvariant();
            _info.text = room.Kind == RoomKind.Mystery ? "" : Ui.Rich(Rooms.Describe(room.Kind));
            Ui.Show(_info, room.Kind != RoomKind.Mystery);
            _slots.Reset();
            _said = null;
            _busy = false;
            Say(room.Kind == RoomKind.Mystery ? room.Description + " " + host.Greeting : host.Greeting);

            // the host slides in from the side, the sign drops, the props come up after
            Ui.Enter(_root, 0, 0, 0.3f);
            Ui.Transition(_hostFrame, 0, "translate", "opacity");
            _hostFrame.style.translate = new Translate(-120, 0);
            _hostFrame.style.opacity = 0;
            _hostFrame.schedule.Execute(() =>
            {
                Ui.Transition(_hostFrame, 0.45f, EasingMode.EaseOutBack, "translate", "opacity");
                _hostFrame.style.translate = new Translate(0, 0);
                _hostFrame.style.opacity = 1;
            }).StartingIn(80);
            Ui.Pop(_sign, 1.3f, 60);
            Ui.Enter(_bubble, 14, 250, 0.3f);
        }

        private void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _said = text;
            _typing = text;
            _typed = Settings.ReducedMotion ? text.Length : 0;
            _speech.text = Settings.ReducedMotion ? Ui.Rich(text) : "";
        }

        // what's on offer, as objects on the counter
        private void Props()
        {
            _props.Clear();
            var options = _run.RoomOptions();
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                // the slot machine's lever is the button
                if (option.Kind == RoomActionKind.PullSlots) continue;
                int index = i;
                bool legal = option.NeedsCard ? CanPickAnyCard(index) : _run.IsLegal(RunInput.RoomOption(index));
                _props.Add(Prop(option, index, legal));
            }
        }

        private VisualElement Prop(RoomOption option, int index, bool legal)
        {
            var (shape, color) = PropLook(option);
            var tile = Ui.Column(null, 6);
            tile.style.width = 170;
            tile.style.minHeight = 150;
            Ui.Margin(tile, 7);
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.FlexEnd;
            Ui.Padding(tile, 10, 10);
            tile.style.backgroundImage = Ui.Gradient(new Color(0.18f, 0.12f, 0.08f, 0.95f), new Color(0.07f, 0.05f, 0.04f, 0.95f));
            tile.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Ui.Rounded(tile, 14);
            Ui.Border(tile, 2, legal ? new Color(color.r, color.g, color.b, 0.8f) : new Color(1, 1, 1, 0.12f));

            // a medallion with the thing itself
            var medal = new VisualElement { pickingMode = PickingMode.Ignore };
            medal.style.width = medal.style.height = 64;
            Ui.Rounded(medal, 32);
            medal.style.alignItems = Align.Center;
            medal.style.justifyContent = Justify.Center;
            medal.style.backgroundColor = new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.9f);
            Ui.Border(medal, 2, color);
            medal.Add(new SuitIcon(shape, color, 32) { pickingMode = PickingMode.Ignore });
            tile.Add(medal);

            var label = Ui.Text(tile, option.Label, 14, Ui.Cream, Weight.Bold);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.pickingMode = PickingMode.Ignore;
            if (option.Cost != 0)
                Ui.Pill(tile, option.Cost > 0 ? $"{option.Cost}g" : $"+{-option.Cost}g", option.Cost > 0 ? Ui.Gold : Ui.Good, filled: true, size: 12)
                    .pickingMode = PickingMode.Ignore;
            if (option.NeedsCard) Ui.Text(tile, "pick a card next", 11, Ui.Dim).pickingMode = PickingMode.Ignore;

            tile.style.opacity = legal ? 1 : 0.4f;
            if (option.Kind == RoomActionKind.BuyCocktail)
                Ui.Tip(tile, option.Label, Cocktails.Describe(_run.Room.Cocktails[option.Arg]));
            if (!legal) return tile;
            Ui.Lift(tile, 1.06f);
            // the medallion turns gently so the choices feel alive
            float t = index * 0.9f;
            medal.schedule.Execute(() =>
            {
                if (Settings.ReducedMotion) return;
                t += 0.05f;
                medal.style.rotate = new Rotate(Mathf.Sin(t * 1.6f) * 6);
            }).Every(50);
            tile.RegisterCallback<ClickEvent>(_ =>
            {
                if (option.NeedsCard) CardOptionClicked?.Invoke(index, option.Label);
                else Pick(index);
            });
            return tile;
        }

        // take an option, with its little moment first
        private void Pick(int index)
        {
            if (_busy || _run == null) return;
            var input = RunInput.RoomOption(index);
            if (!_run.IsLegal(input)) return;
            var options = _run.RoomOptions();
            var moment = Settings.ReducedMotion ? Moment.None : MomentFor(_run.Room, options[index]);
            if (moment == Moment.None)
            {
                InputChosen?.Invoke(input);
                return;
            }
            _busy = true;
            long ms = Play(moment, options[index]);
            _root.schedule.Execute(() =>
            {
                _busy = false;
                _stage.Clear();
                InputChosen?.Invoke(input);
            }).StartingIn(ms);
        }

        private enum Moment { None, Coin, Box, Slide, Lever }

        private static Moment MomentFor(Room room, RoomOption option) => option.Kind switch
        {
            RoomActionKind.PullSlots => Moment.Lever,
            RoomActionKind.TakeLostVice or RoomActionKind.BuyUsedRare => Moment.Box,
            RoomActionKind.BuyCocktail or RoomActionKind.FreeCocktail or RoomActionKind.Borrow
                or RoomActionKind.Bribe or RoomActionKind.PayOffHeat => Moment.Slide,
            RoomActionKind.EventChoice => (room.Event, option.Arg) switch
            {
                (EventKind.Hustler, _) or (EventKind.SecuritySweep, 1) => Moment.Coin,
                (EventKind.LuckyChip, 0) or (EventKind.BackAlley, _) or (EventKind.Magician, 0) => Moment.Box,
                (EventKind.SpilledTray, 0) => Moment.Slide,
                _ => Moment.None,
            },
            _ => Moment.None,
        };

        // plays the moment on the stage and says how long it takes
        private long Play(Moment moment, RoomOption option)
        {
            _stage.Clear();
            switch (moment)
            {
                case Moment.Lever:
                    return 10;
                case Moment.Coin:
                {
                    // a gold coin spins up and comes down
                    var coin = new SuitIcon(SuitIcon.Shape.Coin, Ui.Gold, 90) { pickingMode = PickingMode.Ignore };
                    coin.style.position = Position.Absolute;
                    coin.style.left = -45;
                    coin.style.top = -45;
                    _stage.Add(coin);
                    for (int f = 0; f <= 30; f++)
                    {
                        int frame = f;
                        coin.schedule.Execute(() =>
                        {
                            float t = frame / 30f;
                            coin.style.scale = new Scale(new Vector2(Mathf.Cos(t * Mathf.PI * 8), 1));
                            coin.style.translate = new Translate(0, -Mathf.Sin(t * Mathf.PI) * 140);
                        }).StartingIn(f * 32);
                    }
                    return 1050;
                }
                case Moment.Box:
                {
                    // a box rattles, then the lid flies off
                    var box = new VisualElement { pickingMode = PickingMode.Ignore };
                    box.style.position = Position.Absolute;
                    box.style.left = -60;
                    box.style.top = -40;
                    box.style.width = 120;
                    box.style.height = 80;
                    box.style.backgroundColor = new Color(0.55f, 0.38f, 0.2f);
                    Ui.Rounded(box, 6);
                    Ui.Border(box, 3, new Color(0.3f, 0.18f, 0.08f));
                    var lid = new VisualElement { pickingMode = PickingMode.Ignore };
                    lid.style.position = Position.Absolute;
                    lid.style.left = -8;
                    lid.style.right = -8;
                    lid.style.top = -18;
                    lid.style.height = 20;
                    lid.style.backgroundColor = new Color(0.62f, 0.44f, 0.24f);
                    Ui.Rounded(lid, 5);
                    Ui.Border(lid, 3, new Color(0.3f, 0.18f, 0.08f));
                    box.Add(lid);
                    var mark = Ui.Title(box, "?", 40, new Color(0.98f, 0.9f, 0.6f), glow: false);
                    mark.style.position = Position.Absolute;
                    mark.style.left = 0;
                    mark.style.right = 0;
                    mark.style.top = 14;
                    mark.style.unityTextAlign = TextAnchor.MiddleCenter;
                    _stage.Add(box);
                    float[] shake = { -6, 6, -8, 8, -5, 5, -3, 0 };
                    for (int i = 0; i < shake.Length; i++)
                    {
                        float angle = shake[i];
                        box.schedule.Execute(() => box.style.rotate = new Rotate(angle)).StartingIn(i * 70);
                    }
                    box.schedule.Execute(() =>
                    {
                        Ui.Transition(lid, 0.35f, "translate", "rotate", "opacity");
                        lid.style.translate = new Translate(60, -90);
                        lid.style.rotate = new Rotate(35);
                        lid.style.opacity = 0;
                        Ui.Pop(box, 1.15f);
                    }).StartingIn(600);
                    return 1000;
                }
                default:
                {
                    // the thing slides along the counter to you
                    var (shape, color) = PropLook(option);
                    var thing = new SuitIcon(shape, color, 70) { pickingMode = PickingMode.Ignore };
                    thing.style.position = Position.Absolute;
                    thing.style.left = -35;
                    thing.style.top = 60;
                    thing.style.translate = new Translate(420, 0);
                    _stage.Add(thing);
                    thing.schedule.Execute(() =>
                    {
                        Ui.Transition(thing, 0.55f, EasingMode.EaseOutCubic, "translate", "rotate");
                        thing.style.translate = new Translate(0, 0);
                        thing.style.rotate = new Rotate(-360);
                    }).StartingIn(20);
                    return 700;
                }
            }
        }

        // the icon and color for an option: drinks, money, cards, boxes...
        private static (SuitIcon.Shape shape, Color color) PropLook(RoomOption option) => option.Kind switch
        {
            RoomActionKind.BuyCocktail or RoomActionKind.FreeCocktail => (SuitIcon.Shape.Glass, Ui.Cocktail),
            RoomActionKind.PlayBackRoom => (SuitIcon.Shape.Spade, Ui.Bad),
            RoomActionKind.SellViceFull or RoomActionKind.Borrow or RoomActionKind.Bribe => (SuitIcon.Shape.Coin, Ui.Gold),
            RoomActionKind.BuyUsedRare or RoomActionKind.TakeLostVice => (SuitIcon.Shape.Star, new Color(1f, 0.6f, 0.4f)),
            RoomActionKind.RemoveCard => (SuitIcon.Shape.Skull, Ui.Bad),
            RoomActionKind.DuplicateCard => (SuitIcon.Shape.Cards, Ui.Chips),
            RoomActionKind.EnhanceCard => (SuitIcon.Shape.Star, Ui.Gold),
            RoomActionKind.PayOffHeat => (SuitIcon.Shape.Eye, Ui.Good),
            RoomActionKind.SkipNextTable => (SuitIcon.Shape.Crown, new Color(1f, 0.75f, 0.85f)),
            RoomActionKind.EventChoice when option.Cost < 0 => (SuitIcon.Shape.Coin, Ui.Gold),
            RoomActionKind.EventChoice => option.Label.StartsWith("Grab") ? (SuitIcon.Shape.Glass, Ui.Cocktail)
                : option.Label.StartsWith("Learn") ? (SuitIcon.Shape.Spade, new Color(0.55f, 0.85f, 1f))
                : option.Label.StartsWith("Shred") ? (SuitIcon.Shape.Skull, Ui.Bad)
                : option.Label.StartsWith("Copy") ? (SuitIcon.Shape.Cards, Ui.Chips)
                : option.Label.Contains("Heat") ? (SuitIcon.Shape.Eye, Ui.HeatColor)
                : option.Label.Contains("coin") || option.Label.Contains("Bet") ? (SuitIcon.Shape.Coin, Ui.Gold)
                : (SuitIcon.Shape.Star, new Color(0.8f, 0.7f, 1f)),
            _ => (SuitIcon.Shape.Star, Ui.Gold),
        };

        private bool CanPickAnyCard(int option)
        {
            for (int c = 0; c < _run.Gambler.Deck.Count; c++)
                if (_run.IsLegal(RunInput.RoomOption(option, c))) return true;
            return false;
        }

        public static SuitIcon.Shape RoomIcon(RoomKind kind) => kind switch
        {
            RoomKind.Bar => SuitIcon.Shape.Glass,
            RoomKind.BackRoom => SuitIcon.Shape.Spade,
            RoomKind.PawnShop => SuitIcon.Shape.Coin,
            RoomKind.LoanShark => SuitIcon.Shape.Skull,
            RoomKind.CardRoom => SuitIcon.Shape.Cards,
            RoomKind.LostAndFound => SuitIcon.Shape.Star,
            RoomKind.SecurityOffice => SuitIcon.Shape.Eye,
            RoomKind.Elite => SuitIcon.Shape.Flame,
            RoomKind.Mystery => SuitIcon.Shape.Star,
            RoomKind.Shop => SuitIcon.Shape.Diamond,
            RoomKind.Slots => SuitIcon.Shape.Coin,
            _ => SuitIcon.Shape.Crown,
        };
    }
}
