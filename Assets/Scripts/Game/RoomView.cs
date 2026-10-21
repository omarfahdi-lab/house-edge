using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the floor map's fork (pick one of two rooms) and the room you walk into
    public sealed class RoomView
    {
        private static readonly Color Door = new Color(0.32f, 0.18f, 0.1f);

        private readonly VisualElement _root, _content;
        private readonly Label _title, _text, _result;
        private readonly Button _leave;
        private Run _run;

        public event Action<RunInput> InputChosen;
        // a room option that works on a card: (option index, label)
        public event Action<int, string> CardOptionClicked;

        public VisualElement Root => _root;

        public RoomView(VisualElement parent)
        {
            _root = Ui.Column(parent, 12);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            Ui.Padding(_root, 26, 32);
            _root.style.alignItems = Align.Center;
            _root.style.backgroundColor = new Color(0.06f, 0.05f, 0.08f, 0.97f);

            _title = Ui.Text(_root, "", 32, Ui.Gold, bold: true);
            _title.style.letterSpacing = 3;
            _text = Ui.Text(_root, "", 17, Ui.Cream);
            _text.style.whiteSpace = WhiteSpace.Normal;
            _text.style.unityTextAlign = TextAnchor.MiddleCenter;
            _text.style.maxWidth = 760;
            _content = Ui.Row(_root, 18);
            _content.style.marginTop = 10;
            _content.style.flexWrap = Wrap.Wrap;
            _content.style.justifyContent = Justify.Center;
            _content.style.alignItems = Align.FlexStart;
            _result = Ui.Text(_root, "", 18, Ui.Gold, bold: true);
            _result.style.whiteSpace = WhiteSpace.Normal;
            _result.style.unityTextAlign = TextAnchor.MiddleCenter;
            _leave = Ui.Button(_root, "Leave  <size=12><color=#FFFFFF88>Space</color></size>", () => InputChosen?.Invoke(RunInput.LeaveRoom()), accent: true);
            _leave.enableRichText = true;
        }

        public void Refresh(Run run)
        {
            _run = run;
            _content.Clear();
            if (run.Phase == RunPhase.RoomChoice) RefreshChoice();
            else RefreshRoom();
        }

        private void RefreshChoice()
        {
            var (floor, stage) = _run.NextStage;
            _title.text = "PICK A DOOR";
            _text.text = $"On the way to the {Hud.StageName(stage, floor)}.";
            _result.text = "";
            Ui.Show(_leave, false);
            for (int i = 0; i < _run.RoomChoices.Count; i++)
            {
                var kind = _run.RoomChoices[i];
                var input = RunInput.ChooseRoom(i);
                var door = Ui.Column(_content, 10);
                door.style.width = 260;
                door.style.minHeight = 230;
                door.style.backgroundColor = Door;
                Ui.Rounded(door, 14);
                Ui.Border(door, 3, Ui.Gold);
                Ui.Padding(door, 18, 18);
                door.style.alignItems = Align.Center;
                Ui.Text(door, RoomIcon(kind), 40, Ui.Gold);
                Ui.Text(door, Rooms.Name(kind), 22, Ui.Cream, bold: true);
                var text = Ui.Text(door, Rooms.Describe(kind), 14, Ui.Dim);
                text.style.whiteSpace = WhiteSpace.Normal;
                text.style.unityTextAlign = TextAnchor.MiddleCenter;
                text.style.flexGrow = 1;
                var enter = Ui.Button(door, $"Enter  <size=12><color=#FFFFFF88>{i + 1}</color></size>", () => InputChosen?.Invoke(input), accent: true);
                enter.enableRichText = true;
                enter.style.alignSelf = Align.Stretch;
            }
        }

        private void RefreshRoom()
        {
            var room = _run.Room;
            _title.text = room.Name.ToUpperInvariant();
            _text.text = room.Description;
            _result.text = room.Result ?? "";
            Ui.Show(_leave, true);
            Ui.SetEnabled(_leave, _run.IsLegal(RunInput.LeaveRoom()));

            var options = _run.RoomOptions();
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                int index = i;
                string cost = option.Cost > 0 ? $"  {option.Cost}g" : option.Cost < 0 ? $"  +{-option.Cost}g" : "";
                var button = Ui.Button(_content, option.Label + cost, () =>
                {
                    if (option.NeedsCard) CardOptionClicked?.Invoke(index, option.Label);
                    else InputChosen?.Invoke(RunInput.RoomOption(index));
                });
                button.style.minWidth = 200;
                button.style.whiteSpace = WhiteSpace.Normal;
                bool legal = option.NeedsCard ? CanPickAnyCard(index) : _run.IsLegal(RunInput.RoomOption(index));
                Ui.SetEnabled(button, legal);
            }
        }

        private bool CanPickAnyCard(int option)
        {
            for (int c = 0; c < _run.Gambler.Deck.Count; c++)
                if (_run.IsLegal(RunInput.RoomOption(option, c))) return true;
            return false;
        }

        private static string RoomIcon(RoomKind kind) => kind switch
        {
            RoomKind.Bar => "♥",
            RoomKind.BackRoom => "♠",
            RoomKind.PawnShop => "$",
            RoomKind.LoanShark => "%",
            RoomKind.CardRoom => "♦",
            RoomKind.LostAndFound => "?",
            RoomKind.SecurityOffice => "!",
            _ => "♣",
        };
    }
}
