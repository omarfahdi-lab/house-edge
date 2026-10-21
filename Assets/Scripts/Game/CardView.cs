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
        private static readonly Color CurseFace = new Color(0.16f, 0.1f, 0.2f);
        private static readonly Color CurseInk = new Color(0.78f, 0.6f, 1f);

        public static Color EnhancementColor(Enhancement enhancement) => enhancement switch
        {
            Enhancement.Gilded => new Color(0.98f, 0.76f, 0.2f),
            Enhancement.Flex => new Color(0.35f, 0.8f, 0.95f),
            Enhancement.Fragile => new Color(0.75f, 0.9f, 1f),
            Enhancement.Marked => new Color(0.95f, 0.35f, 0.35f),
            Enhancement.Ghost => new Color(0.75f, 0.75f, 0.85f),
            Enhancement.Hot => new Color(1f, 0.5f, 0.15f),
            Enhancement.Heavy => new Color(0.45f, 0.45f, 0.5f),
            _ => new Color(0, 0, 0, 0.25f),
        };

        public static Color StampColor(Stamp stamp) => stamp switch
        {
            Stamp.Red => new Color(0.86f, 0.15f, 0.15f),
            Stamp.Blue => new Color(0.2f, 0.45f, 0.95f),
            Stamp.Gold => new Color(0.98f, 0.76f, 0.2f),
            _ => new Color(0.08f, 0.08f, 0.08f),
        };

        public static VisualElement Create(Card card, float scale = 1f)
        {
            if (card.IsCursed) return CreateCurse(card, scale);

            var root = Shell(scale);
            root.style.backgroundColor = Face;
            bool enhanced = card.Enhancement != Enhancement.None;
            Ui.Border(root, enhanced ? 4 * scale : 1, EnhancementColor(card.Enhancement));
            if (card.Enhancement == Enhancement.Ghost) root.style.opacity = 0.75f;
            var ink = card.IsRed ? RedInk : BlackInk;

            var corner = Ui.Column(root);
            corner.style.position = Position.Absolute;
            corner.style.left = 6 * scale;
            corner.style.top = 3 * scale;
            corner.style.alignItems = Align.Center;
            Ui.Text(corner, card.RankLabel, (int)(20 * scale), ink, bold: true);
            Ui.Text(corner, card.SuitSymbol, (int)(16 * scale), ink);

            var center = Ui.Text(root, card.SuitSymbol, (int)(44 * scale), ink);
            center.style.position = Position.Absolute;
            center.style.left = center.style.right = 0;
            center.style.top = 30 * scale;
            center.style.unityTextAlign = TextAnchor.MiddleCenter;

            if (enhanced)
            {
                var tag = Ui.Text(root, card.Enhancement.ToString().ToUpperInvariant(), (int)(10 * scale), BlackInk, bold: true);
                tag.style.position = Position.Absolute;
                tag.style.left = tag.style.right = 0;
                tag.style.bottom = 3 * scale;
                tag.style.unityTextAlign = TextAnchor.MiddleCenter;
                tag.style.backgroundColor = EnhancementColor(card.Enhancement);
            }
            else
            {
                var bottom = Ui.Text(root, card.RankLabel, (int)(20 * scale), ink, bold: true);
                bottom.style.position = Position.Absolute;
                bottom.style.right = 6 * scale;
                bottom.style.bottom = 3 * scale;
                bottom.style.rotate = new Rotate(180);
            }

            if (card.Stamp != Stamp.None)
            {
                var stamp = new VisualElement();
                stamp.style.position = Position.Absolute;
                stamp.style.right = 6 * scale;
                stamp.style.top = 6 * scale;
                stamp.style.width = stamp.style.height = 14 * scale;
                stamp.style.backgroundColor = StampColor(card.Stamp);
                Ui.Rounded(stamp, 7 * scale);
                Ui.Border(stamp, 1, Face);
                root.Add(stamp);
            }
            root.tooltip = Tooltip(card);
            return root;
        }

        private static VisualElement CreateCurse(Card card, float scale)
        {
            var root = Shell(scale);
            root.style.backgroundColor = CurseFace;
            Ui.Border(root, 3 * scale, CurseInk);
            var column = Ui.Column(root);
            column.style.flexGrow = 1;
            column.style.alignItems = Align.Center;
            column.style.justifyContent = Justify.Center;
            Ui.Text(column, "☠", (int)(34 * scale), CurseInk);
            var name = Ui.Text(column, card.CurseName.ToUpperInvariant(), (int)(11 * scale), CurseInk, bold: true);
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.whiteSpace = WhiteSpace.Normal;
            Ui.Text(column, $"counts {card.LowValue}", (int)(10 * scale), Ui.Dim);
            root.tooltip = Tooltip(card);
            return root;
        }

        public static string Tooltip(Card card)
        {
            if (card.IsCursed) return $"{card.CurseName}: {CardEffects.Describe(card.Curse)}";
            string text = card.ToString();
            if (card.Enhancement != Enhancement.None) text += $"\n{card.Enhancement}: {CardEffects.Describe(card.Enhancement)}";
            if (card.Stamp != Stamp.None) text += $"\n{card.Stamp} Stamp: {CardEffects.Describe(card.Stamp)}";
            return text;
        }

        public static VisualElement CreateFaceDown(float scale = 1f)
        {
            var root = Shell(scale);
            root.style.backgroundColor = Back;
            Ui.Border(root, 4 * scale, Face);

            var inner = new VisualElement();
            Ui.Fill(inner);
            inner.style.left = inner.style.right = inner.style.top = inner.style.bottom = 6 * scale;
            Ui.Border(inner, 2, BackTrim);
            Ui.Rounded(inner, 5);
            root.Add(inner);

            var mark = Ui.Text(inner, "HE", (int)(22 * scale), BackTrim, bold: true);
            mark.style.flexGrow = 1;
            mark.style.unityTextAlign = TextAnchor.MiddleCenter;
            return root;
        }

        private static VisualElement Shell(float scale)
        {
            var root = new VisualElement();
            root.style.width = Width * scale;
            root.style.height = Height * scale;
            root.style.flexShrink = 0;
            Ui.Rounded(root, 8 * scale);
            root.pickingMode = PickingMode.Position;
            return root;
        }
    }
}
