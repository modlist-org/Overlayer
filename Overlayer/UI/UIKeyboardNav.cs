using System.Reflection;
using O5Kit.Behaviour;
using O5Kit.Control;
using O5Kit.Core;
using O5Kit.Input;
using Overlayer.Core;
using Overlayer.UI.Factory;
using Overlayer.UI.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI;

/// <summary>
/// Full keyboard navigation for Overlayer's menu and pages:
/// Tab / Shift+Tab (sequential), Arrow keys (2D spatial + slider adjustment),
/// Enter / Space (activate / hold-to-confirm), and Esc (blur / close popup / clear focus),
/// highlighted with Olly's pulsing 4-edge accent frame.
/// </summary>
internal static class UIKeyboardNav {
    private const float FramePad = 6f;
    private const float EdgeThickness = 3f;
    private const float RepeatDelay = 0.38f;
    private const float RepeatInterval = 0.065f;

    private static readonly FieldInfo TickablesField =
        typeof(O5Object).GetField("_tickables", BindingFlags.Static | BindingFlags.NonPublic);

    private static RectTransform frame;
    private static readonly Image[] edges = new Image[4];
    private static readonly Vector3[] corners = new Vector3[4];
    private static readonly List<RectTransform> candidates = [];
    private static readonly Dictionary<int, O5Object> tickableByRectId = [];

    private static RectTransform focused;
    private static RectTransform scrolledFor;
    private static GameObject keyboardHoldTarget;
    private static bool wasTypingLastFrame;

    private static KeyCode heldKey = KeyCode.None;
    private static float nextRepeatAt;

    internal static RectTransform Focused => focused;

    public static void Tick() {
        if (!CanNavigate()) {
            ReleaseKeyboardHold();
            HideFrame();
            focused = null;
            scrolledFor = null;
            heldKey = KeyCode.None;
            wasTypingLastFrame = false;
            return;
        }

        // Clear keyboard focus ring when the user clicks with the mouse.
        if (O5Input.GetMouseButtonDown(0) || O5Input.GetMouseButtonDown(1)) {
            ReleaseKeyboardHold();
            ClearFocus();
        }

        var activeInput = GetFocusedInputField();
        bool typing = activeInput != null;
        if (typing) {
            ReleaseKeyboardHold();
            wasTypingLastFrame = true;
            // Allow Tab / Shift+Tab to step out of single-line inputs (like search/slider boxes),
            // while leaving multiline code editors untouched for completion/indentation.
            if (activeInput.lineType == TMP_InputField.LineType.SingleLine
                && string.IsNullOrEmpty(O5Input.CompositionString)
                && O5Input.GetKeyDown(KeyCode.Tab)) {
                var parentCandidate = FindOwningCandidate(activeInput.transform);
                activeInput.DeactivateInputField();
                EventSystem.current?.SetSelectedGameObject(null);
                wasTypingLastFrame = false;
                if (parentCandidate != null) {
                    focused = parentCandidate;
                }
                StepSequential(IsShiftHeld() ? -1 : 1);
            } else {
                UpdateFrame();
            }
            return;
        }

        // If Esc just exited a TMP_InputField on the previous/same frame, don't double-clear nav focus immediately.
        if (wasTypingLastFrame) {
            wasTypingLastFrame = false;
            if (O5Input.GetKeyDown(KeyCode.Escape)) {
                UpdateFrame();
                return;
            }
        }

        // Hold-to-confirm tracking while Enter/Space is held on a HoldToConfirm button.
        if (keyboardHoldTarget != null) {
            bool holdKey = O5Input.GetKey(KeyCode.Return) || O5Input.GetKey(KeyCode.KeypadEnter) || O5Input.GetKey(KeyCode.Space);
            if (!holdKey || focused == null || focused.gameObject != keyboardHoldTarget || !keyboardHoldTarget.activeInHierarchy) {
                ReleaseKeyboardHold();
            }
        }

        if (O5Input.GetKeyDown(KeyCode.Escape)) {
            ReleaseKeyboardHold();
            if (!TryCloseOpenPopupOrDropdown()) {
                ClearFocus();
            }
            UpdateFrame();
            return;
        }

        if (ReadNavKey(KeyCode.Tab)) {
            ReleaseKeyboardHold();
            StepSequential(IsShiftHeld() ? -1 : 1);
        } else if (ReadNavKey(KeyCode.DownArrow)) {
            ReleaseKeyboardHold();
            StepDirectional(Vector2.down);
        } else if (ReadNavKey(KeyCode.UpArrow)) {
            ReleaseKeyboardHold();
            StepDirectional(Vector2.up);
        } else if (ReadNavKey(KeyCode.RightArrow)) {
            ReleaseKeyboardHold();
            if (!TryAdjustFocusedSlider(1)) {
                StepDirectional(Vector2.right);
            }
        } else if (ReadNavKey(KeyCode.LeftArrow)) {
            ReleaseKeyboardHold();
            if (!TryAdjustFocusedSlider(-1)) {
                StepDirectional(Vector2.left);
            }
        } else if (focused != null && (O5Input.GetKeyDown(KeyCode.Return) || O5Input.GetKeyDown(KeyCode.KeypadEnter) || O5Input.GetKeyDown(KeyCode.Space))) {
            Activate(focused);
        }

        if (focused != null && !IsAliveAndVisible(focused)) {
            RecoverLostFocus();
        }

        UpdateFrame();
    }

