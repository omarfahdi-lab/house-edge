using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the floor map's fork (pick one of two rooms) and the room you walk into
    public sealed class RoomView
    {
        private readonly VisualElement _root, _content, _icon;
        private readonly Label _title, _text, _result;
        private readonly Button _leave;
        private Run _run;
        private object _shownFor;

        public event Action<RunInput> InputChosen;
        // a room option that works on a card: (option index, label)
        public event Action<int, string> CardOptionClicked;

        public VisualElement Root => _root;

        public RoomView(VisualElement parent)
        {
            _root = Ui.Column(parent, 10);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            Ui.Padding(_root, 26, 32);
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.03f, 0.02f, 0.04f, 0.88f);

            _icon = Ui.Row(_root);
            _title = Ui.Title(_root, "", 40, Ui.Gold);
            _text = Ui.Body(_root, "", 16, Ui.Cream, center: true);
            _text.style.maxWidth = 680;
            _content = Ui.Row(_root, 22);
            _content.style.marginTop = 14;
            _content.style.flexWrap = Wrap.Wrap;
            _content.style.justifyContent = Justify.Center;
            _content.style.alignItems = Align.FlexStart;
            _result = Ui.Text(_root, "", 18, Ui.Gold, Weight.Bold);
            _result.style.whiteSpace = WhiteSpace.Normal;
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _result.style.maxWidth = 680;
            _leave = Ui.Button(_root, "Leave", () => InputChosen?.Invoke(RunInput.LeaveRoom()), ButtonKind.Primary, "Space");
            _leave.style.marginTop = 8;
        }

        public void Refresh(Run run)
        {
            _run = run;
            _content.Clear();
            _icon.Clear();
            object key = (run.Phase, run.Room);
            bool opened = !Equals(key, _shownFor);
            _shownFor = key;
            if (run.Phase == RunPhase.RoomChoice) RefreshChoice(opened);
            else RefreshRoom(opened);
        }

        private void RefreshChoice(bool opened)
        {
            var (floor, stage) = _run.NextStage;
            _title.text = "PICK A DOOR";
            _text.text = Ui.Rich($"You can only go through one. Behind it, then on to the {Hud.StageName(stage, floor)}.");
            _result.text = "";
            Ui.Show(_leave, false);
            for (int i = 0; i < _run.RoomChoices.Count; i++)
            {
                var kind = _run.RoomChoices[i];
                var input = RunInput.ChooseRoom(i);
                var door = Ui.Column(_content, 10);
                door.style.width = 270;
                door.style.minHeight = 330;
                door.style.backgroundImage = Ui.Gradient(new Color(0.42f, 0.24f, 0.12f), new Color(0.2f, 0.1f, 0.05f));
                door.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                // an arched top like a doorway
                door.style.borderTopLeftRadius = door.style.borderTopRightRadius = 135;
                door.style.borderBottomLeftRadius = door.style.borderBottomRightRadius = 10;
                Ui.Border(door, 3, Ui.Gold);
                Ui.Padding(door, 44, 22);
                door.style.paddingBottom = 18;
                door.style.alignItems = Align.Center;

                var badge = Ui.Column(door);
                badge.style.width = badge.style.height = 74;
                Ui.Rounded(badge, 37);
                badge.style.backgroundColor = new Color(0, 0, 0, 0.35f);
                Ui.Border(badge, 2, new Color(0.98f, 0.79f, 0.32f, 0.6f));
                badge.style.alignItems = Align.Center;
                badge.style.justifyContent = Justify.Center;
                Ui.Add(badge, new SuitIcon(RoomIcon(kind), Ui.Gold, 38));

                var name = Ui.Title(door, Rooms.Name(kind).ToUpperInvariant(), 21, Ui.Cream);
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                name.style.whiteSpace = WhiteSpace.Normal;
                var text = Ui.Body(door, Rooms.Describe(kind), 14, new Color(0.95f, 0.9f, 0.82f), center: true);
                text.style.flexGrow = 1;
                var enter = Ui.Button(door, "Enter", () => InputChosen?.Invoke(input), ButtonKind.Gold, (i + 1).ToString());
                enter.style.alignSelf = Align.Stretch;
                Ui.Lift(door, 1.03f);
                door.RegisterCallback<ClickEvent>(e =>
                {
                    if (e.target == door) InputChosen?.Invoke(input);
                });
                if (opened) Ui.Enter(door, 40, 60 + 140 * i, 0.4f);
            }
        }

        private void RefreshRoom(bool opened)
        {
            var room = _run.Room;
            Ui.Add(_icon, new SuitIcon(RoomIcon(room.Kind), Ui.Gold, 34));
            _title.text = room.Name.ToUpperInvariant();
            _text.text = Ui.Rich(room.Description);
            _result.text = Ui.Rich(room.Result ?? "");
            if (!string.IsNullOrEmpty(room.Result)) Ui.Pop(_result, 1.15f);
            Ui.Show(_leave, true);
            Ui.SetEnabled(_leave, _run.IsLegal(RunInput.LeaveRoom()));

            var options = _run.RoomOptions();
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                int index = i;
                bool legal = option.NeedsCard ? CanPickAnyCard(index) : _run.IsLegal(RunInput.RoomOption(index));
                var tile = new Button(() =>
                {
                    if (option.NeedsCard) CardOptionClicked?.Invoke(index, option.Label);
                    else InputChosen?.Invoke(RunInput.RoomOption(index));
                }) { text = "" };
                tile.style.flexDirection = FlexDirection.Column;
                tile.style.alignItems = Align.Center;
                tile.style.justifyContent = Justify.Center;
                tile.style.width = 210;
                tile.style.minHeight = 92;
                Ui.Margin(tile, 0);
                Ui.Padding(tile, 10, 12);
                tile.style.backgroundImage = Ui.Gradient(new Color(0.2f, 0.17f, 0.12f), new Color(0.09f, 0.07f, 0.06f));
                tile.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                Ui.Rounded(tile, 12);
                Ui.Border(tile, 2, new Color(0.98f, 0.79f, 0.32f, 0.55f));
                var label = Ui.Text(tile, option.Label, 15, Ui.Cream, Weight.Bold);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.pickingMode = PickingMode.Ignore;
                if (option.Cost != 0)
                {
                    var cost = Ui.Pill(tile, option.Cost > 0 ? $"{option.Cost}g" : $"+{-option.Cost}g", option.Cost > 0 ? Ui.Gold : Ui.Good, filled: true, size: 12);
                    cost.style.marginTop = 6;
                    cost.pickingMode = PickingMode.Ignore;
                }
                if (option.NeedsCard)
                {
                    var hint = Ui.Text(tile, "pick a card next", 11, Ui.Dim);
                    hint.style.marginTop = 4;
                    hint.pickingMode = PickingMode.Ignore;
                }
                tile.SetEnabled(legal);
                tile.style.opacity = legal ? 1 : 0.38f;
                Ui.Add(_content, tile);
                if (legal) Ui.Lift(tile);
                if (opened) Ui.Enter(tile, 24, 60 + 70 * i);
            }
        }

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
            _ => SuitIcon.Shape.Crown,
        };
    }
}
