using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the King, Queen or Jack on a court card, drawn twice and mirrored like a real deck
    public sealed class CourtArt : VisualElement
    {
        private static readonly Color Skin = new Color(0.98f, 0.86f, 0.74f);
        private static readonly Color Gold = new Color(0.93f, 0.7f, 0.2f);
        private static readonly Color Line = new Color(0.12f, 0.09f, 0.07f);

        private readonly Rank _rank;
        private readonly Color _ink;
        private float _w, _h;
        private bool _flip;

        public CourtArt(Rank rank, Color ink)
        {
            _rank = rank;
            _ink = ink;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            _w = contentRect.width;
            _h = contentRect.height;
            if (_w <= 0) return;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            _flip = false;
            Half(p);
            _flip = true;
            Half(p);

            // the line between the two halves
            p.strokeColor = new Color(_ink.r, _ink.g, _ink.b, 0.5f);
            p.lineWidth = Mathf.Max(1, _w * 0.03f);
            p.BeginPath();
            p.MoveTo(new Vector2(0, _h * 0.54f));
            p.LineTo(new Vector2(_w, _h * 0.46f));
            p.Stroke();
        }

        // one royal from the chest up, in a box whose bottom edge is the middle of the card
        private void Half(Painter2D p)
        {
            var hair = _rank == Rank.Queen ? new Color(0.78f, 0.52f, 0.2f) : _rank == Rank.King ? new Color(0.88f, 0.86f, 0.82f) : new Color(0.38f, 0.22f, 0.12f);

            // the robe, with a gold collar
            p.fillColor = _ink;
            p.BeginPath();
            p.MoveTo(P(0.02f, 1));
            p.LineTo(P(0.12f, 0.8f));
            p.QuadraticCurveTo(P(0.5f, 0.56f), P(0.88f, 0.8f));
            p.LineTo(P(0.98f, 1));
            p.ClosePath();
            p.Fill();
            p.strokeColor = Gold;
            p.lineWidth = _w * 0.05f;
            p.BeginPath();
            p.MoveTo(P(0.3f, 0.7f));
            p.LineTo(P(0.5f, 0.94f));
            p.LineTo(P(0.7f, 0.7f));
            p.Stroke();
            Dot(p, 0.5f, 0.94f, 0.045f, Gold);

            // hair behind the head: long for the Queen
            if (_rank == Rank.Queen)
            {
                p.fillColor = hair;
                p.BeginPath();
                p.MoveTo(P(0.28f, 0.74f));
                p.QuadraticCurveTo(P(0.2f, 0.3f), P(0.5f, 0.26f));
                p.QuadraticCurveTo(P(0.8f, 0.3f), P(0.72f, 0.74f));
                p.ClosePath();
                p.Fill();
            }
            else if (_rank == Rank.Jack) Dot(p, 0.5f, 0.42f, 0.2f, hair);

            Dot(p, 0.5f, 0.46f, 0.16f, Skin);
            Dot(p, 0.44f, 0.44f, 0.025f, Line);
            Dot(p, 0.56f, 0.44f, 0.025f, Line);
            if (_rank == Rank.King)
            {
                // a full beard under the eyes
                p.fillColor = hair;
                p.BeginPath();
                p.MoveTo(P(0.34f, 0.47f));
                p.QuadraticCurveTo(P(0.5f, 0.84f), P(0.66f, 0.47f));
                p.QuadraticCurveTo(P(0.5f, 0.56f), P(0.34f, 0.47f));
                p.Fill();
            }
            else
            {
                p.strokeColor = new Color(0.75f, 0.25f, 0.25f);
                p.lineWidth = _w * 0.025f;
                p.BeginPath();
                p.MoveTo(P(0.46f, 0.53f));
                p.QuadraticCurveTo(P(0.5f, 0.56f), P(0.54f, 0.53f));
                p.Stroke();
            }

            switch (_rank)
            {
                case Rank.King:
                    // a crown with a jewel, and a sword at his side
                    p.fillColor = Gold;
                    p.BeginPath();
                    p.MoveTo(P(0.32f, 0.32f));
                    p.LineTo(P(0.3f, 0.1f));
                    p.LineTo(P(0.41f, 0.21f));
                    p.LineTo(P(0.5f, 0.05f));
                    p.LineTo(P(0.59f, 0.21f));
                    p.LineTo(P(0.7f, 0.1f));
                    p.LineTo(P(0.68f, 0.32f));
                    p.ClosePath();
                    p.Fill();
                    Dot(p, 0.5f, 0.26f, 0.035f, _ink);
                    p.strokeColor = new Color(0.75f, 0.77f, 0.8f);
                    p.lineWidth = _w * 0.04f;
                    p.BeginPath();
                    p.MoveTo(P(0.87f, 0.98f));
                    p.LineTo(P(0.87f, 0.4f));
                    p.Stroke();
                    p.strokeColor = Gold;
                    p.BeginPath();
                    p.MoveTo(P(0.79f, 0.84f));
                    p.LineTo(P(0.95f, 0.84f));
                    p.Stroke();
                    break;
                case Rank.Queen:
                    // a tiara, and a flower she's holding
                    p.fillColor = Gold;
                    p.BeginPath();
                    p.MoveTo(P(0.34f, 0.32f));
                    p.QuadraticCurveTo(P(0.5f, 0.12f), P(0.66f, 0.32f));
                    p.QuadraticCurveTo(P(0.5f, 0.24f), P(0.34f, 0.32f));
                    p.Fill();
                    Dot(p, 0.4f, 0.22f, 0.03f, Gold);
                    Dot(p, 0.5f, 0.16f, 0.04f, Gold);
                    Dot(p, 0.6f, 0.22f, 0.03f, Gold);
                    p.strokeColor = new Color(0.25f, 0.55f, 0.25f);
                    p.lineWidth = _w * 0.03f;
                    p.BeginPath();
                    p.MoveTo(P(0.16f, 0.98f));
                    p.LineTo(P(0.16f, 0.74f));
                    p.Stroke();
                    Dot(p, 0.16f, 0.7f, 0.07f, Gold);
                    Dot(p, 0.16f, 0.7f, 0.03f, _ink);
                    break;
                default:
                    // a cap with a long feather
                    p.fillColor = Color.Lerp(_ink, Color.black, 0.25f);
                    p.BeginPath();
                    p.MoveTo(P(0.3f, 0.34f));
                    p.QuadraticCurveTo(P(0.34f, 0.12f), P(0.62f, 0.18f));
                    p.LineTo(P(0.74f, 0.34f));
                    p.ClosePath();
                    p.Fill();
                    p.strokeColor = Gold;
                    p.lineWidth = _w * 0.035f;
                    p.BeginPath();
                    p.MoveTo(P(0.3f, 0.33f));
                    p.LineTo(P(0.74f, 0.33f));
                    p.Stroke();
                    p.BeginPath();
                    p.MoveTo(P(0.6f, 0.2f));
                    p.QuadraticCurveTo(P(0.8f, 0.18f), P(0.94f, 0.02f));
                    p.Stroke();
                    break;
            }
        }

        // x across the card and y down to its middle, turned upside down for the lower royal
        private Vector2 P(float x, float y)
        {
            var point = new Vector2(x * _w, y * _h * 0.5f);
            return _flip ? new Vector2(_w - point.x, _h - point.y) : point;
        }

        private void Dot(Painter2D p, float x, float y, float radius, Color color)
        {
            p.fillColor = color;
            p.BeginPath();
            p.Arc(P(x, y), radius * _w, 0, 360);
            p.Fill();
        }
    }
}