    public static void ClearFocus() {
        focused = null;
        scrolledFor = null;
        HideFrame();
    }

    public static void Dispose() {
        ReleaseKeyboardHold();
        focused = null;
        scrolledFor = null;
        heldKey = KeyCode.None;
        wasTypingLastFrame = false;
        candidates.Clear();
        tickableByRectId.Clear();
        if (frame != null) {
            UnityEngine.Object.Destroy(frame.gameObject);
            frame = null;
        }
        Array.Clear(edges, 0, edges.Length);
    }

    private static bool CanNavigate() {
        if (!UICore.IsOpen || UICore.CanvasObj == null || !UICore.CanvasObj.activeInHierarchy) {
            return false;
        }
        if (UICore.Panel == null || !UICore.Panel.gameObject.activeInHierarchy) {
            return false;
        }
        if (HudEdit.HudEditor.IsOpen || Setup.SetupWizard.IsOpen || Setup.SetupIntro.IsBlocking || KeyCapture.Active) {
            return false;
        }
        return true;
    }

    internal static int CandidateCount {
        get {
            RebuildCandidates();
            return candidates.Count;
        }
    }

    internal static void ActivateFocused() {
        if (focused != null) {
            Activate(focused);
            if (focused != null && !IsAliveAndVisible(focused)) {
                RecoverLostFocus();
            }
            UpdateFrame();
        }
    }

    internal static void HandleEscape() {
        ReleaseKeyboardHold();
        if (!TryCloseOpenPopupOrDropdown()) {
            ClearFocus();
        }
        UpdateFrame();
    }

    private static bool IsShiftHeld()
        => O5Input.GetKey(KeyCode.LeftShift) || O5Input.GetKey(KeyCode.RightShift);

    private static bool ReadNavKey(KeyCode key) {
        if (O5Input.GetKeyDown(key)) {
            heldKey = key;
            nextRepeatAt = Time.unscaledTime + RepeatDelay;
            return true;
        }
        if (heldKey == key) {
            if (!O5Input.GetKey(key)) {
                heldKey = KeyCode.None;
                return false;
            }
            if (Time.unscaledTime >= nextRepeatAt) {
                nextRepeatAt = Time.unscaledTime + RepeatInterval;
                return true;
            }
        }
        return false;
    }

    private static TMP_InputField GetFocusedInputField() {
        var es = EventSystem.current;
        var go = es != null ? es.currentSelectedGameObject : null;
        var field = go != null ? go.GetComponent<TMP_InputField>() : null;
        return field != null && field.isFocused ? field : null;
    }

    private static RectTransform FindOwningCandidate(Transform leaf) {
        RebuildCandidates();
        for (Transform t = leaf; t != null; t = t.parent) {
            var rt = UnityUtils.AsRectTransform(t);
            if (rt != null && IndexOfCandidate(rt) >= 0) {
                return rt;
            }
        }
        return null;
    }

    private static int IndexOfCandidate(RectTransform rt) {
        if (rt == null) {
            return -1;
        }
        int id = rt.GetInstanceID();
        for (int i = 0; i < candidates.Count; i++) {
            if (candidates[i] != null && candidates[i].GetInstanceID() == id) {
                return i;
            }
        }
        return -1;
    }

    private static bool SameObject(UnityEngine.Object a, UnityEngine.Object b) {
        if (a == null && b == null) {
            return true;
        }
        if (a == null || b == null) {
            return false;
        }
        return a.GetInstanceID() == b.GetInstanceID();
    }

    // ---- candidate discovery ----

