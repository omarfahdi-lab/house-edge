using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // opening a card pack: three cards come out face down, flip one by one, and you keep one
    public sealed class PackView
    {
        private readonly VisualElement _root, _cards;
        private readonly Label _title;
        private Run _shownFor;

        public event Action<RunInput> InputChosen;
        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public PackView(VisualElement parent)
        {
            _root = Ui.Column(parent, 14);
            Ui.Fill(_root);
            _root.style.left = Hud.Width;
            _root.style.backgroundColor = new Color(0.02f, 0.02f, 0.04f, 0.9f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _title = Ui.Title(_root, "", 40, Ui.Gold);
            Ui.Text(_root, "Keep one card for your deck. You and the dealer will both draw it from now on.", 15, Ui.Cream);
            _cards = Ui.Row(_root, 30);
            _cards.style.marginTop = 10;
            _cards.style.alignItems = Align.FlexStart;
            var skip = Ui.Button(_root, "Skip  ·  keep none", () => InputChosen?.Invoke(RunInput.SkipPack()), ButtonKind.Ghost);
            skip.style.marginTop = 8;
            Ui.Show(_root, false);
        }

        public void Refresh(Run run, string packName)
        {
            bool open = run.Phase == RunPhase.OpeningPack;
            if (!open)
            {
                _shownFor = null;
                Ui.Show(_root, false);
                return;
            }
            if (_shownFor == run && IsOpen) return;
            _shownFor = run;
            _title.text = packName.ToUpperInvariant();
            _cards.Clear();
            for (int i = 0; i < run.PackCards.Count; i++)
            {
                int index = i;
                var card = run.PackCards[i];
                var column = Ui.Column(_cards, 10);
                column.style.alignItems = Align.Center;
                column.style.width = 150;

                // the card starts as its back and flips over a beat after the one before
                var slot = new VisualElement();
                slot.style.width = CardView.Width * 1.5f;
                slot.style.height = CardView.Height * 1.5f;
                column.Add(slot);
                var back = CardView.CreateFaceDown(1.5f);
                back.style.position = Position.Absolute;
                slot.Add(back);
                var face = CardView.Create(card, 1.5f);
                face.style.position = Position.Absolute;
                face.style.scale = new Scale(new Vector2(0.02f, 1));
                slot.Add(face);
                long at = 450 + 380 * i;
                back.schedule.Execute(() =>
                {
                    Ui.Transition(back, 0.14f, "scale");
                    back.style.scale = new Scale(new Vector2(0.02f, 1));
                }).StartingIn(at);
                face.schedule.Execute(() =>
                {
                    Ui.Transition(face, 0.22f, EasingMode.EaseOutBack, "scale");
                    face.style.scale = new Scale(Vector2.one);
                }).StartingIn(at + 140);

                var name = Ui.Text(column, CardView.Name(card), 15, Ui.Cream, Weight.Bold);
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                string extra = CardView.Explain(card);
                var text = Ui.Body(column, extra ?? "A plain card.", 12, Ui.Dim, center: true);
                text.style.minHeight = 34;
                var keep = Ui.Button(column, "Keep", () => InputChosen?.Invoke(RunInput.PickPackCard(index)), ButtonKind.Gold);
                keep.style.width = 130;
                Ui.Lift(slot, 1.05f);
                slot.RegisterCallback<ClickEvent>(_ => InputChosen?.Invoke(RunInput.PickPackCard(index)));
                Ui.Enter(column, 40, 80 * i, 0.35f);
            }
            Ui.Show(_root, true);
            Ui.Enter(_root, 0, 0, 0.2f);
        }
    }
}
