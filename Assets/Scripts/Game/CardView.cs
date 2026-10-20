using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // placeholder playing cards drawn with UI Toolkit, no textures
    public static class CardView
    {
        public const float Width = 84;
        public const float Height = 118;

        private static readonly Color Face = new Color(0.99f, 0.98f, 0.95f);
        private static readonly Color RedInk = new Color(0.8f, 0.12f, 0.14f);
        private static readonly Color BlackInk = new Color(0.1f, 0.1f, 0.12f);
        private static readonly Color Back = new Color(0.55f, 0.09f, 0.12f);
        private static readonly Color BackTrim = new Color(0.98f, 0.79f, 0.32f, 0.8f);

        public static VisualElement Create(Card card)
        {
            var root = Shell();
            root.style.backgroundColor = Face;
            Ui.Border(root, 1, new Color(0, 0, 0, 0.25f));
            var ink = card.IsRed ? RedInk : BlackInk;

            var corner = Ui.Column(root);
            corner.style.position = Position.Absolute;
            corner.style.left = 7;
            corner.style.top = 4;
            corner.style.alignItems = Align.Center;
            Ui.Text(corner, card.RankLabel, 20, ink, bold: true);
            Ui.Text(corner, card.SuitSymbol, 16, ink);

            var center = Ui.Text(root, card.SuitSymbol, 44, ink);
            center.style.position = Position.Absolute;
            center.style.left = center.style.right = 0;
            center.style.top = 34;
            center.style.unityTextAlign = TextAnchor.MiddleCenter;

            var bottom = Ui.Text(root, card.RankLabel, 20, ink, bold: true);
            bottom.style.position = Position.Absolute;
            bottom.style.right = 7;
            bottom.style.bottom = 4;
            bottom.style.rotate = new Rotate(180);
            return root;
        }

        public static VisualElement CreateFaceDown()
        {
            var root = Shell();
            root.style.backgroundColor = Back;
            Ui.Border(root, 4, Face);

            var inner = new VisualElement();
            Ui.Fill(inner);
            inner.style.left = inner.style.right = inner.style.top = inner.style.bottom = 6;
            Ui.Border(inner, 2, BackTrim);
            Ui.Rounded(inner, 5);
            root.Add(inner);

            var mark = Ui.Text(inner, "HE", 22, BackTrim, bold: true);
            mark.style.flexGrow = 1;
            mark.style.unityTextAlign = TextAnchor.MiddleCenter;
            return root;
        }

        private static VisualElement Shell()
        {
            var root = new VisualElement();
            root.style.width = Width;
            root.style.height = Height;
            root.style.flexShrink = 0;
            Ui.Rounded(root, 8);
            root.pickingMode = PickingMode.Ignore;
            return root;
        }
    }
}
