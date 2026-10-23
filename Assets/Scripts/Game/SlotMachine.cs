using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // the slot machine room's cabinet: three reels that spin and stop one by one on each pull
    public sealed class SlotMachine : VisualElement
    {
        private const int SpinFrames = 14;

        private readonly VisualElement[] _windows = new VisualElement[3];
        private readonly Label _lights;
        private int _pulls = -1;
        private bool _spinning;

        // the reels stopped and the result can show
        public bool Spinning => _spinning;
        public System.Action Stopped;

        public SlotMachine()
        {
            style.alignItems = Align.Center;
            style.width = 360;
            Ui.Padding(this, 14, 18);
            style.backgroundImage = Ui.Gradient(new Color(0.62f, 0.08f, 0.12f), new Color(0.3f, 0.02f, 0.05f));
            Ui.Rounded(this, 26);
            Ui.Border(this, 4, Ui.Gold);

            _lights = Ui.Title(this, "JACKPOT", 26, Ui.Gold);
            var reels = Ui.Row(this, 10);
            reels.style.marginTop = 8;
            reels.style.backgroundColor = new Color(0.08f, 0.04f, 0.04f);
            Ui.Rounded(reels, 12);
            Ui.Padding(reels, 10, 10);
            for (int i = 0; i < 3; i++)
            {
                var window = new VisualElement();
                window.style.width = 92;
                window.style.height = 104;
                window.style.backgroundColor = new Color(0.98f, 0.96f, 0.9f);
                Ui.Rounded(window, 8);
                window.style.alignItems = Align.Center;
                window.style.justifyContent = Justify.Center;
                window.style.overflow = Overflow.Hidden;
                Ui.Add(reels, window);
                _windows[i] = window;
                Show(i, (SlotSymbol)(i * 2 % 5));
            }

            // the marquee lights chase while the machine is idle
            int frame = 0;
            schedule.Execute(() =>
            {
                frame++;
                if (Settings.ReducedMotion) return;
                _lights.style.opacity = frame % 10 < 5 ? 1f : 0.65f;
            }).Every(120);
        }

        // spin when there's a new pull to show, otherwise just show the last reels
        public void Set(Room room)
        {
            bool fresh = _pulls >= 0 && room.Pulls > _pulls && room.Reels != null;
            _pulls = room.Pulls;
            if (room.Reels == null) return;
            if (!fresh || Settings.ReducedMotion)
            {
                for (int i = 0; i < 3; i++) Show(i, room.Reels[i]);
                if (fresh) Stopped?.Invoke();
                return;
            }
            Spin(room.Reels);
        }

        // a new room: forget the last machine's pulls
        public void Reset() => _pulls = -1;

        private void Spin(SlotSymbol[] result)
        {
            _spinning = true;
            var random = new System.Random(result.GetHashCode() ^ _pulls);
            for (int frame = 0; frame <= SpinFrames + 8; frame++)
            {
                int f = frame;
                schedule.Execute(() =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        // each reel stops a few frames after the one to its left
                        int stopAt = SpinFrames + i * 4;
                        if (f < stopAt) Show(i, (SlotSymbol)random.Next(5), blur: true);
                        else if (f == stopAt)
                        {
                            Show(i, result[i]);
                            Ui.Pop(_windows[i], 1.12f);
                        }
                    }
                    if (f == SpinFrames + 8)
                    {
                        _spinning = false;
                        Stopped?.Invoke();
                    }
                }).StartingIn(f * 70);
            }
        }

        private void Show(int reel, SlotSymbol symbol, bool blur = false)
        {
            var window = _windows[reel];
            window.Clear();
            var view = new SlotSymbolView(symbol, 70);
            view.style.opacity = blur ? 0.55f : 1;
            if (blur) view.style.translate = new Translate(0, Random.Range(-14f, 14f));
            window.Add(view);
        }
    }

    // one reel symbol drawn in code: cherries, a bell, a BAR, a 7 or a lemon
    public sealed class SlotSymbolView : VisualElement
    {
        private readonly SlotSymbol _symbol;

        public SlotSymbolView(SlotSymbol symbol, float size)
        {
            _symbol = symbol;
            style.width = style.height = size;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            pickingMode = PickingMode.Ignore;
            if (symbol == SlotSymbol.Seven)
            {
                var seven = Ui.Title(this, "7", (int)(size * 0.9f), new Color(0.85f, 0.1f, 0.12f), glow: false);
                seven.pickingMode = PickingMode.Ignore;
            }
            else if (symbol == SlotSymbol.Bar)
            {
                var bar = Ui.Text(this, "BAR", (int)(size * 0.3f), Ui.Cream, Weight.Heavy);
                bar.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
                Ui.Rounded(bar, 6);
                Ui.Padding(bar, 4, 8);
                bar.style.letterSpacing = 2;
                bar.pickingMode = PickingMode.Ignore;
            }
            else generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            float s = contentRect.width;
            if (s <= 0) return;
            var p = context.painter2D;
            Vector2 P(float x, float y) => new Vector2(x * s, y * s);
            void Dot(float x, float y, float r, Color color)
            {
                p.fillColor = color;
                p.BeginPath();
                p.Arc(P(x, y), r * s, 0, 360);
                p.Fill();
            }

            switch (_symbol)
            {
                case SlotSymbol.Cherry:
                    p.strokeColor = new Color(0.2f, 0.5f, 0.15f);
                    p.lineWidth = s * 0.05f;
                    p.lineCap = LineCap.Round;
                    p.BeginPath();
                    p.MoveTo(P(0.3f, 0.62f));
                    p.QuadraticCurveTo(P(0.4f, 0.3f), P(0.6f, 0.14f));
                    p.MoveTo(P(0.68f, 0.66f));
                    p.QuadraticCurveTo(P(0.66f, 0.35f), P(0.6f, 0.14f));
                    p.Stroke();
                    p.fillColor = new Color(0.3f, 0.65f, 0.2f);
                    p.BeginPath();
                    p.MoveTo(P(0.6f, 0.14f));
                    p.QuadraticCurveTo(P(0.82f, 0.06f), P(0.86f, 0.22f));
                    p.QuadraticCurveTo(P(0.7f, 0.26f), P(0.6f, 0.14f));
                    p.Fill();
                    Dot(0.3f, 0.7f, 0.17f, new Color(0.82f, 0.08f, 0.12f));
                    Dot(0.68f, 0.74f, 0.17f, new Color(0.75f, 0.05f, 0.1f));
                    Dot(0.25f, 0.65f, 0.04f, new Color(1, 1, 1, 0.6f));
                    Dot(0.63f, 0.69f, 0.04f, new Color(1, 1, 1, 0.6f));
                    break;
                case SlotSymbol.Bell:
                    // the outline as plain points: a dome that flares out at the rim
                    p.fillColor = new Color(0.95f, 0.72f, 0.15f);
                    p.BeginPath();
                    p.MoveTo(P(0.15f, 0.78f));
                    for (int i = 0; i <= 16; i++)
                    {
                        float a = Mathf.PI * i / 16;
                        float x = 0.5f - Mathf.Cos(a) * 0.24f;
                        float y = 0.42f - Mathf.Sin(a) * 0.28f;
                        p.LineTo(P(x, y));
                    }
                    p.LineTo(P(0.74f, 0.6f));
                    p.LineTo(P(0.85f, 0.78f));
                    p.LineTo(P(0.15f, 0.78f));
                    p.LineTo(P(0.26f, 0.6f));
                    p.ClosePath();
                    p.Fill();
                    Dot(0.5f, 0.84f, 0.08f, new Color(0.7f, 0.45f, 0.08f));
                    Dot(0.5f, 0.11f, 0.05f, new Color(0.7f, 0.45f, 0.08f));
                    break;
                default:
                    // a lemon: a fat ellipse with pointed ends
                    p.fillColor = new Color(0.98f, 0.86f, 0.2f);
                    p.BeginPath();
                    p.MoveTo(P(0.1f, 0.5f));
                    p.QuadraticCurveTo(P(0.5f, 0.12f), P(0.9f, 0.5f));
                    p.QuadraticCurveTo(P(0.5f, 0.88f), P(0.1f, 0.5f));
                    p.Fill();
                    Dot(0.4f, 0.42f, 0.06f, new Color(1, 1, 1, 0.45f));
                    break;
            }
        }
    }
}
