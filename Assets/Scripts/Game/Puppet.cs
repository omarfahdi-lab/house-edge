using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // a character as a framed painting: every boss, dealer and host is a public-domain masterpiece
    // (see docs/CREDITS.md). like a Balatro joker it never walks, but it's alive: it sways on its hook,
    // bobs when it talks, and reacts with a hop, a droop, a jolt or a shake, plus a little emote
    public sealed class Puppet : VisualElement
    {
        public enum Mood { Idle, Happy, Sad, Shocked, Angry }

        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Color FrameGold = new Color(0.86f, 0.66f, 0.26f), FrameDark = new Color(0.42f, 0.27f, 0.08f);

        private readonly VisualElement _frame, _image, _glow;
        private readonly Label _emote;
        private readonly bool _bust;
        private float _time, _talk, _moodTime;
        private Mood _mood;

        // hopping along, for the map
        public bool Walking;
        // kept for callers that flip characters; paintings face the way they were painted
        public float Facing = 1;

        public static string Id(string name)
        {
            var chars = new List<char>();
            foreach (char c in name.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) chars.Add(c);
            return new string(chars.ToArray());
        }

        // is there a painting for this character?
        public static bool Has(string name) => Texture(Id(name)) != null;

        // the preloader hands paintings over as they finish loading
        public static void Keep(string id, Texture2D texture)
        {
            if (texture != null) Textures[id] = texture;
        }

        // height of the framed painting; a bust fills a square of that size with just the painting
        public Puppet(string name, float height, bool bust = false)
        {
            _bust = bust;
            pickingMode = PickingMode.Ignore;
            float width = bust ? height : height * 0.8f;
            style.width = width;
            style.height = height;
            style.flexShrink = 0;
            var texture = Texture(Id(name));

            // a soft gold bloom behind the frame, for happy moments
            _glow = Layer(this, -10);
            Ui.Rounded(_glow, bust ? height / 2 : 14);
            _glow.style.backgroundColor = new Color(1f, 0.82f, 0.35f, 0.5f);
            _glow.style.opacity = 0;

            _frame = Layer(this, 0);
            _frame.style.transformOrigin = new TransformOrigin(Length.Percent(50), bust ? Length.Percent(50) : Length.Percent(0));
            if (bust)
            {
                _image = Layer(_frame, 0);
                _image.style.overflow = Overflow.Hidden;
            }
            else
            {
                // an ornate frame: dark shadow, gold moulding, a dark inner lip, then the canvas
                var shadow = Layer(_frame, 0);
                shadow.style.translate = new Translate(4, 7);
                shadow.style.backgroundColor = new Color(0, 0, 0, 0.45f);
                Ui.Rounded(shadow, 6);
                var moulding = Layer(_frame, 0);
                moulding.style.backgroundImage = Ui.Gradient(new Color(0.98f, 0.82f, 0.42f), FrameDark);
                moulding.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                Ui.Rounded(moulding, 6);
                Ui.Border(moulding, 2, new Color(0.3f, 0.18f, 0.04f));
                float rim = Mathf.Max(5, height * 0.045f);
                var lip = Layer(_frame, rim);
                lip.style.backgroundColor = new Color(0.16f, 0.1f, 0.04f);
                Ui.Border(lip, 2, FrameGold);
                _image = Layer(_frame, rim + 3);
                _image.style.overflow = Overflow.Hidden;
            }
            _image.style.backgroundImage = texture;
            _image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);

            _emote = Ui.Text(this, "", (int)Mathf.Clamp(height * 0.11f, 12, 30), Ui.Ink, Weight.Heavy);
            _emote.style.position = Position.Absolute;
            _emote.style.right = -height * 0.08f;
            _emote.style.top = -height * 0.06f;
            _emote.style.backgroundColor = Ui.Cream;
            Ui.Rounded(_emote, 10);
            Ui.Padding(_emote, 2, 8);
            Ui.Border(_emote, 2, Ui.Gold);
            _emote.pickingMode = PickingMode.Ignore;
            _emote.style.display = DisplayStyle.None;

            schedule.Execute(Tick).Every(16);
        }

        private static VisualElement Layer(VisualElement parent, float inset)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.right = layer.style.top = layer.style.bottom = inset;
            parent.Add(layer);
            return layer;
        }

        // a few seconds of talking
        public void Talk(float seconds = 1.6f) => _talk = Mathf.Max(_talk, seconds);

        // a reaction for a little while, then back to idle
        public void React(Mood mood, float seconds = 1.8f)
        {
            _mood = mood;
            _moodTime = seconds;
            string emote = mood switch
            {
                Mood.Happy => "HA!",
                Mood.Sad => "...",
                Mood.Shocked => "!?",
                Mood.Angry => "GRR",
                _ => null,
            };
            _emote.style.display = emote != null && !_bust ? DisplayStyle.Flex : DisplayStyle.None;
            if (emote != null)
            {
                _emote.text = emote;
                Ui.Pop(_emote, 1.4f);
            }
        }

        // hidden paintings (a closed screen) don't need to move
        private bool Visible()
        {
            if (panel == null) return false;
            for (VisualElement e = this; e != null; e = e.parent)
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.opacity <= 0.01f) return false;
            return true;
        }

        private void Tick()
        {
            if (!Visible()) return;
            float dt = 0.016f;
            _time += Settings.ReducedMotion ? 0 : dt;
            _talk = Mathf.Max(0, _talk - dt);
            if (_moodTime > 0)
            {
                _moodTime -= dt;
                if (_moodTime <= 0)
                {
                    _mood = Mood.Idle;
                    _emote.style.display = DisplayStyle.None;
                }
            }

            float t = _time;
            // hanging on its hook, swaying a touch
            float rotate = _bust ? 0 : 1.2f * Mathf.Sin(t * 0.9f);
            float lift = 0, scale = 1 + 0.006f * Mathf.Sin(t * 2.1f), shakeX = 0, glow = 0;
            var tint = Color.white;

            if (_talk > 0)
            {
                lift = -Mathf.Abs(Mathf.Sin(t * 9)) * 4;
                rotate += 1.5f * Mathf.Sin(t * 6);
            }
            if (Walking)
            {
                lift = -Mathf.Abs(Mathf.Sin(t * 10)) * 7;
                rotate += 4 * Mathf.Sin(t * 10);
            }
            switch (_mood)
            {
                case Mood.Happy:
                    lift = -Mathf.Abs(Mathf.Sin(t * 9)) * 14;
                    glow = 0.6f + 0.4f * Mathf.Sin(t * 12);
                    break;
                case Mood.Sad:
                    rotate = 6;
                    lift = 5;
                    tint = new Color(0.62f, 0.66f, 0.72f);
                    break;
                case Mood.Shocked:
                    scale = 1.08f + 0.03f * Mathf.Sin(t * 30);
                    lift = -6;
                    break;
                case Mood.Angry:
                    shakeX = 5 * Mathf.Sin(t * 55);
                    rotate += 2 * Mathf.Sin(t * 37);
                    tint = new Color(1f, 0.62f, 0.58f);
                    break;
            }

            _frame.style.rotate = new Rotate(rotate);
            _frame.style.translate = new Translate(shakeX, lift);
            _frame.style.scale = new Scale(new Vector2(scale, scale));
            _image.style.unityBackgroundImageTintColor = tint;
            _glow.style.opacity = glow;
            _glow.style.translate = new Translate(shakeX, lift);
        }

        private static Texture2D Texture(string id)
        {
            if (Textures.TryGetValue(id, out var texture)) return texture;
            texture = Resources.Load<Texture2D>("Portraits/" + id);
            Textures[id] = texture;
            return texture;
        }
    }
}
