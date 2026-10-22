using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    public enum Weight { Regular, Medium, Bold, Heavy }

    public enum ButtonKind { Normal, Primary, Gold, Ghost }

    // small helpers for building UI Toolkit elements in code: fonts, colors, buttons and tweens
    public static class Ui
    {
        public static readonly Color Felt = new Color(0.04f, 0.25f, 0.16f);
        public static readonly Color FeltDark = new Color(0.02f, 0.13f, 0.08f);
        public static readonly Color Ink = new Color(0.08f, 0.06f, 0.04f);
        public static readonly Color Gold = new Color(0.98f, 0.79f, 0.32f);
        public static readonly Color Cream = new Color(0.98f, 0.95f, 0.87f);
        public static readonly Color Dim = new Color(0.86f, 0.9f, 0.84f, 0.68f);
        public static readonly Color Chips = new Color(0.33f, 0.66f, 1f);
        public static readonly Color Mult = new Color(1f, 0.34f, 0.3f);
        public static readonly Color HeatColor = new Color(1f, 0.55f, 0.2f);
        public static readonly Color Panel = new Color(0.01f, 0.04f, 0.03f, 0.55f);
        public static readonly Color PanelSolid = new Color(0.05f, 0.08f, 0.07f, 0.97f);
        public static readonly Color Hairline = new Color(0.98f, 0.79f, 0.32f, 0.22f);
        public static readonly Color Good = new Color(0.42f, 0.9f, 0.5f);
        public static readonly Color Bad = new Color(1f, 0.43f, 0.38f);
        public static readonly Color Cocktail = new Color(0.78f, 0.5f, 1f);

        private static FontDefinition? _display, _regular, _medium, _bold, _heavy;

        public static FontDefinition DisplayFont => _display ??= Load("Limelight-Regular");

        public static FontDefinition FontOf(Weight weight) => weight switch
        {
            Weight.Medium => _medium ??= Load("FiraSans-Medium"),
            Weight.Bold => _bold ??= Load("FiraSans-Bold"),
            Weight.Heavy => _heavy ??= Load("FiraSans-ExtraBold"),
            _ => _regular ??= Load("FiraSans-Regular"),
        };

        private static FontDefinition Load(string name)
        {
            var font = Resources.Load<Font>("Fonts/" + name);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return FontDefinition.FromFont(font);
        }

        public static UIDocument CreateDocument(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(parent, false);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.match = 0.5f;
            settings.sortingOrder = sortingOrder;

            var document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            go.SetActive(true);

            var root = document.rootVisualElement;
            // no theme = root doesn't fill the screen on its own
            Fill(root);
            root.pickingMode = PickingMode.Ignore;
            root.style.color = Cream;
            root.style.fontSize = 16;
            root.style.unityFontDefinition = FontOf(Weight.Regular);
            return document;
        }

        public static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = e.style.right = e.style.top = e.style.bottom = 0;
        }

        // style.gap isn't available in this UI Toolkit version, so containers made here remember
        // their gap and Add puts it in front of every child after the first
        private sealed class Gap
        {
            public float Size;
            public bool Horizontal;
        }

        public static VisualElement Row(VisualElement parent, float gap = 0)
        {
            var row = new VisualElement { userData = new Gap { Size = gap, Horizontal = true } };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            Add(parent, row);
            return row;
        }

        public static VisualElement Column(VisualElement parent, float gap = 0)
        {
            var column = new VisualElement { userData = new Gap { Size = gap, Horizontal = false } };
            column.style.flexDirection = FlexDirection.Column;
            Add(parent, column);
            return column;
        }

        public static T Add<T>(VisualElement parent, T child) where T : VisualElement
        {
            if (parent == null) return child;
            if (parent.userData is Gap gap && gap.Size > 0 && parent.childCount > 0)
            {
                if (gap.Horizontal) child.style.marginLeft = gap.Size;
                else child.style.marginTop = gap.Size;
            }
            parent.Add(child);
            return child;
        }

        public static Label Text(VisualElement parent, string text, int size, Color color, bool bold = false) =>
            Text(parent, text, size, color, bold ? Weight.Bold : Weight.Regular);

        public static Label Text(VisualElement parent, string text, int size, Color color, Weight weight)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            label.style.unityFontDefinition = FontOf(weight);
            label.enableRichText = true;
            Add(parent, label);
            return label;
        }

        // wrapping body text with the game's words colored in
        public static Label Body(VisualElement parent, string text, int size, Color color, bool center = false)
        {
            var label = Text(parent, Rich(text), size, color);
            label.style.whiteSpace = WhiteSpace.Normal;
            if (center) label.style.unityTextAlign = TextAnchor.UpperCenter;
            return label;
        }

        // the art-deco display face, for titles
        public static Label Title(VisualElement parent, string text, int size, Color color, bool glow = true)
        {
            var label = Text(parent, text, size, color);
            label.style.unityFontDefinition = DisplayFont;
            label.style.letterSpacing = size / 12f;
            if (glow) label.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 12, color = new Color(0, 0, 0, 0.8f) };
            return label;
        }

        // a tiny spaced-out heading like "YOUR VICES"
        public static Label Caption(VisualElement parent, string text, Color? color = null)
        {
            var label = Text(parent, text, 11, color ?? Dim, Weight.Bold);
            label.style.letterSpacing = 1.6f;
            return label;
        }

        public static void Rounded(VisualElement e, float radius)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius =
                e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }

        public static void Border(VisualElement e, float width, Color color)
        {
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
            BorderColor(e, color);
        }

        public static void BorderColor(VisualElement e, Color color) =>
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = color;

        public static void Padding(VisualElement e, float vertical, float horizontal)
        {
            e.style.paddingTop = e.style.paddingBottom = vertical;
            e.style.paddingLeft = e.style.paddingRight = horizontal;
        }

        public static void Margin(VisualElement e, float all) =>
            e.style.marginLeft = e.style.marginRight = e.style.marginTop = e.style.marginBottom = all;

        // a dark glass panel with a thin gold edge
        public static VisualElement Glass(VisualElement parent, float gap = 0, bool row = false, float radius = 12)
        {
            var panel = row ? Row(parent, gap) : Column(parent, gap);
            panel.style.backgroundColor = Panel;
            Rounded(panel, radius);
            Border(panel, 1, Hairline);
            return panel;
        }

        // a small colored tag in a rounded box
        public static Label Pill(VisualElement parent, string text, Color color, bool filled = false, int size = 11)
        {
            var pill = Text(parent, text, size, filled ? Ink : color, Weight.Bold);
            pill.style.letterSpacing = 0.8f;
            Padding(pill, 2, 7);
            Rounded(pill, 9);
            pill.style.backgroundColor = filled ? color : new Color(color.r, color.g, color.b, 0.14f);
            Border(pill, 1, new Color(color.r, color.g, color.b, filled ? 1f : 0.45f));
            pill.style.flexShrink = 0;
            return pill;
        }

        public static Label Keycap(VisualElement parent, string key, bool dark = false)
        {
            var cap = Text(parent, key, 10, dark ? new Color(0, 0, 0, 0.7f) : new Color(1, 1, 1, 0.8f), Weight.Bold);
            cap.style.minWidth = 18;
            cap.style.height = 18;
            cap.style.unityTextAlign = TextAnchor.MiddleCenter;
            Padding(cap, 0, 4);
            Rounded(cap, 4);
            cap.style.backgroundColor = dark ? new Color(0, 0, 0, 0.1f) : new Color(1, 1, 1, 0.1f);
            Border(cap, 1, dark ? new Color(0, 0, 0, 0.3f) : new Color(1, 1, 1, 0.28f));
            cap.style.borderBottomWidth = 2;
            cap.style.flexShrink = 0;
            cap.pickingMode = PickingMode.Ignore;
            return cap;
        }

        // ---- buttons ----

        private sealed class ButtonParts
        {
            public Label Text;
            public VisualElement Sheen;
        }

        public static Button Button(VisualElement parent, string text, Action onClick, bool accent = false, string key = null) =>
            Button(parent, text, onClick, accent ? ButtonKind.Primary : ButtonKind.Normal, key);

        public static Button Button(VisualElement parent, string text, Action onClick, ButtonKind kind, string key = null)
        {
            var button = new Button(onClick) { text = "" };
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.height = 44;
            button.style.minWidth = 92;
            Margin(button, 0);
            Padding(button, 0, 16);
            Rounded(button, 10);
            button.style.overflow = Overflow.Hidden;

            var parts = new ButtonParts();
            button.userData = parts;
            parts.Sheen = new VisualElement { pickingMode = PickingMode.Ignore };
            Fill(parts.Sheen);
            parts.Sheen.style.backgroundColor = Color.white;
            parts.Sheen.style.opacity = 0;
            Transition(parts.Sheen, 0.15f, "opacity");
            button.Add(parts.Sheen);

            parts.Text = Text(button, text, 16, Cream, Weight.Bold);
            parts.Text.pickingMode = PickingMode.Ignore;
            parts.Text.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (key != null) Keycap(button, key, kind == ButtonKind.Gold).style.marginLeft = 8;
            Paint(button, kind);

            Transition(button, 0.14f, "scale", "opacity");
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (!button.enabledInHierarchy) return;
                parts.Sheen.style.opacity = 0.12f;
                button.style.scale = new Scale(new Vector2(1.04f, 1.04f));
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                parts.Sheen.style.opacity = 0;
                button.style.scale = new Scale(Vector2.one);
            });
            button.RegisterCallback<PointerDownEvent>(_ => button.style.scale = new Scale(new Vector2(0.95f, 0.95f)), TrickleDown.TrickleDown);
            button.RegisterCallback<PointerUpEvent>(_ => button.style.scale = new Scale(new Vector2(1.04f, 1.04f)), TrickleDown.TrickleDown);
            Add(parent, button);
            return button;
        }

        public static void Paint(Button button, ButtonKind kind)
        {
            var (top, bottom, edge) = kind switch
            {
                ButtonKind.Primary => (new Color(0.88f, 0.22f, 0.21f), new Color(0.56f, 0.07f, 0.1f), new Color(1f, 0.6f, 0.5f, 0.6f)),
                ButtonKind.Gold => (new Color(1f, 0.9f, 0.5f), new Color(0.86f, 0.6f, 0.16f), new Color(1f, 0.96f, 0.75f, 0.95f)),
                ButtonKind.Ghost => (new Color(1, 1, 1, 0.06f), new Color(1, 1, 1, 0.02f), new Color(1, 1, 1, 0.22f)),
                _ => (new Color(0.22f, 0.25f, 0.24f), new Color(0.1f, 0.12f, 0.12f), new Color(1, 1, 1, 0.18f)),
            };
            button.style.backgroundColor = bottom;
            button.style.backgroundImage = Gradient(top, bottom);
            button.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Border(button, 1, edge);
            button.style.borderBottomWidth = 3;
            button.style.borderBottomColor = new Color(0, 0, 0, 0.45f);
            if (button.userData is ButtonParts parts) parts.Text.style.color = kind == ButtonKind.Gold ? Ink : Cream;
        }

        public static void SetText(Button button, string text)
        {
            if (button.userData is ButtonParts parts) parts.Text.text = text;
            else button.text = text;
        }

        public static void SetEnabled(Button button, bool enabled)
        {
            button.SetEnabled(enabled);
            button.style.opacity = enabled ? 1f : 0.36f;
            if (enabled) return;
            button.style.scale = new Scale(Vector2.one);
            if (button.userData is ButtonParts parts) parts.Sheen.style.opacity = 0;
        }

        public static void Show(VisualElement e, bool visible) => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public static bool IsShown(VisualElement e) => e.style.display != DisplayStyle.None;

        // ---- motion ----

        public static void Transition(VisualElement e, float seconds, params string[] properties) =>
            Transition(e, seconds, EasingMode.EaseOutCubic, properties);

        public static void Transition(VisualElement e, float seconds, EasingMode easing, params string[] properties)
        {
            var names = new List<StylePropertyName>();
            var durations = new List<TimeValue>();
            var curves = new List<EasingFunction>();
            foreach (var property in properties)
            {
                names.Add(property);
                durations.Add(new TimeValue(seconds));
                curves.Add(new EasingFunction(easing));
            }
            e.style.transitionProperty = names;
            e.style.transitionDuration = durations;
            e.style.transitionTimingFunction = curves;
        }

        // fade and slide something in after a delay
        public static void Enter(VisualElement e, float fromY = 18, long delayMs = 0, float seconds = 0.3f)
        {
            Transition(e, 0, "opacity", "translate");
            e.style.opacity = 0;
            e.style.translate = new Translate(0, fromY);
            e.schedule.Execute(() =>
            {
                Transition(e, seconds, "opacity", "translate");
                e.style.opacity = 1;
                e.style.translate = new Translate(0, 0);
            }).StartingIn(delayMs + 30);
        }

        // rises a little under the mouse, for cards and tiles
        public static void Lift(VisualElement e, float scale = 1.04f)
        {
            Transition(e, 0.18f, "scale", "translate");
            e.RegisterCallback<PointerEnterEvent>(_ =>
            {
                e.style.scale = new Scale(new Vector2(scale, scale));
                e.style.translate = new Translate(0, -4);
            });
            e.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                e.style.scale = new Scale(Vector2.one);
                e.style.translate = new Translate(0, 0);
            });
        }

        // a quick scale punch, for numbers and banners that just changed
        public static void Pop(VisualElement e, float scale = 1.25f, long delayMs = 0)
        {
            e.schedule.Execute(() =>
            {
                Transition(e, 0, "scale");
                e.style.scale = new Scale(new Vector2(scale, scale));
                e.schedule.Execute(() =>
                {
                    Transition(e, 0.35f, EasingMode.EaseOutBack, "scale");
                    e.style.scale = new Scale(Vector2.one);
                }).StartingIn(40);
            }).StartingIn(delayMs);
        }

        // ---- textures ----

        private static readonly Dictionary<(Color, Color), Texture2D> Gradients = new Dictionary<(Color, Color), Texture2D>();

        public static Texture2D Gradient(Color top, Color bottom)
        {
            if (Gradients.TryGetValue((top, bottom), out var cached)) return cached;
            var texture = new Texture2D(2, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 32; y++)
            {
                // y = 0 is the bottom row
                var c = Color.Lerp(bottom, top, y / 31f);
                texture.SetPixel(0, y, c);
                texture.SetPixel(1, y, c);
            }
            texture.Apply();
            Gradients[(top, bottom)] = texture;
            return texture;
        }

        // casino felt: fine grain and a dark vignette, made once
        private static Texture2D _felt;

        public static Texture2D FeltTexture()
        {
            if (_felt != null) return _felt;
            const int w = 640, h = 360;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[w * h];
            var random = new System.Random(11);
            var light = new Color(0.075f, 0.37f, 0.235f);
            var dark = new Color(0.008f, 0.06f, 0.04f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - w * 0.58f) / (w * 0.6f);
                    float dy = (y - h * 0.55f) / (h * 0.8f);
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    var c = Color.Lerp(light, dark, Mathf.SmoothStep(0, 1, d));
                    float grain = (float)(random.NextDouble() - 0.5) * 0.03f;
                    c.r += grain;
                    c.g += grain * 1.4f;
                    c.b += grain;
                    pixels[y * w + x] = c;
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return _felt = texture;
        }

        public static void Stretch(VisualElement e, Texture2D texture)
        {
            e.style.backgroundImage = texture;
            e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
        }

        // ---- text ----

        private static readonly Regex Keywords = new Regex(
            @"(?<mult>\bx\d+(?:\.\d+)?(?:\s?Mult)?|[+-]?\d+(?:\.\d+)?\s?Mult|\bMult\b)" +
            @"|(?<chips>[+-]?\d+\s?Chips|\bChips\b)" +
            @"|(?<gold>[+-]?\d+\s?(?:g\b|gold\b))" +
            @"|(?<heat>[+-]?\d+\s?Heat\b|\bHeat\b)" +
            @"|(?<bright>[+-]?\d+\s?(?:Hands?|Burns?)\b|\b(?:Hands?|Burns?)\b)");

        private static readonly string ChipsHex = Hex(Chips), MultHex = Hex(Mult), GoldHex = Hex(Gold), HeatHex = Hex(HeatColor);

        // colors the game's words: Chips blue, Mult red, gold yellow, Heat orange, Hands and Burns white
        public static string Rich(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return Keywords.Replace(text, m =>
            {
                string color = m.Groups["mult"].Success ? MultHex
                    : m.Groups["chips"].Success ? ChipsHex
                    : m.Groups["gold"].Success ? GoldHex
                    : m.Groups["heat"].Success ? HeatHex
                    : "FFFFFF";
                return $"<color=#{color}>{m.Value}</color>";
            });
        }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static string Colored(string text, Color c) => $"<color=#{Hex(c)}>{text}</color>";

        public static void Tip(VisualElement target, string title, string body = null) => Tooltip.Attach(target, title, body);
    }

    // hover tooltips. UI Toolkit's own tooltip property only shows in the editor, not in a build
    public static class Tooltip
    {
        private sealed class Content
        {
            public string Title, Body;
        }

        private static readonly ConditionalWeakTable<VisualElement, Content> Tips = new ConditionalWeakTable<VisualElement, Content>();
        private static VisualElement _root, _box, _target;
        private static Label _title, _body;

        public static void Create(VisualElement root)
        {
            _root = root;
            _box = Ui.Column(null, 4);
            _box.style.position = Position.Absolute;
            _box.style.maxWidth = 300;
            _box.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.97f);
            Ui.Rounded(_box, 10);
            Ui.Border(_box, 1, new Color(0.98f, 0.79f, 0.32f, 0.6f));
            Ui.Padding(_box, 9, 12);
            _box.pickingMode = PickingMode.Ignore;
            _title = Ui.Text(_box, "", 15, Ui.Gold, Weight.Bold);
            _title.style.whiteSpace = WhiteSpace.Normal;
            _body = Ui.Text(_box, "", 13, Ui.Cream);
            _body.style.whiteSpace = WhiteSpace.Normal;
            _title.pickingMode = _body.pickingMode = PickingMode.Ignore;
            Ui.Transition(_box, 0.12f, "opacity");
            Ui.Show(_box, false);
            root.Add(_box);
            _box.RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        public static void Attach(VisualElement target, string title, string body)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) return;
            bool first = !Tips.TryGetValue(target, out var content);
            if (first)
            {
                content = new Content();
                Tips.Add(target, content);
                if (target.pickingMode == PickingMode.Ignore) target.pickingMode = PickingMode.Position;
                target.RegisterCallback<PointerEnterEvent>(_ => Show(target));
                target.RegisterCallback<PointerLeaveEvent>(_ => Hide(target));
                target.RegisterCallback<DetachFromPanelEvent>(_ => Hide(target));
            }
            content.Title = title;
            content.Body = body;
            if (_target == target) Show(target);
        }

        private static void Show(VisualElement target)
        {
            if (_box == null || !Tips.TryGetValue(target, out var content)) return;
            _target = target;
            _title.text = content.Title ?? "";
            Ui.Show(_title, !string.IsNullOrEmpty(content.Title));
            _body.text = Ui.Rich(content.Body ?? "");
            Ui.Show(_body, !string.IsNullOrEmpty(content.Body));
            Ui.Show(_box, true);
            _box.BringToFront();
            _box.style.opacity = 0;
            _box.schedule.Execute(() => { if (_target == target) _box.style.opacity = 1; }).StartingIn(80);
            Place();
        }

        private static void Hide(VisualElement target)
        {
            if (_target != target || _box == null) return;
            _target = null;
            Ui.Show(_box, false);
        }

        public static void HideAll()
        {
            if (_target != null) Hide(_target);
        }

        // above the target if there's room, otherwise below. kept on screen
        private static void Place()
        {
            if (_target == null || _target.panel == null) return;
            var area = _root.layout;
            var bounds = _root.WorldToLocal(_target.worldBound);
            var size = _box.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0) return;
            float x = Mathf.Clamp(bounds.center.x - size.x / 2, 8, Mathf.Max(8, area.width - size.x - 8));
            float y = bounds.yMin - size.y - 8;
            if (y < 8) y = bounds.yMax + 8;
            _box.style.left = x;
            _box.style.top = Mathf.Clamp(y, 8, Mathf.Max(8, area.height - size.y - 8));
        }
    }

    // a number label that counts up or down to its value instead of jumping
    public sealed class Counter
    {
        private readonly Label _label;
        private readonly Func<long, string> _format;
        private double _shown, _from;
        private long _target;
        private float _t = 1;
        private bool _started;

        public Counter(Label label, Func<long, string> format)
        {
            _label = label;
            _format = format;
            label.schedule.Execute(Tick).Every(16);
        }

        public void Set(long value, bool animate = true)
        {
            if (_started && value == _target) return;
            bool up = _started && value > _target;
            animate &= _started;
            _started = true;
            _from = _shown;
            _target = value;
            _t = animate ? 0 : 1;
            if (!animate) _shown = value;
            _label.text = _format((long)Math.Round(_shown));
            if (animate && up) Ui.Pop(_label, 1.18f);
        }

        private void Tick()
        {
            if (_t >= 1) return;
            _t = Mathf.Min(1, _t + 0.016f / 0.6f);
            double eased = 1 - Math.Pow(1 - _t, 3);
            _shown = _t >= 1 ? _target : _from + (_target - _from) * eased;
            _label.text = _format((long)Math.Round(_shown));
        }
    }
}
