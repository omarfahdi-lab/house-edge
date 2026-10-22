using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // playing cards drawn with UI Toolkit, no textures. each card is a shadow plus a face
    public static class CardView
    {
        public const float Width = 84;
        public const float Height = 118;

        private static readonly Color Face = new Color(0.99f, 0.98f, 0.94f);
        private static readonly Color FaceEdge = new Color(0.82f, 0.79f, 0.72f);
        private static readonly Color RedInk = new Color(0.8f, 0.1f, 0.14f);
        private static readonly Color BlackInk = new Color(0.1f, 0.1f, 0.13f);
        private static readonly Color Back = new Color(0.5f, 0.07f, 0.11f);
        private static readonly Color BackTrim = new Color(0.98f, 0.79f, 0.32f, 0.85f);
        private static readonly Color CurseFace = new Color(0.15f, 0.08f, 0.2f);
        private static readonly Color CurseInk = new Color(0.8f, 0.62f, 1f);

        public static Color EnhancementColor(Enhancement enhancement) => enhancement switch
        {
            Enhancement.Gilded => new Color(0.98f, 0.74f, 0.18f),
            Enhancement.Flex => new Color(0.3f, 0.78f, 0.95f),
            Enhancement.Fragile => new Color(0.62f, 0.85f, 1f),
            Enhancement.Marked => new Color(0.92f, 0.3f, 0.3f),
            Enhancement.Ghost => new Color(0.68f, 0.68f, 0.82f),
            Enhancement.Hot => new Color(1f, 0.48f, 0.12f),
            Enhancement.Heavy => new Color(0.42f, 0.42f, 0.48f),
            _ => new Color(0, 0, 0, 0.25f),
        };

        public static Color StampColor(Stamp stamp) => stamp switch
        {
            Stamp.Red => new Color(0.8f, 0.12f, 0.12f),
            Stamp.Blue => new Color(0.18f, 0.4f, 0.9f),
            Stamp.Gold => new Color(0.95f, 0.72f, 0.16f),
            _ => new Color(0.1f, 0.1f, 0.1f),
        };

        public static VisualElement Create(Card card, float scale = 1f)
        {
            var (root, face) = Shell(scale);
            if (card.IsCursed)
            {
                DrawCurse(face, card, scale);
                Ui.Tip(root, card.CurseName, CardEffects.Describe(card.Curse));
                return root;
            }

            face.style.backgroundColor = Face;
            bool enhanced = card.Enhancement != Enhancement.None;
            Ui.Border(face, enhanced ? 3.5f * scale : 1, enhanced ? EnhancementColor(card.Enhancement) : FaceEdge);
            if (card.Enhancement == Enhancement.Ghost) face.style.opacity = 0.72f;
            var ink = card.IsRed ? RedInk : BlackInk;
            var suit = SuitIcon.For(card);

            Corner(face, card.RankLabel, suit, ink, scale, flipped: false);
            if (!enhanced) Corner(face, card.RankLabel, suit, ink, scale, flipped: true);

            if (card.IsFace)
            {
                // a framed court card: the letter in the display face over its suit
                var frame = Ui.Column(face);
                frame.style.position = Position.Absolute;
                frame.style.left = 23 * scale;
                frame.style.right = 23 * scale;
                frame.style.top = 25 * scale;
                frame.style.bottom = 25 * scale;
                frame.style.alignItems = Align.Center;
                frame.style.justifyContent = Justify.Center;
                frame.style.backgroundColor = card.IsRed ? new Color(0.98f, 0.88f, 0.84f) : new Color(0.88f, 0.9f, 0.95f);
                Ui.Border(frame, 1.5f * scale, new Color(ink.r, ink.g, ink.b, 0.5f));
                Ui.Rounded(frame, 4 * scale);
                var letter = Ui.Text(frame, card.RankLabel, (int)(28 * scale), ink);
                letter.style.unityFontDefinition = Ui.DisplayFont;
                Ui.Add(frame, new SuitIcon(suit, ink, 16 * scale));
            }
            else
            {
                float size = (card.Rank == Rank.Ace ? 46 : 36) * scale;
                var center = new SuitIcon(suit, ink, size);
                center.style.position = Position.Absolute;
                center.style.left = (Width * scale - size) / 2;
                center.style.top = (Height * scale - size) / 2;
                face.Add(center);
            }

            if (enhanced)
            {
                var color = EnhancementColor(card.Enhancement);
                var tag = Ui.Text(face, card.Enhancement.ToString().ToUpperInvariant(), (int)(10 * scale), Color.white, Weight.Heavy);
                tag.style.position = Position.Absolute;
                tag.style.left = tag.style.right = 0;
                tag.style.bottom = 0;
                tag.style.height = 15 * scale;
                tag.style.letterSpacing = 1;
                tag.style.unityTextAlign = TextAnchor.MiddleCenter;
                tag.style.backgroundColor = color;
                tag.style.color = card.Enhancement == Enhancement.Fragile || card.Enhancement == Enhancement.Gilded ? BlackInk : Color.white;
            }

            if (card.Stamp != Stamp.None)
            {
                // a wax seal in the top right
                var seal = new VisualElement();
                seal.style.position = Position.Absolute;
                seal.style.right = 5 * scale;
                seal.style.top = 5 * scale;
                seal.style.width = seal.style.height = 16 * scale;
                seal.style.backgroundColor = StampColor(card.Stamp);
                Ui.Rounded(seal, 8 * scale);
                Ui.Border(seal, 2 * scale, Color.Lerp(StampColor(card.Stamp), Color.black, 0.35f));
                face.Add(seal);
            }

            Ui.Tip(root, Name(card), Explain(card));
            return root;
        }

        private static void Corner(VisualElement face, string rank, SuitIcon.Shape suit, Color ink, float scale, bool flipped)
        {
            var corner = Ui.Column(face);
            corner.style.position = Position.Absolute;
            corner.style.alignItems = Align.Center;
            corner.style.minWidth = 20 * scale;
            if (flipped)
            {
                corner.style.right = 4 * scale;
                corner.style.bottom = 4 * scale;
                corner.style.rotate = new Rotate(180);
            }
            else
            {
                corner.style.left = 4 * scale;
                corner.style.top = 4 * scale;
            }
            var label = Ui.Text(corner, rank, (int)(19 * scale), ink, Weight.Heavy);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.letterSpacing = rank.Length > 1 ? -1.5f * scale : 0;
            Ui.Add(corner, new SuitIcon(suit, ink, 12 * scale)).style.marginTop = 1;
        }

        private static void DrawCurse(VisualElement face, Card card, float scale)
        {
            face.style.backgroundColor = CurseFace;
            face.style.backgroundImage = Ui.Gradient(new Color(0.28f, 0.12f, 0.36f), CurseFace);
            face.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Ui.Border(face, 3 * scale, CurseInk);
            var column = Ui.Column(face, 2 * scale);
            column.style.flexGrow = 1;
            column.style.alignItems = Align.Center;
            column.style.justifyContent = Justify.Center;
            Ui.Add(column, new SuitIcon(SuitIcon.Shape.Skull, CurseInk, 32 * scale));
            var name = Ui.Text(column, card.CurseName.ToUpperInvariant(), (int)(11 * scale), CurseInk, Weight.Heavy);
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.whiteSpace = WhiteSpace.Normal;
            Ui.Text(column, $"counts {card.LowValue}", (int)(10 * scale), Ui.Dim);
        }

        // "Queen of Hearts". the UI font has no suit symbols
        public static string Name(Card card)
        {
            if (card.IsCursed) return card.CurseName;
            string rank = card.Rank switch
            {
                Rank.Ace => "Ace", Rank.King => "King", Rank.Queen => "Queen", Rank.Jack => "Jack",
                _ => ((int)card.Rank).ToString(),
            };
            return $"{rank} of {card.Suit}";
        }

        // plain words for what's special about a card
        public static string Explain(Card card)
        {
            string text = "";
            if (card.Enhancement != Enhancement.None) text += $"{card.Enhancement}: {CardEffects.Describe(card.Enhancement)}";
            if (card.Stamp != Stamp.None) text += (text.Length > 0 ? "\n" : "") + $"{card.Stamp} Stamp: {CardEffects.Describe(card.Stamp)}";
            return text.Length > 0 ? text : null;
        }

        public static VisualElement CreateFaceDown(float scale = 1f)
        {
            var (root, face) = Shell(scale);
            face.style.backgroundColor = Back;
            face.style.backgroundImage = Ui.Gradient(new Color(0.66f, 0.11f, 0.15f), new Color(0.38f, 0.04f, 0.08f));
            face.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Ui.Border(face, 4 * scale, Face);

            var pattern = new Lattice();
            Ui.Fill(pattern);
            pattern.style.left = pattern.style.right = pattern.style.top = pattern.style.bottom = 5 * scale;
            Ui.Border(pattern, 1.5f * scale, BackTrim);
            Ui.Rounded(pattern, 4 * scale);
            pattern.style.alignItems = Align.Center;
            pattern.style.justifyContent = Justify.Center;
            face.Add(pattern);

            var badge = new VisualElement();
            badge.style.width = 40 * scale;
            badge.style.height = 40 * scale;
            badge.style.backgroundColor = new Color(0.35f, 0.03f, 0.07f);
            Ui.Rounded(badge, 20 * scale);
            Ui.Border(badge, 1.5f * scale, BackTrim);
            badge.style.alignItems = Align.Center;
            badge.style.justifyContent = Justify.Center;
            pattern.Add(badge);
            var mark = Ui.Text(badge, "HE", (int)(17 * scale), BackTrim);
            mark.style.unityFontDefinition = Ui.DisplayFont;
            return root;
        }

        // a gold ring around a card, for picked cards
        public static void SetSelected(VisualElement card, bool selected)
        {
            var ring = card.Q("ring");
            if (ring == null && selected)
            {
                ring = new VisualElement { name = "ring", pickingMode = PickingMode.Ignore };
                Ui.Fill(ring);
                ring.style.left = ring.style.right = ring.style.top = ring.style.bottom = -4;
                Ui.Border(ring, 3, Ui.Gold);
                Ui.Rounded(ring, 11);
                card.Add(ring);
            }
            if (ring != null) Ui.Show(ring, selected);
            card.style.translate = new Translate(0, selected ? -10 : 0);
        }

        private static (VisualElement root, VisualElement face) Shell(float scale)
        {
            var root = new VisualElement();
            root.style.width = Width * scale;
            root.style.height = Height * scale;
            root.style.flexShrink = 0;
            root.pickingMode = PickingMode.Position;

            var shadow = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(shadow);
            shadow.style.left = 1 * scale;
            shadow.style.right = -3 * scale;
            shadow.style.top = 4 * scale;
            shadow.style.bottom = -5 * scale;
            shadow.style.backgroundColor = new Color(0, 0, 0, 0.32f);
            Ui.Rounded(shadow, 9 * scale);
            root.Add(shadow);

            var face = new VisualElement { name = "face", pickingMode = PickingMode.Ignore };
            Ui.Fill(face);
            Ui.Rounded(face, 7 * scale);
            face.style.overflow = Overflow.Hidden;
            root.Add(face);
            return (root, face);
        }

        // the diamond lattice on the card back
        private sealed class Lattice : VisualElement
        {
            public Lattice()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                float w = contentRect.width, h = contentRect.height;
                if (w <= 0) return;
                var p = context.painter2D;
                p.strokeColor = new Color(0.98f, 0.79f, 0.32f, 0.22f);
                p.lineWidth = 1;
                float step = w / 5;
                p.BeginPath();
                for (float x = -h; x < w + h; x += step)
                {
                    p.MoveTo(new Vector2(x, 0));
                    p.LineTo(new Vector2(x + h, h));
                    p.MoveTo(new Vector2(x + h, 0));
                    p.LineTo(new Vector2(x, h));
                }
                p.Stroke();
            }
        }
    }
}