    private static void RebuildTickablesMap() {
        tickableByRectId.Clear();
        if (TickablesField?.GetValue(null) is not System.Collections.IList list) {
            return;
        }
        for (int i = 0; i < list.Count; i++) {
            if (list[i] is O5Object obj && !obj.IsDisposed && obj.Rect != null) {
                tickableByRectId[obj.Rect.GetInstanceID()] = obj;
            }
        }
    }

    private static void RebuildCandidates() {
        candidates.Clear();
        RebuildTickablesMap();

        if (UICore.CanvasObj == null) {
            return;
        }

        // If an interactive modal popup (e.g. ColorPicker / SpriteEditor) is open directly on the UI canvas, trap focus inside it.
        if (TryCollectActiveModalPopup()) {
            return;
        }

        var panel = UICore.Panel;
        if (panel == null) {
            return;
        }

        // Logical order: TopBar controls -> Sidebar Menu items + Power -> Active page controls.
        var topBar = panel.Find("TopBar");
        if (topBar != null && topBar.gameObject.activeInHierarchy) {
            CollectSubtree(topBar);
        }

        if (UICore.Menu != null && UICore.Menu.gameObject.activeInHierarchy) {
            CollectSubtree(UICore.Menu);
        }

        if (UICore.Pages != null && UICore.Pages.TryGetValue(UICore.CurrentMenuState, out var activePage)
            && activePage != null && activePage.gameObject.activeInHierarchy) {
            for (int i = 0; i < activePage.childCount; i++) {
                CollectSubtree(activePage.GetChild(i));
            }
        } else if (PageFactory.PagesContaner != null && PageFactory.PagesContaner.gameObject.activeInHierarchy) {
            CollectSubtree(PageFactory.PagesContaner);
        }
    }

