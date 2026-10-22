using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // a boss's face, drawn with Painter2D from a few parts: backdrop, suit, head, hat, eyes, mouth and an extra
    public sealed class BossPortrait : VisualElement
    {
        public enum Hat { None, TopHat, TallHat, Bowler, Fedora, Beret, Visor, Crown, Bun, Beanie, SlickHair, WildHair }
        public enum Eyes { Dots, Shifty, Shades, Monocle, Glasses, Mime, Glow }
        public enum Mouth { Smile, Smirk, Flat, Frown, Mustache, Mask, Cigar, Beard }
        public enum Extra { None, Twin, GiantEye, Scar, Cards, Stripes }

        public readonly struct Look
        {
            public readonly Color Backdrop, Suit, Skin, HatColor;
            public readonly Hat Hat;
            public readonly Eyes Eyes;
            public readonly Mouth Mouth;
            public readonly Extra Extra;

            public Look(Color backdrop, Color suit, Color skin, Hat hat, Color hatColor, Eyes eyes, Mouth mouth, Extra extra = Extra.None)
            {
                Backdrop = backdrop;
                Suit = suit;
                Skin = skin;
                Hat = hat;
                HatColor = hatColor;
                Eyes = eyes;
                Mouth = mouth;
                Extra = extra;
            }
        }

        private static Color C(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        private static readonly Color Skin = C("#E9B98F"), Pale = C("#F6E0CC"), Tan = C("#C98F63");

        public static Look For(string boss) => boss switch
        {
            "The Twins" => new Look(C("#4B2A6B"), C("#2E1745"), Pale, Hat.Bowler, C("#111111"), Eyes.Dots, Mouth.Smirk, Extra.Twin),
            "The Iceman" => new Look(C("#2D6C8C"), C("#DDEFF7"), C("#CFE6F2"), Hat.SlickHair, C("#F4FAFF"), Eyes.Shades, Mouth.Flat),
            "The Cheat" => new Look(C("#1F5C3A"), C("#2F7A4B"), Skin, Hat.Visor, C("#39B36A"), Eyes.Shifty, Mouth.Smirk, Extra.Cards),
            "The Mime" => new Look(C("#202020"), C("#111111"), C("#F7F7F2"), Hat.Beret, C("#111111"), Eyes.Mime, Mouth.Frown, Extra.Stripes),
            "The Taxman" => new Look(C("#2C4A3A"), C("#5B5E66"), Pale, Hat.Bowler, C("#1C1C1C"), Eyes.Glasses, Mouth.Mustache),
            "The Purist" => new Look(C("#6B3A5A"), C("#9C6FA0"), Pale, Hat.Bun, C("#B9B4B0"), Eyes.Monocle, Mouth.Flat),
            "The Ceiling" => new Look(C("#2F4C6E"), C("#1A2C45"), Skin, Hat.TallHat, C("#15151A"), Eyes.Dots, Mouth.Smile),
            "The Sweeper" => new Look(C("#6B4A2B"), C("#4A6B8A"), Tan, Hat.Beanie, C("#C2412B"), Eyes.Dots, Mouth.Mustache),
            "The Silencer" => new Look(C("#2B2F3A"), C("#14161C"), Skin, Hat.Fedora, C("#22252E"), Eyes.Shifty, Mouth.Mask),
            "The Card Mechanic" => new Look(C("#7A1E24"), C("#3B0F14"), Skin, Hat.SlickHair, C("#1A1110"), Eyes.Dots, Mouth.Smirk, Extra.Cards),
            "The Hoarder" => new Look(C("#5A5A2A"), C("#6B4F2A"), Tan, Hat.WildHair, C("#9A9A92"), Eyes.Dots, Mouth.Beard),
            "The Pit Boss" => new Look(C("#6E1515"), C("#A8201E"), Tan, Hat.SlickHair, C("#120E0C"), Eyes.Shades, Mouth.Cigar),
            "The Eye in the Sky" => new Look(C("#14181F"), C("#14181F"), Skin, Hat.None, Color.clear, Eyes.Dots, Mouth.Flat, Extra.GiantEye),
            "The Enforcer" => new Look(C("#2A2A2E"), C("#0E0E10"), Tan, Hat.None, Color.clear, Eyes.Shades, Mouth.Flat, Extra.Scar),
            "The House" => new Look(C("#3A2A08"), C("#0A0A0C"), C("#2B2620"), Hat.Crown, C("#F2C14E"), Eyes.Glow, Mouth.Smirk),
            _ => new Look(C("#333333"), C("#222222"), Skin, Hat.Fedora, C("#222222"), Eyes.Dots, Mouth.Flat),
        };

        private readonly Look _look;

        public BossPortrait(string boss, float size)
        {
            _look = For(boss);
            style.width = style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            Ui.Rounded(this, size * 0.16f);
            style.overflow = Overflow.Hidden;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            float s = contentRect.width;
            if (s <= 0) return;
            var p = context.painter2D;
            Vector2 At(float x, float y) => new Vector2(x * s, y * s);
            var look = _look;

            // backdrop with a soft spotlight
            p.fillColor = look.Backdrop;
            Rect(p, At(0, 0), At(1, 1));
            p.fillColor = Color.Lerp(look.Backdrop, Color.white, 0.12f);
            Disc(p, At(0.5f, 0.42f), 0.42f * s);

            if (look.Extra == Extra.GiantEye)
            {
                GiantEye(p, At, s);
                return;
            }
            if (look.Extra == Extra.Twin)
            {
                Person(p, At, s, look, -0.18f, 0.85f);
                Person(p, At, s, look, 0.18f, 0.85f);
                return;
            }
            Person(p, At, s, look, 0, 1);
        }

        // one figure, shifted sideways by dx and scaled by k around the bottom middle
        private static void Person(Painter2D p, System.Func<float, float, Vector2> at, float s, Look look, float dx, float k)
        {
            Vector2 A(float x, float y) => at(0.5f + (x - 0.5f) * k + dx, 1 - (1 - y) * k);

            // shoulders
            p.fillColor = look.Suit;
            p.BeginPath();
            p.MoveTo(A(0.1f, 1.05f));
            p.BezierCurveTo(A(0.12f, 0.78f), A(0.3f, 0.72f), A(0.5f, 0.72f));
            p.BezierCurveTo(A(0.7f, 0.72f), A(0.88f, 0.78f), A(0.9f, 1.05f));
            p.ClosePath();
            p.Fill();
            if (look.Extra == Extra.Stripes)
            {
                p.fillColor = new Color(0.95f, 0.95f, 0.95f);
                for (int i = 0; i < 3; i++) Rect(p, A(0.14f, 0.82f + i * 0.07f), A(0.86f, 0.85f + i * 0.07f));
            }
            // collar and tie
            p.fillColor = new Color(0.96f, 0.95f, 0.92f);
            Tri(p, A(0.42f, 0.73f), A(0.58f, 0.73f), A(0.5f, 0.84f));
            p.fillColor = Color.Lerp(look.Backdrop, Color.red, 0.4f);
            Tri(p, A(0.47f, 0.76f), A(0.53f, 0.76f), A(0.5f, 0.9f));

            // neck and head
            p.fillColor = Color.Lerp(look.Skin, Color.black, 0.15f);
            Rect(p, A(0.44f, 0.6f), A(0.56f, 0.74f));
            p.fillColor = look.Skin;
            Ellipse(p, A(0.5f, 0.45f), 0.17f * s * k, 0.2f * s * k);

            HatBehind(p, A, s * k, look);
            Face(p, A, s * k, look);
            HatFront(p, A, s * k, look);

            if (look.Extra == Extra.Scar)
            {
                p.strokeColor = new Color(0.55f, 0.15f, 0.15f);
                p.lineWidth = s * k * 0.012f;
                p.BeginPath();
                p.MoveTo(A(0.58f, 0.36f));
                p.LineTo(A(0.62f, 0.52f));
                p.Stroke();
            }
            if (look.Extra == Extra.Cards)
            {
                // a fan of cards held up by the shoulder
                for (int i = 0; i < 3; i++)
                {
                    float x = 0.72f + i * 0.05f;
                    p.fillColor = i == 1 ? new Color(0.98f, 0.97f, 0.93f) : new Color(0.93f, 0.92f, 0.88f);
                    p.BeginPath();
                    p.MoveTo(A(x, 0.7f));
                    p.LineTo(A(x + 0.1f, 0.68f));
                    p.LineTo(A(x + 0.12f, 0.86f));
                    p.LineTo(A(x + 0.02f, 0.88f));
                    p.ClosePath();
                    p.Fill();
                }
                p.fillColor = new Color(0.8f, 0.1f, 0.14f);
                Disc(p, A(0.83f, 0.77f), 0.018f * s * k);
            }
        }

        private static void HatBehind(Painter2D p, System.Func<float, float, Vector2> A, float s, Look look)
        {
            p.fillColor = look.HatColor;
            switch (look.Hat)
            {
                case Hat.WildHair:
                    for (int i = 0; i < 7; i++)
                    {
                        float angle = Mathf.PI * (0.9f + i * 0.2f);
                        Disc(p, A(0.5f + Mathf.Cos(angle) * 0.18f, 0.42f + Mathf.Sin(angle) * 0.17f), 0.07f * s);
                    }
                    break;
                case Hat.Bun:
                    Disc(p, A(0.5f, 0.22f), 0.07f * s);
                    break;
            }
        }

        private static void HatFront(Painter2D p, System.Func<float, float, Vector2> A, float s, Look look)
        {
            p.fillColor = look.HatColor;
            switch (look.Hat)
            {
                case Hat.TopHat:
                case Hat.TallHat:
                    float top = look.Hat == Hat.TallHat ? 0.0f : 0.12f;
                    Rect(p, A(0.36f, top), A(0.64f, 0.31f));
                    Rect(p, A(0.27f, 0.29f), A(0.73f, 0.33f));
                    p.fillColor = new Color(0.6f, 0.1f, 0.12f);
                    Rect(p, A(0.36f, 0.26f), A(0.64f, 0.29f));
                    break;
                case Hat.Bowler:
                    Ellipse(p, A(0.5f, 0.3f), 0.15f * s, 0.1f * s);
                    Rect(p, A(0.3f, 0.3f), A(0.7f, 0.33f));
                    break;
                case Hat.Fedora:
                    Ellipse(p, A(0.5f, 0.3f), 0.16f * s, 0.09f * s);
                    Ellipse(p, A(0.5f, 0.33f), 0.27f * s, 0.035f * s);
                    p.fillColor = new Color(0, 0, 0, 0.45f);
                    Rect(p, A(0.35f, 0.3f), A(0.65f, 0.32f));
                    break;
                case Hat.Beret:
                    Ellipse(p, A(0.47f, 0.29f), 0.2f * s, 0.07f * s);
                    Disc(p, A(0.5f, 0.21f), 0.015f * s);
                    break;
                case Hat.Visor:
                    p.fillColor = new Color(look.HatColor.r, look.HatColor.g, look.HatColor.b, 0.85f);
                    Tri(p, A(0.3f, 0.34f), A(0.7f, 0.34f), A(0.5f, 0.42f));
                    Rect(p, A(0.33f, 0.31f), A(0.67f, 0.34f));
                    break;
                case Hat.Crown:
                    p.BeginPath();
                    p.MoveTo(A(0.33f, 0.3f));
                    p.LineTo(A(0.33f, 0.12f));
                    p.LineTo(A(0.42f, 0.22f));
                    p.LineTo(A(0.5f, 0.08f));
                    p.LineTo(A(0.58f, 0.22f));
                    p.LineTo(A(0.67f, 0.12f));
                    p.LineTo(A(0.67f, 0.3f));
                    p.ClosePath();
                    p.Fill();
                    p.fillColor = new Color(0.8f, 0.1f, 0.14f);
                    Disc(p, A(0.5f, 0.24f), 0.022f * s);
                    break;
                case Hat.Beanie:
                    Ellipse(p, A(0.5f, 0.31f), 0.18f * s, 0.11f * s);
                    p.fillColor = Color.Lerp(look.HatColor, Color.white, 0.25f);
                    Rect(p, A(0.32f, 0.33f), A(0.68f, 0.37f));
                    Disc(p, A(0.5f, 0.19f), 0.03f * s);
                    break;
                case Hat.SlickHair:
                    p.BeginPath();
                    p.MoveTo(A(0.33f, 0.42f));
                    p.BezierCurveTo(A(0.32f, 0.22f), A(0.66f, 0.2f), A(0.68f, 0.38f));
                    p.BezierCurveTo(A(0.6f, 0.3f), A(0.45f, 0.3f), A(0.33f, 0.42f));
                    p.ClosePath();
                    p.Fill();
                    break;
            }
        }

        private static void Face(Painter2D p, System.Func<float, float, Vector2> A, float s, Look look)
        {
            var ink = new Color(0.1f, 0.08f, 0.08f);
            switch (look.Eyes)
            {
                case Eyes.Dots:
                    p.fillColor = ink;
                    Disc(p, A(0.44f, 0.44f), 0.018f * s);
                    Disc(p, A(0.56f, 0.44f), 0.018f * s);
                    break;
                case Eyes.Shifty:
                    p.fillColor = ink;
                    Disc(p, A(0.45f, 0.45f), 0.016f * s);
                    Disc(p, A(0.57f, 0.45f), 0.016f * s);
                    p.strokeColor = ink;
                    p.lineWidth = 0.014f * s;
                    p.BeginPath();
                    p.MoveTo(A(0.4f, 0.4f));
                    p.LineTo(A(0.48f, 0.42f));
                    p.MoveTo(A(0.6f, 0.4f));
                    p.LineTo(A(0.52f, 0.42f));
                    p.Stroke();
                    break;
                case Eyes.Shades:
                    p.fillColor = new Color(0.05f, 0.05f, 0.07f);
                    Rect(p, A(0.36f, 0.415f), A(0.64f, 0.43f));
                    Ellipse(p, A(0.435f, 0.445f), 0.055f * s, 0.032f * s);
                    Ellipse(p, A(0.565f, 0.445f), 0.055f * s, 0.032f * s);
                    p.fillColor = new Color(1, 1, 1, 0.35f);
                    Disc(p, A(0.42f, 0.437f), 0.008f * s);
                    Disc(p, A(0.55f, 0.437f), 0.008f * s);
                    break;
                case Eyes.Monocle:
                    p.fillColor = ink;
                    Disc(p, A(0.44f, 0.44f), 0.016f * s);
                    Disc(p, A(0.56f, 0.44f), 0.016f * s);
                    p.strokeColor = new Color(0.9f, 0.75f, 0.3f);
                    p.lineWidth = 0.01f * s;
                    p.BeginPath();
                    p.Arc(A(0.56f, 0.44f), 0.04f * s, 0, 360);
                    p.Stroke();
                    break;
                case Eyes.Glasses:
                    p.fillColor = ink;
                    Disc(p, A(0.44f, 0.44f), 0.014f * s);
                    Disc(p, A(0.56f, 0.44f), 0.014f * s);
                    p.strokeColor = ink;
                    p.lineWidth = 0.01f * s;
                    p.BeginPath();
                    p.Arc(A(0.44f, 0.44f), 0.038f * s, 0, 360);
                    p.Stroke();
                    p.BeginPath();
                    p.Arc(A(0.56f, 0.44f), 0.038f * s, 0, 360);
                    p.Stroke();
                    break;
                case Eyes.Mime:
                    p.fillColor = ink;
                    Tri(p, A(0.44f, 0.4f), A(0.42f, 0.47f), A(0.46f, 0.47f));
                    Tri(p, A(0.56f, 0.4f), A(0.54f, 0.47f), A(0.58f, 0.47f));
                    break;
                case Eyes.Glow:
                    p.fillColor = new Color(1f, 0.82f, 0.3f);
                    Ellipse(p, A(0.44f, 0.44f), 0.03f * s, 0.014f * s);
                    Ellipse(p, A(0.56f, 0.44f), 0.03f * s, 0.014f * s);
                    break;
            }

            switch (look.Mouth)
            {
                case Mouth.Smile:
                case Mouth.Smirk:
                case Mouth.Frown:
                    p.strokeColor = look.Mouth == Mouth.Frown && look.Eyes == Eyes.Mime ? new Color(0.75f, 0.1f, 0.15f) : ink;
                    p.lineWidth = 0.014f * s;
                    p.lineCap = LineCap.Round;
                    p.BeginPath();
                    if (look.Mouth == Mouth.Smile)
                    {
                        p.MoveTo(A(0.44f, 0.53f));
                        p.QuadraticCurveTo(A(0.5f, 0.58f), A(0.56f, 0.53f));
                    }
                    else if (look.Mouth == Mouth.Smirk)
                    {
                        p.MoveTo(A(0.44f, 0.545f));
                        p.QuadraticCurveTo(A(0.52f, 0.56f), A(0.57f, 0.52f));
                    }
                    else
                    {
                        p.MoveTo(A(0.45f, 0.56f));
                        p.QuadraticCurveTo(A(0.5f, 0.52f), A(0.55f, 0.56f));
                    }
                    p.Stroke();
                    break;
                case Mouth.Flat:
                    p.fillColor = ink;
                    Rect(p, A(0.45f, 0.54f), A(0.55f, 0.552f));
                    break;
                case Mouth.Mustache:
                    p.fillColor = new Color(0.25f, 0.17f, 0.12f);
                    Ellipse(p, A(0.46f, 0.52f), 0.05f * s, 0.018f * s);
                    Ellipse(p, A(0.54f, 0.52f), 0.05f * s, 0.018f * s);
                    break;
                case Mouth.Mask:
                    p.fillColor = new Color(0.08f, 0.08f, 0.1f);
                    p.BeginPath();
                    p.MoveTo(A(0.33f, 0.48f));
                    p.LineTo(A(0.67f, 0.48f));
                    p.LineTo(A(0.64f, 0.6f));
                    p.QuadraticCurveTo(A(0.5f, 0.66f), A(0.36f, 0.6f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case Mouth.Cigar:
                    p.fillColor = ink;
                    Rect(p, A(0.46f, 0.535f), A(0.53f, 0.55f));
                    p.fillColor = new Color(0.45f, 0.28f, 0.15f);
                    Rect(p, A(0.52f, 0.53f), A(0.68f, 0.55f));
                    p.fillColor = new Color(1f, 0.5f, 0.15f);
                    Rect(p, A(0.675f, 0.53f), A(0.69f, 0.55f));
                    p.fillColor = new Color(1, 1, 1, 0.35f);
                    Disc(p, A(0.72f, 0.5f), 0.015f * s);
                    Disc(p, A(0.74f, 0.46f), 0.02f * s);
                    break;
                case Mouth.Beard:
                    p.fillColor = look.HatColor;
                    p.BeginPath();
                    p.MoveTo(A(0.34f, 0.47f));
                    p.QuadraticCurveTo(A(0.5f, 0.75f), A(0.66f, 0.47f));
                    p.QuadraticCurveTo(A(0.5f, 0.56f), A(0.34f, 0.47f));
                    p.ClosePath();
                    p.Fill();
                    break;
            }
        }

        // the Eye in the Sky: one enormous watching eye
        private static void GiantEye(Painter2D p, System.Func<float, float, Vector2> At, float s)
        {
            p.fillColor = new Color(0.96f, 0.95f, 0.9f);
            p.BeginPath();
            p.MoveTo(At(0.08f, 0.5f));
            p.BezierCurveTo(At(0.3f, 0.2f), At(0.7f, 0.2f), At(0.92f, 0.5f));
            p.BezierCurveTo(At(0.7f, 0.8f), At(0.3f, 0.8f), At(0.08f, 0.5f));
            p.ClosePath();
            p.Fill();
            p.fillColor = new Color(0.75f, 0.12f, 0.12f);
            Disc(p, At(0.5f, 0.5f), 0.16f * s);
            p.fillColor = new Color(0.05f, 0.05f, 0.06f);
            Disc(p, At(0.5f, 0.5f), 0.07f * s);
            p.fillColor = Color.white;
            Disc(p, At(0.46f, 0.46f), 0.025f * s);
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

        private static void Tri(Painter2D p, Vector2 a, Vector2 b, Vector2 c)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.LineTo(c);
            p.ClosePath();
            p.Fill();
        }

        private static void Disc(Painter2D p, Vector2 center, float radius)
        {
            p.BeginPath();
            p.Arc(center, radius, 0, 360);
            p.ClosePath();
            p.Fill();
        }

        // an ellipse from four curves
        private static void Ellipse(Painter2D p, Vector2 c, float rx, float ry)
        {
            const float k = 0.5523f;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x + rx, c.y));
            p.BezierCurveTo(new Vector2(c.x + rx, c.y + ry * k), new Vector2(c.x + rx * k, c.y + ry), new Vector2(c.x, c.y + ry));
            p.BezierCurveTo(new Vector2(c.x - rx * k, c.y + ry), new Vector2(c.x - rx, c.y + ry * k), new Vector2(c.x - rx, c.y));
            p.BezierCurveTo(new Vector2(c.x - rx, c.y - ry * k), new Vector2(c.x - rx * k, c.y - ry), new Vector2(c.x, c.y - ry));
            p.BezierCurveTo(new Vector2(c.x + rx * k, c.y - ry), new Vector2(c.x + rx, c.y - ry * k), new Vector2(c.x + rx, c.y));
            p.ClosePath();
            p.Fill();
        }
    }
}
