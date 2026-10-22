using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // suits and small icons drawn as vector shapes. the built-in font has no ♠♥♦♣ glyphs on WebGL,
    // where there's no system font to fall back on
    public sealed class SuitIcon : VisualElement
    {
        public enum Shape { Spade, Heart, Diamond, Club, Skull, Coin, Flame, Lock, Cards, Glass, Eye, Check, Star, Crown }

        private Shape _shape;
        private Color _color;

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

        public void Set(Shape shape, Color color)
        {
            _shape = shape;
            _color = color;
            MarkDirtyRepaint();
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
                case Shape.Coin:
                    Circle(p, At(0.5f, 0.5f), 0.46f * w);
                    p.strokeColor = new Color(0, 0, 0, 0.3f);
                    p.lineWidth = Mathf.Max(1, w * 0.08f);
                    p.BeginPath();
                    p.Arc(At(0.5f, 0.5f), 0.3f * w, 0, 360);
                    p.Stroke();
                    break;
                case Shape.Flame:
                    p.BeginPath();
                    p.MoveTo(At(0.5f, 0.02f));
                    p.BezierCurveTo(At(0.62f, 0.3f), At(0.88f, 0.45f), At(0.86f, 0.68f));
                    p.BezierCurveTo(At(0.84f, 0.9f), At(0.66f, 0.99f), At(0.5f, 0.99f));
                    p.BezierCurveTo(At(0.32f, 0.99f), At(0.14f, 0.88f), At(0.14f, 0.66f));
                    p.BezierCurveTo(At(0.14f, 0.48f), At(0.3f, 0.4f), At(0.36f, 0.24f));
                    p.BezierCurveTo(At(0.44f, 0.36f), At(0.46f, 0.2f), At(0.5f, 0.02f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case Shape.Lock:
                    p.strokeColor = _color;
                    p.lineWidth = w * 0.12f;
                    p.BeginPath();
                    p.Arc(At(0.5f, 0.38f), 0.2f * w, 180, 360);
                    p.Stroke();
                    p.BeginPath();
                    p.MoveTo(At(0.3f, 0.38f));
                    p.LineTo(At(0.3f, 0.48f));
                    p.MoveTo(At(0.7f, 0.38f));
                    p.LineTo(At(0.7f, 0.48f));
                    p.Stroke();
                    Rect(p, At(0.16f, 0.46f), At(0.84f, 0.96f));
                    break;
                case Shape.Cards:
                    p.fillColor = new Color(_color.r, _color.g, _color.b, _color.a * 0.5f);
                    Rect(p, At(0.12f, 0.06f), At(0.68f, 0.8f));
                    p.fillColor = _color;
                    Rect(p, At(0.32f, 0.2f), At(0.88f, 0.94f));
                    break;
                case Shape.Glass:
                    p.BeginPath();
                    p.MoveTo(At(0.08f, 0.1f));
                    p.LineTo(At(0.92f, 0.1f));
                    p.LineTo(At(0.56f, 0.52f));
                    p.LineTo(At(0.56f, 0.86f));
                    p.LineTo(At(0.76f, 0.94f));
                    p.LineTo(At(0.24f, 0.94f));
                    p.LineTo(At(0.44f, 0.86f));
                    p.LineTo(At(0.44f, 0.52f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case Shape.Eye:
                    p.BeginPath();
                    p.MoveTo(At(0.02f, 0.5f));
                    p.BezierCurveTo(At(0.3f, 0.1f), At(0.7f, 0.1f), At(0.98f, 0.5f));
                    p.BezierCurveTo(At(0.7f, 0.9f), At(0.3f, 0.9f), At(0.02f, 0.5f));
                    p.ClosePath();
                    p.Fill();
                    p.fillColor = new Color(0.05f, 0.05f, 0.06f);
                    Circle(p, At(0.5f, 0.5f), 0.17f * w);
                    break;
                case Shape.Check:
                    p.strokeColor = _color;
                    p.lineWidth = w * 0.16f;
                    p.lineCap = LineCap.Round;
                    p.lineJoin = LineJoin.Round;
                    p.BeginPath();
                    p.MoveTo(At(0.16f, 0.52f));
                    p.LineTo(At(0.4f, 0.76f));
                    p.LineTo(At(0.86f, 0.24f));
                    p.Stroke();
                    break;
                case Shape.Star:
                    p.BeginPath();
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = -Mathf.PI / 2 + i * Mathf.PI / 5;
                        float r = i % 2 == 0 ? 0.48f : 0.2f;
                        var point = At(0.5f + Mathf.Cos(angle) * r, 0.53f + Mathf.Sin(angle) * r);
                        if (i == 0) p.MoveTo(point);
                        else p.LineTo(point);
                    }
                    p.ClosePath();
                    p.Fill();
                    break;
                case Shape.Crown:
                    p.BeginPath();
                    p.MoveTo(At(0.08f, 0.82f));
                    p.LineTo(At(0.04f, 0.26f));
                    p.LineTo(At(0.3f, 0.5f));
                    p.LineTo(At(0.5f, 0.14f));
                    p.LineTo(At(0.7f, 0.5f));
                    p.LineTo(At(0.96f, 0.26f));
                    p.LineTo(At(0.92f, 0.82f));
                    p.ClosePath();
                    p.Fill();
                    break;
            }
        }

        private static void Rect(Painter2D p, Vector2 min, Vector2 max)
        {
            p.BeginPath();
            p.MoveTo(min);
            p.LineTo(new Vector2(max.x, min.y));
            p.LineTo(max);
            p.LineTo(new Vector2(min.x, max.y));
            p.ClosePath();
            p.Fill();
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
