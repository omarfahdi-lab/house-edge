using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // a character built from Kenney's Modular Characters (CC0), baked into layers by the art tools: legs, arms,
    // torso and head, with a mouth that changes. it breathes, looks around, talks with its hands, walks, and
    // reacts: cheering, slumping, gasping or getting angry
    public sealed class Puppet : VisualElement
    {
        public enum Mood { Idle, Happy, Sad, Shocked, Angry }

        private sealed class Rig
        {
            public VisualElement Root, LegL, LegR, ArmL, ArmR, Torso, Head, Mouth;
            public string RestMouth, CurrentMouth;
            public float Phase;
        }

        private static readonly Dictionary<string, Dictionary<string, string[]>> Meta = new Dictionary<string, Dictionary<string, string[]>>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        private readonly List<Rig> _rigs = new List<Rig>();
        private readonly float _k;
        private float _time, _talk, _moodTime;
        private Mood _mood;

        // walking: legs and arms swing and the body bobs
        public bool Walking;
        // which way it faces: 1 right (as drawn), -1 flipped
        public float Facing = 1;

        public static string Id(string name)
        {
            var chars = new List<char>();
            foreach (char c in name.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) chars.Add(c);
            return new string(chars.ToArray());
        }

        // is there baked art for this character?
        public static bool Has(string name) => Load(Id(name)) != null;

        // height is the full figure's height; bust frames just the head and shoulders in a square of that size
        public Puppet(string name, float height, bool bust = false)
        {
            pickingMode = PickingMode.Ignore;
            string id = Id(name);
            var meta = Load(id);
            float baseHeight = meta != null ? Number(meta["size"][1]) : 320;
            float baseWidth = meta != null ? Number(meta["size"][0]) : 210;
            bool twins = name == "The Twins";
            // a bust zooms in on the top of the figure
            float zoom = bust ? 2.05f : 1;
            _k = height / baseHeight * zoom * (twins ? 0.82f : 1);
            style.width = bust ? height : baseWidth * _k * (twins ? 1.6f : 1);
            style.height = height;
            style.flexShrink = 0;
            if (bust) style.overflow = Overflow.Hidden;
            if (meta == null) return;

            int count = twins ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                var rig = Build(id, meta);
                float width = baseWidth * _k;
                float left = bust ? (height - width) / 2 : 0;
                if (twins) left += (i == 0 ? -0.27f : 0.27f) * width + (bust ? 0 : 0.3f * width);
                rig.Root.style.left = left;
                rig.Root.style.top = bust ? -height * 0.04f : 0;
                rig.Phase = i * 1.7f;
                Add(rig.Root);
                _rigs.Add(rig);
            }
            schedule.Execute(Tick).Every(16);
        }

        // a few seconds of talking with the hands
        public void Talk(float seconds = 1.6f) => _talk = Mathf.Max(_talk, seconds);

        // a reaction for a little while, then back to idle
        public void React(Mood mood, float seconds = 1.8f)
        {
            _mood = mood;
            _moodTime = seconds;
            string mouth = mood switch
            {
                Mood.Happy => "teethUpper",
                Mood.Sad => "sad",
                Mood.Shocked => "oh",
                Mood.Angry => "teethLower",
                _ => null,
            };
            foreach (var rig in _rigs) SetMouth(rig, mouth ?? rig.RestMouth);
        }

        private Rig Build(string id, Dictionary<string, string[]> meta)
        {
            var rig = new Rig { Root = new VisualElement { pickingMode = PickingMode.Ignore } };
            rig.Root.style.position = Position.Absolute;
            rig.Root.style.width = Number(meta["size"][0]) * _k;
            rig.Root.style.height = Number(meta["size"][1]) * _k;
            rig.LegL = Layer(rig.Root, id, "legL", meta);
            rig.LegR = Layer(rig.Root, id, "legR", meta);
            rig.ArmL = Layer(rig.Root, id, "armL", meta);
            rig.ArmR = Layer(rig.Root, id, "armR", meta);
            rig.Torso = Layer(rig.Root, id, "torso", meta);
            rig.Head = Layer(rig.Root, id, "head", meta);

            var mouth = meta["mouth"];
            rig.RestMouth = mouth[2] == "None" ? null : mouth[2];
            rig.Mouth = new VisualElement { pickingMode = PickingMode.Ignore };
            rig.Mouth.style.position = Position.Absolute;
            var head = meta["head"];
            // the mouth sits on the head, so it moves with it
            rig.Mouth.style.left = (Number(mouth[0]) - Number(head[0])) * _k;
            rig.Mouth.style.top = (Number(mouth[1]) - Number(head[1])) * _k;
            rig.Head.Add(rig.Mouth);
            SetMouth(rig, rig.RestMouth);
            return rig;
        }

        private VisualElement Layer(VisualElement parent, string id, string key, Dictionary<string, string[]> meta)
        {
            var m = meta[key];
            float x = Number(m[0]), y = Number(m[1]), w = Number(m[2]), h = Number(m[3]), px = Number(m[4]), py = Number(m[5]);
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = x * _k;
            layer.style.top = y * _k;
            layer.style.width = w * _k;
            layer.style.height = h * _k;
            layer.style.backgroundImage = Texture($"{id}_{key}");
            layer.style.transformOrigin = new TransformOrigin((px - x) * _k, (py - y) * _k);
            parent.Add(layer);
            return layer;
        }

        private void SetMouth(Rig rig, string mouth)
        {
            if (mouth == rig.CurrentMouth && rig.CurrentMouth != null) return;
            rig.CurrentMouth = mouth;
            if (mouth == null)
            {
                rig.Mouth.style.display = DisplayStyle.None;
                return;
            }
            var texture = Texture("mouth_" + mouth);
            if (texture == null) return;
            rig.Mouth.style.display = DisplayStyle.Flex;
            rig.Mouth.style.backgroundImage = texture;
            float w = texture.width * _k, h = texture.height * _k;
            rig.Mouth.style.width = w;
            rig.Mouth.style.height = h;
            rig.Mouth.style.translate = new Translate(-w / 2, -h / 2);
        }

        // hidden puppets (a closed screen, a scrolled-away list) don't need to move
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
            bool still = Settings.ReducedMotion;
            _time += still ? 0 : dt;
            _talk = Mathf.Max(0, _talk - dt);
            if (_moodTime > 0)
            {
                _moodTime -= dt;
                if (_moodTime <= 0)
                {
                    _mood = Mood.Idle;
                    foreach (var r in _rigs) SetMouth(r, r.RestMouth);
                }
            }

            foreach (var rig in _rigs)
            {
                float t = _time + rig.Phase;
                float breathe = Mathf.Sin(t * 2.2f);
                float armL = 3 * Mathf.Sin(t * 1.9f), armR = -3 * Mathf.Sin(t * 1.9f + 0.6f);
                float legL = 0, legR = 0, headTilt = 2.5f * Mathf.Sin(t * 1.1f), bob = breathe * 1.2f;

                if (Walking)
                {
                    float step = Mathf.Sin(t * 10);
                    legL = 22 * step;
                    legR = -22 * step;
                    armL = -18 * step;
                    armR = 18 * step;
                    bob = -Mathf.Abs(step) * 5;
                }
                if (_talk > 0)
                {
                    // the right hand gestures and the mouth flaps
                    armR = -38 + 14 * Mathf.Sin(t * 7);
                    headTilt += 3 * Mathf.Sin(t * 5);
                    bool open = Mathf.Sin(t * 14) > 0;
                    if (_mood == Mood.Idle) SetMouth(rig, open ? "oh" : rig.RestMouth);
                }
                else if (_mood == Mood.Idle) SetMouth(rig, rig.RestMouth);

                switch (_mood)
                {
                    case Mood.Happy:
                        // arms up, hopping
                        armL = 120 + 15 * Mathf.Sin(t * 12);
                        armR = -120 - 15 * Mathf.Sin(t * 12);
                        bob = -Mathf.Abs(Mathf.Sin(t * 9)) * 14;
                        break;
                    case Mood.Sad:
                        headTilt = 10;
                        armL = -4;
                        armR = 4;
                        bob = 6;
                        break;
                    case Mood.Shocked:
                        armL = 60;
                        armR = -60;
                        bob = -8;
                        break;
                    case Mood.Angry:
                        armL = 70 + 10 * Mathf.Sin(t * 20);
                        armR = -70 - 10 * Mathf.Sin(t * 20);
                        headTilt = 4 * Mathf.Sin(t * 30);
                        break;
                }

                rig.LegL.style.rotate = new Rotate(legL);
                rig.LegR.style.rotate = new Rotate(legR);
                rig.ArmL.style.rotate = new Rotate(armL);
                rig.ArmR.style.rotate = new Rotate(armR);
                rig.Torso.style.scale = new Scale(new Vector2(1, 1 + 0.012f * breathe));
                rig.Head.style.rotate = new Rotate(headTilt);
                rig.Head.style.translate = new Translate(0, -breathe * 1.2f * _k);
                rig.Root.style.translate = new Translate(0, bob * _k * 2);
                rig.Root.style.scale = new Scale(new Vector2(Facing, 1));
                var tint = _mood == Mood.Angry ? new Color(1f, 0.72f, 0.7f) : Color.white;
                rig.Head.style.unityBackgroundImageTintColor = tint;
            }
        }

        // the layer metadata: "key x y w h pivotX pivotY", "mouth x y kind", "size w h"
        private static Dictionary<string, string[]> Load(string id)
        {
            if (Meta.TryGetValue(id, out var cached)) return cached;
            var asset = Resources.Load<TextAsset>("Characters/" + id);
            Dictionary<string, string[]> meta = null;
            if (asset != null)
            {
                meta = new Dictionary<string, string[]>();
                foreach (var line in asset.text.Split('\n'))
                {
                    var parts = line.Trim().Split(' ');
                    if (parts.Length < 2) continue;
                    var values = new string[parts.Length - 1];
                    System.Array.Copy(parts, 1, values, 0, values.Length);
                    meta[parts[0]] = values;
                }
            }
            Meta[id] = meta;
            return meta;
        }

        // the preloader hands textures over as they finish loading
        public static void Keep(string name, Texture2D texture)
        {
            if (texture != null) Textures[name] = texture;
        }

        private static Texture2D Texture(string name)
        {
            if (Textures.TryGetValue(name, out var texture)) return texture;
            texture = Resources.Load<Texture2D>("Characters/" + name);
            Textures[name] = texture;
            return texture;
        }

        private static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);
    }
}
