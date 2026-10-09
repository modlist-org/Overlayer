using Newtonsoft.Json.Linq;
using O5Kit.Input;
using Overlayer.IO.Interface;
using Overlayer.IO.Overlay;
using Overlayer.IO.UnityComponent;
using Overlayer.IO.UnityComponent.Impl;
using Overlayer.Overlay;
using UnityEngine;
using UnityEngine.EventSystems;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Overlay;

// Ctrl/Cmd+C, Ctrl/Cmd+V in the canvas editor.
// Hovering a component card copies that component; otherwise the selected object (with children) is copied.
// Paste puts an object after the selection, or adds/replaces the copied component on the selected object.
public partial class OvCanvasSettingPage {
    // Shared across canvases so objects/components can move between them.
    private static JToken copiedObject;
    private static Type copiedComponentType;
    private static JToken copiedComponent;
    private static JToken copiedTextEngine;

    private readonly System.Collections.Generic.List<(RectTransform Card, UnityComponentSettingsBase Settings)> componentCards = [];

    internal void Tick() {
        if (!GameObject.activeInHierarchy || !CanvasGroup.blocksRaycasts || currentCanvas == null || IsTyping()) {
            return;
        }
        if (!O5Input.GetKey(KeyCode.LeftControl) && !O5Input.GetKey(KeyCode.RightControl)
            && !O5Input.GetKey(KeyCode.LeftCommand) && !O5Input.GetKey(KeyCode.RightCommand)) {
            return;
        }

        if (O5Input.GetKeyDown(KeyCode.C)) {
            Copy();
        } else if (O5Input.GetKeyDown(KeyCode.V)) {
            Paste();
        }
    }

    private static bool IsTyping() {
        var selected = EventSystem.current?.currentSelectedGameObject;
        return selected != null
            && (selected.GetComponent<TMP_InputField>() != null || selected.GetComponent<UnityEngine.UI.InputField>() != null);
    }

    private void Copy() {
        if (selectedObject == null) {
            return;
        }

        var hovered = HoveredComponent();
        if (hovered != null) {
            copiedObject = null;
            copiedComponentType = hovered.GetType();
            copiedComponent = hovered.Serialize();
            copiedTextEngine = hovered is TextMeshProUGUISettings ? selectedObject.Config.TextEngineConfig?.Serialize() : null;
            return;
        }

        copiedComponentType = null;
        copiedComponent = null;
        copiedTextEngine = null;
        copiedObject = selectedObject.Serialize();
    }

    private UnityComponentSettingsBase HoveredComponent() {
        Vector2 mouse = O5Input.MousePosition;
        foreach (var (card, settings) in componentCards) {
            if (card != null && RectTransformUtility.RectangleContainsScreenPoint(card, mouse, null)) {
                return settings;
            }
        }
        return null;
    }

    private void Paste() {
        if (copiedObject != null) {
            var obj = new OvObject();
            obj.Deserialize(copiedObject);
            InsertAfter(selectedObject, obj);
            obj.ApplyComponent();
            obj.ApplyConfig();
            SelectObject(obj);
            SaveConfig();
            return;
        }

        if (copiedComponent == null || selectedObject == null) {
            return;
        }

        var config = selectedObject.Config;
        // Same rules as the Add Component dropdown.
        if ((copiedComponentType == typeof(TextMeshProUGUISettings) && config.ImageConfig != null)
            || (copiedComponentType == typeof(ImageSettings) && config.TextConfig != null)
            || (copiedComponentType == typeof(ColorRangeSettings) && config.TextConfig == null)) {
            return;
        }

        var field = typeof(OvObjectSettings).GetFields().FirstOrDefault(f => f.FieldType == copiedComponentType);
        if (field == null) {
            return;
        }
        var settings = (ISettingsFile)Activator.CreateInstance(copiedComponentType);
        settings.Deserialize(copiedComponent);
        field.SetValue(config, settings);
        if (copiedComponentType == typeof(TextMeshProUGUISettings)) {
            config.TextEngineConfig = new OvTextSettings();
            if (copiedTextEngine != null) {
                config.TextEngineConfig.Deserialize(copiedTextEngine);
            }
        }

        selectedObject.ApplyComponent();
        selectedObject.ApplyConfig();
        SaveConfig();
        RebuildInspector();
    }
}
