using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HouseEdge.Game
{
    // small helpers for building UI Toolkit elements in code
    public static class Ui
    {
        public static readonly Color Felt = new Color(0.05f, 0.27f, 0.17f);
        public static readonly Color FeltDark = new Color(0.03f, 0.18f, 0.11f);
        public static readonly Color Gold = new Color(0.98f, 0.79f, 0.32f);
        public static readonly Color Cream = new Color(0.98f, 0.95f, 0.87f);
        public static readonly Color Dim = new Color(0.86f, 0.9f, 0.84f, 0.7f);
        public static readonly Color Chips = new Color(0.36f, 0.68f, 1f);
        public static readonly Color Mult = new Color(1f, 0.38f, 0.33f);
        public static readonly Color Panel = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color ButtonFace = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        public static readonly Color ButtonAccent = new Color(0.72f, 0.16f, 0.16f);
        public static readonly Color Good = new Color(0.45f, 0.9f, 0.5f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.4f);

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
            root.style.fontSize = 18;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) root.style.unityFontDefinition = FontDefinition.FromFont(font);
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

        public static Label Text(VisualElement parent, string text, int size, Color color, bool bold = false)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            if (bold) label.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(parent, label);
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
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = color;
        }

        public static void Padding(VisualElement e, float vertical, float horizontal)
        {
            e.style.paddingTop = e.style.paddingBottom = vertical;
            e.style.paddingLeft = e.style.paddingRight = horizontal;
        }

        public static Button Button(VisualElement parent, string text, Action onClick, bool accent = false)
        {
            var button = new Button(onClick) { text = text };
            button.style.backgroundColor = accent ? ButtonAccent : ButtonFace;
            button.style.color = Cream;
            button.style.fontSize = 17;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.height = 46;
            button.style.minWidth = 96;
            Padding(button, 0, 14);
            Rounded(button, 8);
            Border(button, 2, new Color(1f, 1f, 1f, 0.18f));
            Add(parent, button);
            return button;
        }

        public static void SetEnabled(Button button, bool enabled)
        {
            button.SetEnabled(enabled);
            button.style.opacity = enabled ? 1f : 0.35f;
        }

        public static void Show(VisualElement e, bool visible) => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
