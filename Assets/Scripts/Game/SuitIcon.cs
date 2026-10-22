using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // suits drawn as vector shapes. the built-in font has no ♠♥♦♣ glyphs on WebGL,
    // where there's no system font to fall back on
    public sealed class SuitIcon : VisualElement
    {
        public enum Shape { Spade, Heart, Diamond, Club, Skull }

        private readonly Shape _shape;
        private readonly Color _color;

        public SuitIcon(Shape shape, Color color, float size)
        {
            _shape = shape;
            _color = color;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public static Shape For(Card card) => card.IsCursed ? Shape.Skull : card.Suit switch
        {
            Suit.Spades => Shape.Spade,
            Suit.Hearts => Shape.Heart,
            Suit.Diamonds => Shape.Diamond,
            _ => Shape.Club,
        };

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width;
            float h = contentRect.height;
            if (w <= 0 || h <= 0) return;
            var p = context.painter2D;
            p.fillColor = _color;
            Vector2 At(float x, float y) => new Vector2(x * w, y * h);

            switch (_shape)
            {
                case Shape.Diamond:
                    p.BeginPath();
                    p.MoveTo(At(0.5f, 0.02f));
                    p.LineTo(At(0.9f, 0.5f));
                    p.LineTo(At(0.5f, 0.98f));
                    p.LineTo(At(0.1f, 0.5f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case Shape.Heart:
                    Heart(p, At, flip: false);
                    break;
                case Shape.Spade:
                    Heart(p, At, flip: true);
                    Stem(p, At);
                    break;
                case Shape.Club:
                    Circle(p, At(0.5f, 0.3f), 0.22f * w);
                    Circle(p, At(0.27f, 0.58f), 0.22f * w);
                    Circle(p, At(0.73f, 0.58f), 0.22f * w);
                    Circle(p, At(0.5f, 0.52f), 0.14f * w);
                    Stem(p, At);
                    break;
                case Shape.Skull:
                    Circle(p, At(0.5f, 0.42f), 0.36f * w);
                    p.BeginPath();
                    p.MoveTo(At(0.3f, 0.62f));
                    p.LineTo(At(0.7f, 0.62f));
                    p.LineTo(At(0.66f, 0.92f));
                    p.LineTo(At(0.34f, 0.92f));
                    p.ClosePath();
                    p.Fill();
                    p.fillColor = new Color(0.08f, 0.05f, 0.1f);
                    Circle(p, At(0.36f, 0.45f), 0.1f * w);
                    Circle(p, At(0.64f, 0.45f), 0.1f * w);
                    break;
            }
        }

        // two lobes and a point. flipped, it's the top of a spade
        private static void Heart(Painter2D p, System.Func<float, float, Vector2> at, bool flip)
        {
            float Y(float y) => flip ? 1.02f - y - 0.22f : y;
            p.BeginPath();
            p.MoveTo(at(0.5f, Y(0.3f)));
            p.BezierCurveTo(at(0.5f, Y(0.05f)), at(0.02f, Y(0.02f)), at(0.05f, Y(0.38f)));
            p.BezierCurveTo(at(0.08f, Y(0.62f)), at(0.4f, Y(0.75f)), at(0.5f, Y(0.95f)));
            p.BezierCurveTo(at(0.6f, Y(0.75f)), at(0.92f, Y(0.62f)), at(0.95f, Y(0.38f)));
            p.BezierCurveTo(at(0.98f, Y(0.02f)), at(0.5f, Y(0.05f)), at(0.5f, Y(0.3f)));
            p.ClosePath();
            p.Fill();
        }

        private static void Stem(Painter2D p, System.Func<float, float, Vector2> at)
        {
            p.BeginPath();
            p.MoveTo(at(0.5f, 0.55f));
            p.LineTo(at(0.66f, 0.98f));
            p.LineTo(at(0.34f, 0.98f));
            p.ClosePath();
            p.Fill();
        }

        private static void Circle(Painter2D p, Vector2 center, float radius)
        {
            p.BeginPath();
            p.Arc(center, radius, 0, 360);
            p.ClosePath();
            p.Fill();
        }
    }
}
