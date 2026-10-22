using System;
using System.Collections.Generic;
using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // a casino chip drawn with Painter2D, used for the stakes
    public sealed class PokerChip : VisualElement
    {
        private readonly Color _color, _insert;

        public PokerChip(Color color, Color insert, float size)
        {
            _color = color;
            _insert = insert;
            style.width = style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public static Color ColorOf(Stake stake) => stake switch
        {
            Stake.Red => new Color(0.8f, 0.14f, 0.16f),
            Stake.Green => new Color(0.13f, 0.58f, 0.3f),
            Stake.Black => new Color(0.13f, 0.13f, 0.15f),
            Stake.Purple => new Color(0.5f, 0.24f, 0.76f),
            Stake.Gold => new Color(0.95f, 0.72f, 0.2f),
            _ => new Color(0.93f, 0.92f, 0.88f),
        };

        public static Color InsertOf(Stake stake) => stake == Stake.White ? new Color(0.2f, 0.42f, 0.85f) : new Color(0.97f, 0.96f, 0.92f);

        private void Draw(MeshGenerationContext context)
        {
            float size = contentRect.width;
            if (size <= 0) return;
            var p = context.painter2D;
            var center = new Vector2(size / 2, size / 2);
            float r = size / 2;

            p.fillColor = new Color(0, 0, 0, 0.35f);
            Disc(p, center + new Vector2(0, size * 0.04f), r);
            p.fillColor = _color;
            Disc(p, center, r * 0.98f);

            // the edge stripes
            p.fillColor = _insert;
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * 45 - 9, a1 = i * 45 + 9;
                p.BeginPath();
                p.Arc(center, r * 0.98f, a0, a1);
                p.Arc(center, r * 0.72f, a1, a0, ArcDirection.CounterClockwise);
                p.ClosePath();
                p.Fill();
            }

            p.strokeColor = new Color(_insert.r, _insert.g, _insert.b, 0.8f);
            p.lineWidth = Mathf.Max(1, size * 0.03f);
            p.BeginPath();
            p.Arc(center, r * 0.62f, 0, 360);
            p.Stroke();
            p.fillColor = Color.Lerp(_color, Color.white, 0.12f);
            Disc(p, center, r * 0.56f);
        }

        private static void Disc(Painter2D p, Vector2 center, float radius)
        {
            p.BeginPath();
            p.Arc(center, radius, 0, 360);
            p.ClosePath();
            p.Fill();
        }
    }

    // gold lines printed on the felt, like a real blackjack table: a curved band around the printed rules
    public sealed class TableMarkings : VisualElement
    {
        private float _bandY = -1;

        public TableMarkings()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        // the band curves through this height (local to the table)
        public void SetBand(float y)
        {
            if (Mathf.Abs(y - _bandY) < 0.5f) return;
            _bandY = y;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w <= 0 || _bandY < 0) return;
            var p = context.painter2D;
            p.lineWidth = 2;
            float radius = w * 1.1f;
            var center = new Vector2(w / 2, _bandY - 30 - radius);
            foreach (var (r, alpha) in new[] { (radius, 0.26f), (radius + 60, 0.16f) })
            {
                float half = Mathf.Asin(Mathf.Min(0.98f, w * 0.43f / r)) * Mathf.Rad2Deg;
                p.strokeColor = new Color(0.98f, 0.79f, 0.32f, alpha);
                p.BeginPath();
                p.Arc(center, r, 90 - half, 90 + half);
                p.Stroke();
            }
        }
    }

    // an on/off switch with a sliding knob. there's no UI theme, so Toggle would be unstyled
    public sealed class Switch : VisualElement
    {
        private readonly VisualElement _knob;
        private readonly Label _state;

        public Switch(Action clicked)
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            var track = new VisualElement();
            track.style.width = 52;
            track.style.height = 28;
            Ui.Rounded(track, 14);
            Ui.Border(track, 1, new Color(1, 1, 1, 0.25f));
            Ui.Transition(track, 0.2f, "background-color");
            Add(track);
            _knob = new VisualElement();
            _knob.style.position = Position.Absolute;
            _knob.style.top = 3;
            _knob.style.left = 3;
            _knob.style.width = _knob.style.height = 20;
            _knob.style.backgroundColor = Ui.Cream;
            Ui.Rounded(_knob, 10);
            Ui.Transition(_knob, 0.22f, EasingMode.EaseOutBack, "translate");
            track.Add(_knob);
            _state = Ui.Text(this, "", 13, Ui.Dim, Weight.Heavy);
            _state.style.marginLeft = 10;
            _state.style.width = 30;
            RegisterCallback<ClickEvent>(_ => clicked());
        }

        public void Set(bool on)
        {
            var track = _knob.parent;
            track.style.backgroundColor = on ? Ui.Gold : new Color(0, 0, 0, 0.45f);
            _knob.style.translate = new Translate(on ? 24 : 0, 0);
            _knob.style.backgroundColor = on ? Ui.Ink : Ui.Cream;
            _state.text = on ? "ON" : "OFF";
            _state.style.color = on ? Ui.Gold : Ui.Dim;
        }
    }

    // big faint playing cards drifting behind the menu
    public sealed class Backdrop : VisualElement
    {
        private sealed class Floater
        {
            public VisualElement View;
            public Vector2 Position, Velocity;
            public float Angle, Spin;
        }

        private readonly List<Floater> _floaters = new List<Floater>();

        public Backdrop()
        {
            Ui.Fill(this);
            pickingMode = PickingMode.Ignore;
            style.overflow = Overflow.Hidden;
            var random = new System.Random(3);
            var ranks = new[] { Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten, Rank.Seven };
            for (int i = 0; i < 12; i++)
            {
                var card = i % 3 == 0
                    ? CardView.CreateFaceDown(1.5f)
                    : CardView.Create(new Card(ranks[random.Next(ranks.Length)], (Suit)random.Next(4)), 1.5f);
                card.style.position = Position.Absolute;
                card.style.opacity = 0.09f + (float)random.NextDouble() * 0.08f;
                card.pickingMode = PickingMode.Ignore;
                Add(card);
                _floaters.Add(new Floater
                {
                    View = card,
                    Position = new Vector2(random.Next(-60, 1280), random.Next(-100, 720)),
                    Velocity = new Vector2((float)random.NextDouble() * 8 - 4, -6 - (float)random.NextDouble() * 10),
                    Angle = random.Next(-30, 30),
                    Spin = (float)random.NextDouble() * 6 - 3,
                });
            }
            schedule.Execute(Tick).Every(16);
        }

        private void Tick()
        {
            if (resolvedStyle.display == DisplayStyle.None || parent == null || parent.resolvedStyle.display == DisplayStyle.None) return;
            const float dt = 0.016f;
            foreach (var f in _floaters)
            {
                f.Position += f.Velocity * dt;
                f.Angle += f.Spin * dt;
                if (f.Position.y < -260) f.Position = new Vector2(f.Position.x, 760);
                if (f.Position.x < -200) f.Position.x = 1300;
                if (f.Position.x > 1320) f.Position.x = -180;
                f.View.style.left = f.Position.x;
                f.View.style.top = f.Position.y;
                f.View.style.rotate = new Rotate(f.Angle);
            }
        }
    }

    // coins and confetti flying out of a point, with gravity
    public sealed class Particles : VisualElement
    {
        private sealed class Bit
        {
            public VisualElement View;
            public Vector2 Position, Velocity;
            public float Life, Age, Spin, Angle;
        }

        private readonly List<Bit> _bits = new List<Bit>();
        private readonly System.Random _random = new System.Random();

        public Particles()
        {
            Ui.Fill(this);
            pickingMode = PickingMode.Ignore;
            schedule.Execute(Tick).Every(16);
        }

        private static readonly Color[] Confetti =
        {
            new Color(0.98f, 0.79f, 0.32f), new Color(1f, 0.34f, 0.3f), new Color(0.33f, 0.66f, 1f),
            new Color(0.42f, 0.9f, 0.5f), new Color(0.78f, 0.5f, 1f), new Color(0.98f, 0.95f, 0.87f),
        };

        public void Coins(Vector2 at, int count, float speed = 420)
        {
            for (int i = 0; i < count; i++)
            {
                var coin = new SuitIcon(SuitIcon.Shape.Coin, Ui.Gold, 12 + (float)_random.NextDouble() * 8);
                Spawn(coin, at, speed, 1.1f);
            }
        }

        public void Burst(Vector2 at, int count, float speed = 520)
        {
            for (int i = 0; i < count; i++)
            {
                var bit = new VisualElement();
                bit.style.width = 6 + _random.Next(6);
                bit.style.height = 10 + _random.Next(8);
                bit.style.backgroundColor = Confetti[_random.Next(Confetti.Length)];
                Ui.Rounded(bit, 2);
                Spawn(bit, at, speed, 1.6f);
            }
        }

        private const int MaxBits = 220;

        private void Spawn(VisualElement view, Vector2 at, float speed, float life)
        {
            // a flood of bursts at once (a fast replay) drops the oldest bits
            if (_bits.Count >= MaxBits)
            {
                _bits[0].View.RemoveFromHierarchy();
                _bits.RemoveAt(0);
            }
            view.pickingMode = PickingMode.Ignore;
            view.style.position = Position.Absolute;
            Add(view);
            float angle = (float)(_random.NextDouble() * Math.PI * 2);
            float power = speed * (0.35f + (float)_random.NextDouble() * 0.65f);
            _bits.Add(new Bit
            {
                View = view,
                Position = at,
                Velocity = new Vector2(Mathf.Cos(angle) * power, Mathf.Sin(angle) * power - speed * 0.55f),
                Life = life * (0.7f + (float)_random.NextDouble() * 0.6f),
                Spin = (float)_random.NextDouble() * 720 - 360,
            });
        }

        private void Tick()
        {
            const float dt = 0.016f;
            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                var b = _bits[i];
                b.Age += dt;
                if (b.Age >= b.Life)
                {
                    b.View.RemoveFromHierarchy();
                    _bits.RemoveAt(i);
                    continue;
                }
                b.Velocity += new Vector2(0, 900) * dt;
                b.Velocity *= 0.985f;
                b.Position += b.Velocity * dt;
                b.Angle += b.Spin * dt;
                b.View.style.left = b.Position.x;
                b.View.style.top = b.Position.y;
                b.View.style.rotate = new Rotate(b.Angle);
                b.View.style.opacity = Mathf.Clamp01((b.Life - b.Age) / 0.4f);
            }
        }
    }
}
