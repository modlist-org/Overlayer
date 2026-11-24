using Overlayer.Compat;
using Overlayer.Core;
using Overlayer.Resource;
using O5Kit.Control;
using O5Kit.Factory;
using UnityEngine;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Utility;

/// <summary>Collapsible folder row used by list pages (Resources, JS).</summary>
internal static class FolderHeader {
    public static O5Button Create(Transform parent, string name, int count, bool collapsed, Action onToggle, string id) {
        O5Button button = O5Factory.Button(O5KitAdapters.Ctx, parent, id, new O5ButtonOptions { Text = $"{name}  <alpha=#80>({count})", OnClick = onToggle, Height = 40f });
        button.Label.alignment = TextAlignmentOptions.Left;
        button.Label.fontSize = 18f;
        button.Label.rectTransform.offsetMin = new Vector2(34f, 0f);

        var foldout = MainCore.Icons.Create(button.Rect, "Foldout", UIIcon.Triangle, Color.white, 12f);
        RectTransform foldoutRect = foldout.rectTransform;
        foldoutRect.anchorMin = new Vector2(0f, 0.5f);
        foldoutRect.anchorMax = new Vector2(0f, 0.5f);
        foldoutRect.anchoredPosition = new Vector2(18f, 0f);
        foldoutRect.sizeDelta = new Vector2(12f, 12f);
        foldoutRect.localRotation = Quaternion.Euler(0f, 0f, collapsed ? 0f : 180f);
        return button;
    }
}
