using O5Kit.Control;
using O5Kit.Factory;
using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using System;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Utility;

// Quieter look for the HUD / curve editors on top of O5: ghost buttons (accent only on
// hover / primary actions), small caption-style section labels, hairline separators.
internal static class UiStyle {
    public static Color Surface => UIColors.Surface;
    public static readonly Color Ghost = new(1f, 1f, 1f, 0.05f);
    public static readonly Color Line = new(1f, 1f, 1f, 0.08f);
    public static readonly Color Caption = new(1f, 1f, 1f, 0.42f);
    public static readonly Color Muted = new(1f, 1f, 1f, 0.6f);
    public static readonly Color Danger = new(1f, 0.42f, 0.45f, 1f);

    public static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    public static Image Rounded(string name, Transform parent, Color color) {
        var img = NewRect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = MainCore.Spr.Get(UISliceSprite.Circle128P512);
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    /// <summary>Button with an almost invisible background; text left/center, optional icon.</summary>
    public static O5Button Button(Transform parent, string id, string text, Action onClick, bool primary = false,
        TextAlignmentOptions align = TextAlignmentOptions.Center, float fontSize = 18f) {
        var button = O5Factory.Button(O5KitAdapters.Ctx, parent, id, new O5ButtonOptions { Text = text, OnClick = onClick });
        if (!primary) {
            button.NormalColor = Ghost;
            button.UpdateVisual(true);
        }
        if (button.Label != null) {
            button.Label.fontSize = fontSize;
            button.Label.alignment = align;
            if (align == TextAlignmentOptions.Left || align == TextAlignmentOptions.MidlineLeft) {
                button.Label.margin = new Vector4(12f, 0f, 8f, 0f);
            }
        }
        return button;
    }

    public static O5Button IconButton(Transform parent, string id, Sprite icon, Action onClick, float padding = 8f, bool primary = false) {
        var button = O5Factory.Button(O5KitAdapters.Ctx, parent, id, new O5ButtonOptions { Icon = icon, IconPadding = padding, OnClick = onClick });
        if (!primary) {
            button.NormalColor = Ghost;
            button.UpdateVisual(true);
        }
        return button;
    }

    public static void Tooltip(O5Kit.Core.O5Object control, string text) {
        if (!string.IsNullOrEmpty(text)) {
            control.Rect.AddToolTip(O5KitAdapters.Ctx, text);
        }
    }

    public static void Tooltip(O5Kit.Core.O5Object control, Func<string> text) {
        if (text != null) {
            control.Rect.AddToolTip(O5KitAdapters.Ctx, text);
        }
    }

    /// <summary>Small grey caption above a group of controls.</summary>
    public static TextMeshProUGUI Section(Transform parent, string text) {
        var row = O5Factory.Row(O5KitAdapters.Ctx, parent, 22f);
        var label = O5Factory.ControlText(O5KitAdapters.Ctx, row, 13f, true);
        label.font = O5KitAdapters.Ctx.Fonts.Medium;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(4f, 0f);
        label.rectTransform.offsetMax = Vector2.zero;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.characterSpacing = 6f;
        label.color = Caption;
        label.raycastTarget = false;
        label.text = text.ToUpperInvariant();
        return label;
    }

    public static Image Separator(Transform parent, bool vertical = false) {
        var rect = NewRect("Separator", parent);
        var le = rect.gameObject.AddComponent<LayoutElement>();
        if (vertical) {
            le.preferredWidth = le.minWidth = 1f;
        } else {
            le.preferredHeight = le.minHeight = 1f;
        }
        var img = rect.gameObject.AddComponent<Image>();
        img.color = Line;
        img.raycastTarget = false;
        return img;
    }

    public static HorizontalLayoutGroup Horizontal(GameObject go, float spacing, RectOffset padding = null) {
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;
        return layout;
    }

    public static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding = null) {
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    /// <summary>Row inside a vertical layout that lays its children out horizontally.</summary>
    public static RectTransform HRow(Transform parent, float spacing = 6f, float? height = null) {
        var row = O5Factory.Row(O5KitAdapters.Ctx, parent, height);
        // Otherwise the layout group's flexible height wins and the row eats free space.
        row.GetComponent<LayoutElement>().flexibleHeight = 0f;
        Horizontal(row.gameObject, spacing);
        return row;
    }

    /// <summary>
    /// A row of tab buttons with one content group per tab (built right under the bar).
    /// Used to keep advanced options out of the way instead of a global "advanced mode".
    /// </summary>
    public sealed class Tabs {
        private readonly List<O5Button> buttons = [];
        private readonly List<RectTransform> pages = [];
        public int Current { get; private set; } = -1;
        public Action<int> Changed;

        public Tabs(Transform parent, string id, params string[] titles) {
            var bar = HRow(parent, 4f, 36f);
            for (int i = 0; i < titles.Length; i++) {
                int index = i;
                buttons.Add(Button(Cell(bar, 130f), $"{id}_tab_{i}", titles[i], () => Select(index)));
            }
            Cell(bar, 0f);
            foreach (var _ in titles) {
                var page = NewRect("TabPage", parent);
                page.gameObject.AddComponent<LayoutElement>().flexibleHeight = 0f;
                Vertical(page.gameObject, 12f);
                pages.Add(page);
            }
            Select(0);
        }

        public RectTransform this[int index] => pages[index];
        public O5Button TabButton(int index) => buttons[index];

        public void Select(int index) {
            if (index == Current) {
                return;
            }
            Current = index;
            for (int i = 0; i < pages.Count; i++) {
                pages[i].gameObject.SetActive(i == index);
                buttons[i].NormalColor = i == index ? O5KitAdapters.Ctx.Theme.ObjectButton : Ghost;
                buttons[i].UpdateVisual(true);
            }
            Changed?.Invoke(index);
        }
    }

    /// <summary>Layout slot: fixed width, or flexible when width is 0.</summary>
    public static RectTransform Cell(Transform parent, float width) {
        var rect = NewRect("Cell", parent);
        var le = rect.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
        le.flexibleWidth = width <= 0f ? 1f : 0f;
        return rect;
    }
}
