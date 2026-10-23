using HouseEdge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // who runs a room: their name, what they do, and what they say when you walk in
    public readonly struct Host
    {
        public readonly string Name, Role, Greeting;

        public Host(string name, string role, string greeting)
        {
            Name = name;
            Role = role;
            Greeting = greeting;
        }

        public static Host For(Room room) => room.Kind switch
        {
            RoomKind.Bar => new Host("Sal", "Bartender", "What'll it be, friend? Everything's half price for regulars. And you look like a regular."),
            RoomKind.BackRoom => new Host("Vinnie", "Back Room Dealer", "One hand. No Vices, no tricks. Win and your gold doubles. Lose and it's mine."),
            RoomKind.PawnShop => new Host("Morty", "Pawnbroker", "I pay full price for Vices, no haggling. And I've got a Rare in the back... mostly genuine."),
            RoomKind.LoanShark => new Host("Finn", "Loan Shark", "Short on gold? I can help. Just pay me back by the end of next floor. With a little extra."),
            RoomKind.CardRoom => new Host("Dot", "Card Room Clerk", "Hand me a card from your deck. I can make it vanish, copy it, or give it a little something extra."),
            RoomKind.LostAndFound => new Host("Gus", "Lost and Found", "People leave the strangest things behind. Reach in the box and take one. No peeking."),
            RoomKind.SecurityOffice => new Host("Officer Reyes", "Night Guard", "You're getting a lot of attention, pal. I can make some of it go away. For a price."),
            RoomKind.HighLimitLounge => new Host("Madame Rose", "Lounge Host", "Why sit through the next table, darling? Relax here. The one after will be a little harder."),
            RoomKind.Slots => new Host("Cherry", "Change Girl", "Feeling lucky, sugar? Pull the lever! Cherries, bells, bars... and the big 7-7-7."),
            _ => room.Event switch
            {
                EventKind.LuckyChip => new Host("Mr. Big", "High Roller", "Here, kid, take my lucky chip! It's never let me down! ...Where'd my wallet go?"),
                EventKind.Hustler => new Host("Slick Eddie", "Hustler", "Double or nothing, friend? One flip. Heads you win, tails I win. Easy."),
                EventKind.SpilledTray => new Host("Jules", "Waiter", "Oh no, oh no. Sorry! Uh... one of these survived. Want it?"),
                EventKind.BackAlley => new Host("The Coat", "Back Alley Seller", "Psst. Rare goods. No refunds, no questions. Might be something stuck to it."),
                EventKind.Shredder => new Host("Hank", "Cleaner", "Got any junk cards? This old shredder's hungry. It only eats the small ones."),
                EventKind.PhotoBooth => new Host("Flash", "Photo Booth", "Smile! This booth prints a perfect copy of anything you put in it."),
                EventKind.SecuritySweep => new Host("Security", "Floor Sweep", "Faces against the list, people. You there. Look at the camera."),
                EventKind.CompTicket => new Host("The Usher", "Comps Desk", "Ah, a comp ticket! Cash it in for a Comp, or I'll buy it off you."),
                _ => new Host("Zappo", "Card Magician", "Pick a card, any card! Or better: let me teach you a trick of your own."),
            },
        };
    }

    // a room's backdrop, drawn in code: the wall, what hangs on it, a counter and the floor. some things move
    public sealed class RoomScene : VisualElement
    {
        private readonly RoomKind _kind;
        private readonly Color _wallTop, _wallBottom, _accent;
        // a photo sits behind: only the counter, the floor and the light are drawn over it
        private readonly bool _photo;
        private float _time;

        // the photo backdrop for a room, if there is one
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> Photos = new System.Collections.Generic.Dictionary<string, Texture2D>();

        // the preloader hands photos over as they finish loading
        public static void Keep(string name, Texture2D texture)
        {
            if (texture != null) Photos[name] = texture;
        }

        public static Texture2D Photo(RoomKind kind) => PhotoNamed(kind switch
        {
            RoomKind.Bar => "bar",
            RoomKind.Slots => "slots",
            RoomKind.PawnShop => "pawn",
            RoomKind.LoanShark => "vault",
            RoomKind.SecurityOffice => "security",
            RoomKind.LostAndFound => "lostfound",
            RoomKind.HighLimitLounge => "lounge",
            RoomKind.BackRoom => "backroom",
            RoomKind.CardRoom => "cardroom",
            RoomKind.Shop => "shop",
            _ => "mystery",
        });

        private static Texture2D PhotoNamed(string name)
        {
            if (Photos.TryGetValue(name, out var photo)) return photo;
            photo = Resources.Load<Texture2D>("Backgrounds/" + name);
            Photos[name] = photo;
            return photo;
        }

        public RoomScene(RoomKind kind, bool photo = false)
        {
            _kind = kind;
            _photo = photo;
            (_wallTop, _wallBottom, _accent) = Palette(kind);
            Ui.Fill(this);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            schedule.Execute(() =>
            {
                if (Settings.ReducedMotion || resolvedStyle.display == DisplayStyle.None) return;
                _time += 0.1f;
                MarkDirtyRepaint();
            }).Every(100);
        }

        private static (Color top, Color bottom, Color accent) Palette(RoomKind kind) => kind switch
        {
            RoomKind.Bar => (new Color(0.32f, 0.16f, 0.08f), new Color(0.14f, 0.06f, 0.03f), new Color(0.98f, 0.7f, 0.3f)),
            RoomKind.BackRoom => (new Color(0.28f, 0.04f, 0.06f), new Color(0.08f, 0.01f, 0.02f), new Color(1f, 0.4f, 0.3f)),
            RoomKind.PawnShop => (new Color(0.3f, 0.26f, 0.14f), new Color(0.12f, 0.1f, 0.05f), new Color(0.9f, 0.8f, 0.5f)),
            RoomKind.LoanShark => (new Color(0.08f, 0.2f, 0.22f), new Color(0.02f, 0.06f, 0.07f), new Color(0.5f, 0.9f, 0.85f)),
            RoomKind.CardRoom => (new Color(0.08f, 0.26f, 0.16f), new Color(0.02f, 0.09f, 0.05f), new Color(0.98f, 0.79f, 0.32f)),
            RoomKind.LostAndFound => (new Color(0.22f, 0.22f, 0.26f), new Color(0.08f, 0.08f, 0.1f), new Color(0.8f, 0.85f, 1f)),
            RoomKind.SecurityOffice => (new Color(0.08f, 0.12f, 0.2f), new Color(0.02f, 0.03f, 0.06f), new Color(0.4f, 1f, 0.6f)),
            RoomKind.HighLimitLounge => (new Color(0.32f, 0.04f, 0.16f), new Color(0.1f, 0.01f, 0.05f), new Color(1f, 0.75f, 0.85f)),
            RoomKind.Slots => (new Color(0.24f, 0.06f, 0.26f), new Color(0.06f, 0.01f, 0.08f), new Color(1f, 0.85f, 0.3f)),
            _ => (new Color(0.12f, 0.1f, 0.2f), new Color(0.03f, 0.02f, 0.06f), new Color(0.8f, 0.7f, 1f)),
        };

        private void Draw(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w <= 0) return;
            var p = context.painter2D;
            float floorY = h * 0.88f;

            if (!_photo)
            {
                // the wall, darkening toward the floor, with faint wallpaper stripes
                for (int i = 0; i < 14; i++)
                {
                    p.fillColor = Color.Lerp(_wallTop, _wallBottom, i / 13f);
                    Box(p, 0, floorY * i / 14, w, floorY / 14 + 1);
                }
                p.fillColor = new Color(1, 1, 1, 0.025f);
                for (float x = 20; x < w; x += 46) Box(p, x, 0, 14, floorY);
                Props(p, w, h);
            }

            // the counter everything sits on, then the floor
            p.fillColor = new Color(0.22f, 0.12f, 0.06f);
            Box(p, 0, floorY - 18, w, 22);
            p.fillColor = new Color(_accent.r, _accent.g, _accent.b, 0.35f);
            Box(p, 0, floorY - 18, w, 3);
            p.fillColor = new Color(0.05f, 0.03f, 0.02f);
            Box(p, 0, floorY + 4, w, h - floorY);
            p.strokeColor = new Color(1, 1, 1, 0.04f);
            p.lineWidth = 1;
            p.BeginPath();
            for (int i = -8; i <= 8; i++)
            {
                p.MoveTo(new Vector2(w / 2 + i * 40, floorY + 4));
                p.LineTo(new Vector2(w / 2 + i * 160, h));
            }
            p.Stroke();

            // a warm pool of light in the middle
            for (int i = 0; i < 10; i++)
            {
                float k = 1 - i / 10f;
                p.fillColor = new Color(_accent.r, _accent.g, _accent.b, 0.012f);
                Ellipse(p, new Vector2(w / 2, h * 0.45f), w * 0.42f * k, h * 0.45f * k);
            }
        }

        // what hangs on the wall: different for every room, and some of it moves
        private void Props(Painter2D p, float w, float h)
        {
            var random = new System.Random((int)_kind * 31 + 7);
            switch (_kind)
            {
                case RoomKind.Bar:
                case RoomKind.PawnShop:
                    // shelves of bottles, or of pawned odds and ends
                    for (int shelf = 0; shelf < 3; shelf++)
                    {
                        float y = h * (0.22f + shelf * 0.17f);
                        p.fillColor = new Color(0.35f, 0.2f, 0.1f);
                        Box(p, w * 0.04f, y, w * 0.92f, 6);
                        for (float x = w * 0.06f; x < w * 0.94f; x += 26 + random.Next(20))
                        {
                            var color = Color.HSVToRGB((float)random.NextDouble(), 0.55f, 0.55f + (float)random.NextDouble() * 0.3f);
                            color.a = 0.75f;
                            p.fillColor = color;
                            if (_kind == RoomKind.Bar)
                            {
                                float tall = 30 + random.Next(22);
                                Box(p, x, y - tall, 14, tall);
                                Box(p, x + 4, y - tall - 12, 6, 12);
                            }
                            else if (random.Next(2) == 0) Ellipse(p, new Vector2(x + 10, y - 14), 12, 13);
                            else Box(p, x, y - 22 - random.Next(16), 20, 22);
                        }
                    }
                    break;
                case RoomKind.LostAndFound:
                    // stacks of boxes with tape
                    for (int i = 0; i < 9; i++)
                    {
                        float bw = 70 + random.Next(50), bh = 50 + random.Next(30);
                        float x = i < 5 ? w * 0.02f + (i % 2) * 60 : w * 0.78f + (i % 2) * 50;
                        float y = h * 0.88f - 18 - bh * (1 + i % 3);
                        p.fillColor = new Color(0.5f + random.Next(20) / 100f, 0.36f, 0.2f, 0.8f);
                        Box(p, x, y, bw, bh);
                        p.fillColor = new Color(0.85f, 0.8f, 0.6f, 0.5f);
                        Box(p, x + bw / 2 - 5, y, 10, bh);
                    }
                    break;
                case RoomKind.SecurityOffice:
                    // a wall of monitors that flicker
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col < 6; col++)
                        {
                            float x = w * 0.08f + col * w * 0.145f, y = h * 0.08f + row * h * 0.17f;
                            p.fillColor = new Color(0.1f, 0.1f, 0.12f);
                            Box(p, x, y, w * 0.13f, h * 0.15f);
                            float flicker = 0.07f + 0.1f * Mathf.PerlinNoise(_time * 3 + col, row * 7.3f);
                            p.fillColor = new Color(0.3f, 0.9f, 0.5f, flicker);
                            Box(p, x + 4, y + 4, w * 0.13f - 8, h * 0.15f - 8);
                        }
                    break;
                case RoomKind.LoanShark:
                    // a big safe, and a desk lamp swinging slightly
                    p.fillColor = new Color(0.3f, 0.33f, 0.36f);
                    Box(p, w * 0.76f, h * 0.36f, w * 0.18f, h * 0.52f - 18);
                    p.strokeColor = new Color(0.7f, 0.75f, 0.8f, 0.8f);
                    p.lineWidth = 4;
                    p.BeginPath();
                    p.Arc(new Vector2(w * 0.85f, h * 0.55f), 22, 0, 360);
                    p.Stroke();
                    Lamp(p, w * 0.22f, h, 0.03f);
                    break;
                case RoomKind.BackRoom:
                    Lamp(p, w * 0.5f, h, 0.06f);
                    break;
                case RoomKind.CardRoom:
                    // framed giant cards on the wall
                    for (int i = 0; i < 5; i++)
                    {
                        float x = w * (0.1f + i * 0.18f), y = h * 0.12f + (i % 2) * 20;
                        p.fillColor = new Color(0.45f, 0.32f, 0.12f);
                        Box(p, x - 6, y - 6, 72, 100);
                        p.fillColor = new Color(0.96f, 0.94f, 0.88f);
                        Box(p, x, y, 60, 88);
                        p.fillColor = i % 2 == 0 ? new Color(0.8f, 0.1f, 0.14f) : new Color(0.1f, 0.1f, 0.13f);
                        Ellipse(p, new Vector2(x + 30, y + 44), 12, 14);
                    }
                    break;
                case RoomKind.HighLimitLounge:
                    // velvet curtains on both sides
                    for (int side = 0; side < 2; side++)
                        for (int fold = 0; fold < 5; fold++)
                        {
                            float x = side == 0 ? fold * 26 : w - (fold + 1) * 26;
                            p.fillColor = fold % 2 == 0 ? new Color(0.55f, 0.05f, 0.18f) : new Color(0.4f, 0.03f, 0.12f);
                            Box(p, x, 0, 27, h * 0.88f - 18);
                        }
                    break;
                case RoomKind.Slots:
                    // a row of machines with chasing bulbs
                    for (int i = 0; i < 7; i++)
                    {
                        if (i == 3) continue;
                        float x = w * (0.04f + i * 0.14f), y = h * 0.3f;
                        p.fillColor = new Color(0.45f, 0.08f, 0.12f, 0.8f);
                        Box(p, x, y, w * 0.11f, h * 0.58f - 18);
                        for (int b = 0; b < 5; b++)
                        {
                            bool on = ((int)(_time * 6) + b + i) % 3 == 0;
                            p.fillColor = on ? new Color(1f, 0.9f, 0.4f, 0.9f) : new Color(0.5f, 0.4f, 0.2f, 0.6f);
                            Ellipse(p, new Vector2(x + 8 + b * (w * 0.11f - 16) / 4, y + 8), 4, 4);
                        }
                    }
                    break;
                default:
                    // "?": a spotlight sweeping the dark, and question marks drifting in it
                    float sweep = Mathf.Sin(_time * 0.6f) * w * 0.25f;
                    for (int i = 0; i < 8; i++)
                    {
                        float k = 1 - i / 8f;
                        p.fillColor = new Color(0.8f, 0.7f, 1f, 0.02f);
                        Ellipse(p, new Vector2(w / 2 + sweep, h * 0.35f), w * 0.22f * k, h * 0.3f * k);
                    }
                    break;
            }
        }

        // a hanging lamp that sways, with a cone of light under it
        private void Lamp(Painter2D p, float x, float h, float swing)
        {
            float angle = Mathf.Sin(_time * 1.2f) * swing;
            var bulb = new Vector2(x + Mathf.Sin(angle) * h * 0.3f, h * 0.3f);
            p.strokeColor = new Color(0.1f, 0.1f, 0.1f);
            p.lineWidth = 2;
            p.BeginPath();
            p.MoveTo(new Vector2(x, 0));
            p.LineTo(bulb);
            p.Stroke();
            p.fillColor = new Color(1f, 0.9f, 0.6f, 0.05f);
            p.BeginPath();
            p.MoveTo(bulb);
            p.LineTo(bulb + new Vector2(-h * 0.35f, h * 0.48f));
            p.LineTo(bulb + new Vector2(h * 0.35f, h * 0.48f));
            p.ClosePath();
            p.Fill();
            p.fillColor = new Color(0.2f, 0.2f, 0.18f);
            p.BeginPath();
            p.MoveTo(bulb + new Vector2(-24, 0));
            p.LineTo(bulb + new Vector2(24, 0));
            p.LineTo(bulb + new Vector2(10, -16));
            p.LineTo(bulb + new Vector2(-10, -16));
            p.ClosePath();
            p.Fill();
            Ellipse(p, bulb + new Vector2(0, 2), 6, 5);
        }

        private static void Box(Painter2D p, float x, float y, float w, float h)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x, y));
            p.LineTo(new Vector2(x + w, y));
            p.LineTo(new Vector2(x + w, y + h));
            p.LineTo(new Vector2(x, y + h));
            p.ClosePath();
            p.Fill();
        }

        private static void Ellipse(Painter2D p, Vector2 center, float rx, float ry)
        {
            p.BeginPath();
            for (int i = 0; i <= 28; i++)
            {
                float a = i * Mathf.PI * 2 / 28;
                var point = center + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                if (i == 0) p.MoveTo(point);
                else p.LineTo(point);
            }
            p.ClosePath();
            p.Fill();
        }
    }
}