    private static bool TryCollectActiveModalPopup() {
        var canvasTransform = UICore.CanvasObj.transform;
        for (int i = canvasTransform.childCount - 1; i >= 0; i--) {
            var child = canvasTransform.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy) {
                continue;
            }
            if (child == UICore.Panel || (frame != null && child == frame)) {
                continue;
            }
            string n = child.name;
            if (n.IndexOf("Tooltip", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Blocker", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Resize", StringComparison.OrdinalIgnoreCase) >= 0
                || n is "TagCompletion" or "JsCompletion") {
                continue;
            }
            if (!IsModalGroupActive(child)) {
                continue;
            }
            candidates.Clear();
            CollectSubtree(child);
            if (candidates.Count > 0) {
                return true;
            }
        }
        return false;
    }

    private static bool IsModalGroupActive(Transform t) {
        var cg = t.GetComponent<CanvasGroup>();
        if (cg == null || !cg.enabled) {
            return true;
        }
        return cg.interactable && cg.blocksRaycasts;
    }

    private static void CollectSubtree(Transform root) {
        if (root == null || !root.gameObject.activeInHierarchy) {
            return;
        }
        if (!IsCanvasGroupInteractive(root)) {
            return;
        }

        var rt = UnityUtils.AsRectTransform(root);
        if (rt != null && IsCandidate(rt, out bool descendIntoChildren)) {
            if (HasUsableSize(rt)) {
                candidates.Add(rt);
            }
            if (!descendIntoChildren) {
                return;
            }
        }

        int count = root.childCount;
        for (int i = 0; i < count; i++) {
            CollectSubtree(root.GetChild(i));
        }
    }

    private static bool IsCandidate(RectTransform rt, out bool descendIntoChildren) {
        descendIntoChildren = true;
        var go = rt.gameObject;
        string name = go.name;

        if (name is "Panel" or "Menu" or "TopBar" or "ScrollView" or "Viewport" or "Content" or "Wheel"
            || name.IndexOf("Blocker", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Resize", StringComparison.OrdinalIgnoreCase) >= 0) {
            return false;
        }
        if (go.GetComponent<DragHandler>() != null
            || go.GetComponent<ResizeHandle>() != null
            || go.GetComponent<ScrollRect>() != null
            || go.GetComponent<UIScrollController>() != null) {
            return false;
        }

        if (tickableByRectId.TryGetValue(rt.GetInstanceID(), out var o5Obj)) {
            if (o5Obj is O5Slider or O5InputField) {
                // Single focus stop for the whole slider / input field (don't also stop on inner TMP_InputField).
                descendIntoChildren = false;
                return true;
            }
            if (o5Obj is O5ColorPicker || IsDropdownObject(o5Obj)) {
                // Descend into the interactive children:
                // - O5ColorPicker: Header/ToggleArea + Header/HexInput
                // - O5Dropdown / O5MultiDropdown: Header background (OventHandler) + expanded List/Row items
                descendIntoChildren = true;
                return false;
            }
        }

        if (go.GetComponent<TMP_InputField>() is { } tmpInput) {
            descendIntoChildren = false;
            return tmpInput.IsInteractable();
        }
        if (go.GetComponent<Button>() is { } button) {
            descendIntoChildren = false;
            return button.IsInteractable();
        }
        if (go.GetComponent<Toggle>() is { } toggle) {
            descendIntoChildren = false;
            return toggle.IsInteractable();
        }
        if (go.GetComponent<Slider>() is { } slider) {
            descendIntoChildren = false;
            return slider.IsInteractable();
        }
        if (go.GetComponent<NonRaycastButton>() is { } nrb && nrb.onClick != null) {
            descendIntoChildren = false;
            return true;
        }
        if (go.GetComponent<OventHandler>() is { } ovent && ovent.OnClick != null) {
            // Card headers / tiles / dropdown headers can contain child buttons (e.g. tile action buttons).
            descendIntoChildren = name.IndexOf("Toggle", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Button", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Segment", StringComparison.OrdinalIgnoreCase) < 0;
            return true;
        }
        if (go.GetComponent<EventTrigger>() is { } trigger && HasClickableTrigger(trigger)) {
            descendIntoChildren = name.IndexOf("Button", StringComparison.OrdinalIgnoreCase) < 0
                && name.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0;
            return true;
        }

        return false;
    }

    private static bool IsDropdownObject(O5Object obj) {
        var t = obj.GetType();
        if (!t.IsGenericType) {
            return false;
        }
        var def = t.GetGenericTypeDefinition();
        return def == typeof(O5Dropdown<>) || def == typeof(O5MultiDropdown<>);
    }

    private static bool HasClickableTrigger(EventTrigger trigger) {
        if (trigger == null || trigger.triggers == null) {
            return false;
        }
        bool hasClick = false, hasDown = false, hasUp = false;
        for (int i = 0; i < trigger.triggers.Count; i++) {
            var id = trigger.triggers[i].eventID;
            if (id == EventTriggerType.PointerClick) {
                hasClick = true;
            } else if (id == EventTriggerType.PointerDown) {
                hasDown = true;
            } else if (id == EventTriggerType.PointerUp) {
                hasUp = true;
            }
        }
        return hasClick || (hasDown && hasUp);
    }

    private static bool IsActivePage(Transform t)
        => UICore.Pages != null
        && UICore.Pages.TryGetValue(UICore.CurrentMenuState, out var page)
        && page != null
        && SameObject(t, page);

    private static bool IsCanvasGroupInteractive(Transform t) {
        if (IsActivePage(t)) {
            return true;
        }
        var cg = t.GetComponent<CanvasGroup>();
        if (cg != null && cg.enabled && (!cg.interactable || !cg.blocksRaycasts)) {
            return false;
        }
        return true;
    }

    private static bool IsAliveAndVisible(RectTransform rt) {
        if (rt == null || !rt.gameObject.activeInHierarchy) {
            return false;
        }
        for (Transform t = rt; t != null; t = t.parent) {
            if (!IsCanvasGroupInteractive(t)) {
                return false;
            }
        }
        return HasUsableSize(rt);
    }

    private static bool HasUsableSize(RectTransform rt) {
        var rect = rt.rect;
        return rect.width >= 6f && rect.height >= 6f;
    }

    // ---- navigation ----

    internal static void StepSequential(int delta) {
        RebuildCandidates();
        if (candidates.Count == 0) {
            ClearFocus();
            return;
        }

        int idx = IndexOfCandidate(focused);
        if (idx < 0) {
            // Start on the first control in the active page if available, otherwise first candidate.
            idx = delta > 0 ? FindInitialIndex() : candidates.Count - 1;
        } else {
            idx = (idx + delta + candidates.Count) % candidates.Count;
        }
        SetFocus(candidates[idx]);
    }

    internal static void StepDirectional(Vector2 dir) {
        RebuildCandidates();
        if (candidates.Count == 0) {
            ClearFocus();
            return;
        }

        int curIdx = IndexOfCandidate(focused);
        if (curIdx < 0) {
            SetFocus(candidates[FindInitialIndex()]);
            return;
        }

        int focusedId = focused.GetInstanceID();
        GetScreenBounds(focused, out Vector2 curMin, out Vector2 curMax);
        Vector2 curCenter = (curMin + curMax) * 0.5f;
        bool vertical = Mathf.Abs(dir.y) > 0.5f;

        // Pressing Right from the sidebar should always enter the active page (never jump up to TopBar Close),
        // even during the page's slide-in transition.
        bool preferPageFromSidebar = false;
        if (dir.x > 0.5f && UICore.Menu != null && PageFactory.PagesContaner != null && focused.IsChildOf(UICore.Menu)) {
            for (int i = 0; i < candidates.Count; i++) {
                if (candidates[i] != null && candidates[i].IsChildOf(PageFactory.PagesContaner)) {
                    preferPageFromSidebar = true;
                    break;
                }
            }
        }

        RectTransform best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++) {
            var cand = candidates[i];
            if (cand == null || cand.GetInstanceID() == focusedId) {
                continue;
            }
            if (preferPageFromSidebar && !cand.IsChildOf(PageFactory.PagesContaner)) {
                continue;
            }
            GetScreenBounds(cand, out Vector2 min, out Vector2 max);
            Vector2 center = (min + max) * 0.5f;
            Vector2 diff = center - curCenter;

            float primary = Vector2.Dot(diff, dir);
            if (primary <= 2f) {
                continue;
            }

            // Perpendicular distance between projected intervals (0 when aligned in the same column/row).
            float perpDist;
            if (vertical) {
                float overlap = Mathf.Min(curMax.x, max.x) - Mathf.Max(curMin.x, min.x);
                perpDist = overlap > 0f ? 0f : Mathf.Max(curMin.x - max.x, min.x - curMax.x);
            } else {
                float overlap = Mathf.Min(curMax.y, max.y) - Mathf.Max(curMin.y, min.y);
                perpDist = overlap > 0f ? 0f : Mathf.Max(curMin.y - max.y, min.y - curMax.y);
            }

            float centerPerp = vertical ? Mathf.Abs(diff.x) : Mathf.Abs(diff.y);
            float score = primary + perpDist * 3.5f + centerPerp * 0.35f;
            if (score < bestScore) {
                bestScore = score;
                best = cand;
            }
        }

        if (best != null) {
            SetFocus(best);
        }
    }

    private static int FindInitialIndex() {
        if (PageFactory.PagesContaner != null) {
            for (int i = 0; i < candidates.Count; i++) {
                if (candidates[i].IsChildOf(PageFactory.PagesContaner)) {
                    return i;
                }
            }
        }
        return 0;
    }

    private static void SetFocus(RectTransform target) {
        if (target == null) {
            ClearFocus();
            return;
        }
        bool changed = !SameObject(focused, target);
        focused = target;
        if (changed) {
            scrolledFor = null;
            UISound.PlayHover();
        }
    }

    // ---- activation & control editing ----

    internal static bool TryAdjustFocusedSlider(int dir) {
        if (focused == null) {
            return false;
        }
        RebuildTickablesMap();
        if (tickableByRectId.TryGetValue(focused.GetInstanceID(), out var obj) && obj is O5Slider slider && !slider.IsDisposed) {
            double span = Math.Abs(slider.Max - slider.Min);
            double step = Math.Max(0.01d, span * (IsShiftHeld() ? 0.01d : 0.05d));
            double next = slider.Value + dir * step;
            if (slider.DragFilter != null) {
                double filtered = slider.DragFilter(next);
                if (Math.Abs(filtered - slider.Value) < 1e-9) {
                    filtered = slider.DragFilter(slider.Value + dir);
                }
                next = filtered;
            }
            if (slider.ClampDrag) {
                double lo = Math.Min(slider.Min, slider.Max);
                double hi = Math.Max(slider.Min, slider.Max);
                next = Math.Max(lo, Math.Min(hi, next));
            }
            slider.Set(next, true);
            slider.OnComplete?.Invoke(slider.Value);
            float norm = span > 1e-6 ? (float)((slider.Value - slider.Min) / span) : 0.5f;
            UISound.PlaySliderTick(Mathf.Clamp01(norm));
            return true;
        }
        if (focused.GetComponent<Slider>() is { } unitySlider && unitySlider.IsInteractable()) {
            float span = Mathf.Abs(unitySlider.maxValue - unitySlider.minValue);
            float step = unitySlider.wholeNumbers ? 1f : Mathf.Max(0.01f, span * (IsShiftHeld() ? 0.01f : 0.05f));
            unitySlider.value = Mathf.Clamp(unitySlider.value + dir * step, unitySlider.minValue, unitySlider.maxValue);
            UISound.PlaySliderTick(unitySlider.normalizedValue);
            return true;
        }
        return false;
    }

    private static void Activate(RectTransform target) {
        if (target == null || !IsAliveAndVisible(target)) {
            return;
        }
        RebuildTickablesMap();
        var go = target.gameObject;

        if (tickableByRectId.TryGetValue(target.GetInstanceID(), out var o5Obj)) {
            if (o5Obj is O5Slider slider && !slider.IsDisposed && slider.InputCore?.InputField != null) {
                FocusInputField(slider.InputCore.InputField, moveToEnd: false);
                return;
            }
            if (o5Obj is O5InputField inputField && !inputField.IsDisposed && inputField.InputField != null) {
                FocusInputField(inputField.InputField, moveToEnd: true);
                return;
            }
        }

        if (go.GetComponent<TMP_InputField>() is { } tmpInput && tmpInput.IsInteractable()) {
            FocusInputField(tmpInput, moveToEnd: true);
            return;
        }

        if (HoldToConfirm.IsHoldTarget(go)) {
            if (HoldToConfirm.SetKeyboardHold(go, true, IsShiftHeld())) {
                keyboardHoldTarget = go;
            }
            return;
        }

        if (go.GetComponent<Button>() is { } btn && btn.IsInteractable()) {
            UISound.PlayConfirm();
            btn.onClick.Invoke();
            return;
        }

        if (go.GetComponent<Toggle>() is { } tog && tog.IsInteractable()) {
            tog.isOn = !tog.isOn;
            UISound.PlayToggle(tog.isOn);
            return;
        }

        if (go.GetComponent<NonRaycastButton>() is { } nrb && nrb.onClick != null) {
            UISound.PlayConfirm();
            nrb.onClick.Invoke();
            return;
        }

        if (go.GetComponent<OventHandler>() is { } ovent && ovent.OnClick != null) {
            ovent.OnClick.Invoke(PointerEventData.InputButton.Left);
            return;
        }

        if (go.GetComponent<EventTrigger>() is { } trigger && HasClickableTrigger(trigger)) {
            bool silentSelfAudio = (target.parent != null && target.parent.name == "List")
                || (UICore.MenuContent != null && target.IsChildOf(UICore.MenuContent));
            InvokeEventTriggerClick(trigger);
            if (!silentSelfAudio) {
                UISound.PlayConfirm();
            }
        }
    }

    private static void FocusInputField(TMP_InputField field, bool moveToEnd) {
        if (EventSystem.current != null) {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(field.gameObject);
        }
        field.Select();
        field.ActivateInputField();
        if (moveToEnd) {
            field.MoveTextEnd(false);
        }
    }

    private static void InvokeEventTriggerClick(EventTrigger trigger) {
        var es = EventSystem.current;
        var ped = es != null
            ? new PointerEventData(es) { button = PointerEventData.InputButton.Left, clickCount = 1 }
            : null;

        bool hasClick = false;
        for (int i = 0; i < trigger.triggers.Count; i++) {
            if (trigger.triggers[i].eventID == EventTriggerType.PointerClick) {
                hasClick = true;
                break;
            }
        }

        if (hasClick) {
            FireTrigger(trigger, EventTriggerType.PointerClick, ped);
        } else {
            // O5Button requires _hovered && _holding on PointerUp to invoke OnClick.
            FireTrigger(trigger, EventTriggerType.PointerEnter, ped);
            FireTrigger(trigger, EventTriggerType.PointerDown, ped);
            FireTrigger(trigger, EventTriggerType.PointerUp, ped);
            FireTrigger(trigger, EventTriggerType.PointerExit, ped);
        }
    }

    private static void FireTrigger(EventTrigger trigger, EventTriggerType type, BaseEventData data) {
        for (int i = 0; i < trigger.triggers.Count; i++) {
            var entry = trigger.triggers[i];
            if (entry.eventID == type) {
                entry.callback?.Invoke(data);
            }
        }
    }

    private static bool TryCloseOpenPopupOrDropdown() {
        RebuildTickablesMap();
        foreach (var obj in tickableByRectId.Values) {
            if (obj == null || obj.IsDisposed) {
                continue;
            }
            if (obj is O5ColorPicker picker && picker.Expanded) {
                picker.SetExpanded(false);
                var toggleArea = picker.Rect != null ? UnityUtils.AsRectTransform(picker.Rect.Find("Header/ToggleArea")) : null;
                SetFocus(toggleArea ?? picker.Rect);
                return true;
            }
            var t = obj.GetType();
            if (t.IsGenericType) {
                var def = t.GetGenericTypeDefinition();
                if (def == typeof(O5Dropdown<>) || def == typeof(O5MultiDropdown<>)) {
                    var expProp = t.GetProperty("Expanded");
                    if (expProp != null && expProp.GetValue(obj) is true) {
                        var setExp = t.GetMethod("SetExpanded");
                        if (setExp != null) {
                            var ps = setExp.GetParameters();
                            object[] args = ps.Length == 2 ? [false, true] : [false];
                            setExp.Invoke(obj, args);
                            SetFocus(FindDropdownHeader(obj.Rect) ?? obj.Rect);
                            return true;
                        }
                    }
                }
            }
        }

        // O5MultiDropdown doesn't register as a tickable; detect any active MultiDropdown/List in the page.
        if (PageFactory.PagesContaner != null) {
            var lists = PageFactory.PagesContaner.GetComponentsInChildren<Transform>(false);
            for (int i = 0; i < lists.Length; i++) {
                var tr = lists[i];
                if (tr != null && tr.name == "List" && tr.parent != null && tr.parent.name == "MultiDropdown") {
                    var header = FindDropdownHeader(UnityUtils.AsRectTransform(tr.parent));
                    if (header != null && header.GetComponent<OventHandler>() is { } ovent && ovent.OnClick != null) {
                        ovent.OnClick.Invoke(PointerEventData.InputButton.Left);
                        SetFocus(header);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static RectTransform FindDropdownHeader(RectTransform dropdownRoot) {
        if (dropdownRoot == null) {
            return null;
        }
        for (int i = 0; i < dropdownRoot.childCount; i++) {
            var child = dropdownRoot.GetChild(i);
            if (child != null && child.name != "List" && child.GetComponent<OventHandler>() != null) {
                return UnityUtils.AsRectTransform(child);
            }
        }
        return null;
    }

    private static void RecoverLostFocus() {
        // When a dropdown Row closes its List on click, keep focus on the dropdown header instead of dropping focus.
        if (focused != null && focused.parent != null && focused.parent.name == "List" && focused.parent.parent != null) {
            var header = FindDropdownHeader(UnityUtils.AsRectTransform(focused.parent.parent));
            if (header != null && IsAliveAndVisible(header)) {
                SetFocus(header);
                return;
            }
        }
        focused = null;
        scrolledFor = null;
    }

    private static void ReleaseKeyboardHold() {
        if (keyboardHoldTarget != null) {
            HoldToConfirm.SetKeyboardHold(keyboardHoldTarget, false, false);
            keyboardHoldTarget = null;
        }
    }

    // ---- Olly-style pulsing 4-edge focus frame ----

    private static void EnsureFrame() {
        if (frame != null || UICore.CanvasObj == null) {
            return;
        }
        frame = UiStyle.NewRect("KeyboardFocusFrame", UICore.CanvasObj.transform);
        frame.anchorMin = frame.anchorMax = Vector2.zero;
        frame.pivot = Vector2.zero;
        var cg = frame.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        Color accent = UIColors.ObjectActive;
        for (int i = 0; i < 4; i++) {
            var img = UiStyle.NewRect("Edge" + i, frame).gameObject.AddComponent<Image>();
            img.color = accent;
            img.raycastTarget = false;
            var r = img.rectTransform;
            r.anchorMin = i switch { 0 => Vector2.zero, 1 => new Vector2(0f, 1f), 2 => Vector2.zero, _ => new Vector2(1f, 0f) };
            r.anchorMax = i switch { 0 => new Vector2(1f, 0f), 1 => Vector2.one, 2 => new Vector2(0f, 1f), _ => Vector2.one };
            r.sizeDelta = i < 2 ? new Vector2(0f, EdgeThickness) : new Vector2(EdgeThickness, 0f);
            edges[i] = img;
        }
        frame.gameObject.SetActive(false);
    }

    private static void HideFrame() {
        if (frame != null && frame.gameObject.activeSelf) {
            frame.gameObject.SetActive(false);
        }
    }

    internal static void UpdateFrame() {
        if (focused == null || !IsAliveAndVisible(focused)) {
            HideFrame();
            return;
        }

        EnsureFrame();
        if (frame == null) {
            return;
        }

        if (!SameObject(scrolledFor, focused)) {
            ScrollIntoView(focused);
            scrolledFor = focused;
        }

        var canvasRect = UICore.CanvasObj.GetComponent<RectTransform>();
        if (canvasRect == null) {
            HideFrame();
            return;
        }

        float groupAlpha = GetEffectiveCanvasGroupAlpha(focused);
        if (groupAlpha <= 0.02f) {
            HideFrame();
            return;
        }

        GetCanvasBounds(focused, canvasRect, out Vector2 min, out Vector2 max);
        min -= Vector2.one * FramePad;
        max += Vector2.one * FramePad;

        // Clamp against every clipping RectMask2D ancestor (ScrollView viewports + UICore.Panel)
        // so the focus frame never leaks outside the window or over the TopBar during scroll/slide transitions.
        for (Transform p = focused.parent; p != null && !SameObject(p, canvasRect); p = p.parent) {
            if (p.GetComponent<RectMask2D>() is { enabled: true }) {
                var maskRt = UnityUtils.AsRectTransform(p);
                if (maskRt != null) {
                    GetCanvasBounds(maskRt, canvasRect, out Vector2 cMin, out Vector2 cMax);
                    min.x = Mathf.Max(min.x, cMin.x);
                    min.y = Mathf.Max(min.y, cMin.y);
                    max.x = Mathf.Min(max.x, cMax.x);
                    max.y = Mathf.Min(max.y, cMax.y);
                }
            }
        }

        float w = max.x - min.x;
        float h = max.y - min.y;
        if (w < 4f || h < 4f) {
            HideFrame();
            return;
        }

        frame.gameObject.SetActive(true);
        frame.SetAsLastSibling();
        frame.anchoredPosition = min;
        frame.sizeDelta = new Vector2(w, h);

        Color accent = UIColors.ObjectActive;
        float alpha = (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f))) * groupAlpha;
        var edgeColor = new Color(accent.r, accent.g, accent.b, alpha);
        for (int i = 0; i < edges.Length; i++) {
            if (edges[i] != null) {
                edges[i].color = edgeColor;
            }
        }
    }

    private static float GetEffectiveCanvasGroupAlpha(Transform leaf) {
        float alpha = 1f;
        for (Transform t = leaf; t != null; t = t.parent) {
            var cg = t.GetComponent<CanvasGroup>();
            if (cg != null && cg.enabled) {
                alpha *= cg.alpha;
                if (alpha <= 0.02f) {
                    return 0f;
                }
            }
        }
        return Mathf.Clamp01(alpha);
    }

    private static void GetCanvasBounds(RectTransform target, RectTransform canvasRect, out Vector2 min, out Vector2 max) {
        GetScreenBounds(target, out Vector2 sMin, out Vector2 sMax);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sMin, null, out Vector2 lMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sMax, null, out Vector2 lMax);
        // Convert canvas-local (pivot-centered) to bottom-left anchoredPosition matching anchorMin=(0,0).
        Vector2 origin = canvasRect.rect.min;
        min = lMin - origin;
        max = lMax - origin;
    }

    private static void GetScreenBounds(RectTransform target, out Vector2 min, out Vector2 max) {
        UnityUtils.GetWorldCornersSafe(target, corners);
        var canvas = target.GetComponentInParent<Canvas>()?.rootCanvas;
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 p0 = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 p1 = RectTransformUtility.WorldToScreenPoint(cam, corners[1]);
        Vector2 p2 = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        Vector2 p3 = RectTransformUtility.WorldToScreenPoint(cam, corners[3]);
        min = new Vector2(Mathf.Min(p0.x, p1.x, p2.x, p3.x), Mathf.Min(p0.y, p1.y, p2.y, p3.y));
        max = new Vector2(Mathf.Max(p0.x, p1.x, p2.x, p3.x), Mathf.Max(p0.y, p1.y, p2.y, p3.y));
    }

    private static void ScrollIntoView(RectTransform target) {
        var scroller = target.GetComponentInParent<UIScrollController>();
        if (scroller == null || scroller.content == null || scroller.viewport == null) {
            return;
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroller.content);
        var rel = RectTransformUtility.CalculateRelativeRectTransformBounds(scroller.viewport, target);
        var view = scroller.viewport.rect;
        const float pad = 16f;
        float now = scroller.content.anchoredPosition.y;
        bool scrolled = false;
        if (rel.size.y > view.height - pad * 2f || rel.max.y > view.yMax - pad) {
            scroller.ScrollTo(now - (rel.max.y - (view.yMax - pad)), true);
            scrolled = true;
        } else if (rel.min.y < view.yMin + pad) {
            scroller.ScrollTo(now + ((view.yMin + pad) - rel.min.y), true);
            scrolled = true;
        }
        if (scrolled) {
            Canvas.ForceUpdateCanvases();
        }
    }
}
