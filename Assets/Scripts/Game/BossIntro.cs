using System;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the boss walks in: portrait, name, a line, and what their ability does in plain words
    public sealed class BossIntro
    {
        private readonly VisualElement _root, _frame, _card, _tipBox, _portraitSlot;
        private readonly Sunburst _rays;
        private readonly Label _kind, _name, _title, _quote, _ability, _rule, _tip;
        private string _quoteText = "";
        private int _typed;

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;
        public event Action Closed;

        public BossIntro(VisualElement parent)
        {
            _root = new VisualElement();
            Ui.Fill(_root);
            _root.style.backgroundColor = new Color(0.01f, 0.01f, 0.02f, 0.9f);
            _root.style.flexDirection = FlexDirection.Row;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            parent.Add(_root);

            // the portrait in a gold frame, with light turning behind it
            var left = new VisualElement();
            left.style.width = 380;
            left.style.height = 420;
            left.style.alignItems = Align.Center;
            left.style.justifyContent = Justify.Center;
            _root.Add(left);
            _rays = new Sunburst(new Color(0.98f, 0.79f, 0.32f, 0.12f), 560);
            _rays.style.left = 190 - 280;
            _rays.style.top = 210 - 280;
            left.Add(_rays);
            _frame = new VisualElement();
            Ui.Border(_frame, 5, Ui.Gold);
            Ui.Rounded(_frame, 26);
            _frame.style.backgroundColor = Ui.Gold;
            left.Add(_frame);
            _portraitSlot = new VisualElement();
            _frame.Add(_portraitSlot);

            var right = Ui.Column(_root, 8);
            right.style.width = 560;
            right.style.marginLeft = 30;
            _kind = Ui.Pill(right, "", Ui.Bad, filled: true, size: 12);
            _kind.style.alignSelf = Align.FlexStart;
            _name = Ui.Title(right, "", 54, Ui.Gold);
            _title = Ui.Text(right, "", 18, Ui.Dim, Weight.Medium);
            _title.style.unityFontStyleAndWeight = FontStyle.Italic;
            _quote = Ui.Text(right, "", 21, Ui.Cream, Weight.Medium);
            _quote.style.whiteSpace = WhiteSpace.Normal;
            _quote.style.unityFontStyleAndWeight = FontStyle.Italic;
            _quote.style.minHeight = 56;

            _card = Ui.Glass(right, 6, radius: 14);
            _card.style.backgroundColor = new Color(0.45f, 0.05f, 0.08f, 0.55f);
            Ui.BorderColor(_card, new Color(1f, 0.45f, 0.4f, 0.7f));
            Ui.Padding(_card, 14, 18);
            var abilityHead = Ui.Row(_card, 8);
            Ui.Caption(abilityHead, "ABILITY", new Color(1f, 0.75f, 0.7f));
            _ability = Ui.Text(abilityHead, "", 22, Ui.Cream, Weight.Heavy);
            _rule = Ui.Body(_card, "", 18, Ui.Cream);

            _tipBox = Ui.Glass(right, 4, radius: 12);
            Ui.Padding(_tipBox, 10, 16);
            var tipHead = Ui.Row(_tipBox, 6);
            Ui.Add(tipHead, new SuitIcon(SuitIcon.Shape.Star, Ui.Gold, 14));
            Ui.Caption(tipHead, "HOW TO BEAT THEM", Ui.Gold);
            _tip = Ui.Body(_tipBox, "", 15, new Color(0.9f, 0.92f, 0.88f));

            var seat = Ui.Button(right, "Take a seat", Close, ButtonKind.Gold, "Space");
            seat.style.alignSelf = Align.FlexStart;
            seat.style.marginTop = 8;

            // type the quote out, and keep the light turning
            float spin = 0;
            _root.schedule.Execute(() =>
            {
                if (!IsOpen) return;
                spin += 0.016f;
                _rays.style.rotate = new Rotate(spin * 14);
                if (_typed < _quoteText.Length && spin > 0.45f)
                {
                    _typed = Mathf.Min(_quoteText.Length, _typed + 2);
                    _quote.text = "“" + _quoteText.Substring(0, _typed) + (_typed == _quoteText.Length ? "”" : "");
                }
            }).Every(28);
            Ui.Show(_root, false);
        }

        public void Show(BossDefinition boss, string kind)
        {
            _portraitSlot.Clear();
            _portraitSlot.Add(new BossPortrait(boss.Name, 280));
            _kind.text = kind;
            _name.text = boss.Name.ToUpperInvariant();
            _title.text = boss.Title;
            _quoteText = boss.Quote;
            _typed = 0;
            _quote.text = "";
            _ability.text = boss.Ability;
            _rule.text = Ui.Rich(boss.Twist);
            _tip.text = Ui.Rich(boss.Tip);

            Ui.Show(_root, true);
            Ui.Enter(_root, 0, 0, 0.25f);
            // the portrait swings in, the name stamps down, then the rules slide up
            Ui.Transition(_frame, 0, "translate", "rotate", "opacity");
            _frame.style.translate = new Translate(-260, 30);
            _frame.style.rotate = new Rotate(-14);
            _frame.style.opacity = 0;
            _frame.schedule.Execute(() =>
            {
                Ui.Transition(_frame, 0.55f, EasingMode.EaseOutBack, "translate", "rotate", "opacity");
                _frame.style.translate = new Translate(0, 0);
                _frame.style.rotate = new Rotate(-3);
                _frame.style.opacity = 1;
            }).StartingIn(60);
            Ui.Pop(_name, 1.7f, 320);
            Ui.Enter(_card, 26, 700, 0.35f);
            Ui.Enter(_tipBox, 26, 900, 0.35f);
        }

        public void Close()
        {
            if (!IsOpen) return;
            Ui.Show(_root, false);
            Closed?.Invoke();
        }
    }
}
