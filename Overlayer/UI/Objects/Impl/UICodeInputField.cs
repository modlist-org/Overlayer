using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using O5Kit.Input;

#if ML && IL2CPP
using Il2CppInterop.Runtime;
using MelonLoader;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
public sealed class UICodeInputField
#if ML && IL2CPP
    // On IL2CPP Unity 6, ClassInjector leaves has_references=0/gc_desc=0 on subclasses of TMP_InputField,
    // causing Boehm GC to sweep inherited Selectable/TMP_InputField fields (m_CanvasGroupCache, m_OnValueChanged).
    // Keep a plain MonoBehaviour companion on a real native TMP_InputField instead.
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
    : TMP_InputField
#endif
{
    public Action<TMP_Text, bool> AfterLabelUpdate;
    public Func<KeyCode, bool> HandleKey;
    public Func<bool> IsCompletionActive;
    public Action OnFieldDisabled;
    public Action OnFieldDestroyed;
    public Action OnDoubleClick;
    public bool CanUndo => undoHistory.Count > 0;
    public bool CanRedo => redoHistory.Count > 0;

    private readonly List<HistoryState> undoHistory = [];
    private readonly List<HistoryState> redoHistory = [];
    private HistoryState lastState;
    private EditInfo lastEdit;
    private bool hasLastEdit;
    private bool suppressHistory;
    private UnityEngine.Events.UnityAction<string> historyCallback;
    private const int MaxHistory = 100;
    private static UICodeInputField hoveredField;
    private RectTransform caretTransform;
    private bool completionLocked;
    private string lockedText;
    private int lockedAnchor;
    private int lockedFocus;
    private Vector2 lockedScrollPos;
#if !(ML && IL2CPP)
    private readonly Event drainEvent = new();
#endif

    public static bool ShouldConsumeParentScroll
        => hoveredField != null && IsShiftHeld();

    public static TMP_InputField Attach(GameObject go) {
#if ML && IL2CPP
        var field = go.AddComponent<TMP_InputField>();
        var code = go.AddComponent<UICodeInputField>();
        code.Bind(field);
        return field;
#else
        return go.AddComponent<UICodeInputField>();
#endif
    }

    public static UICodeInputField From(TMP_InputField field) {
#if ML && IL2CPP
        if (field == null) {
            return null;
        }
        var code = field.GetComponent<UICodeInputField>();
        if (code != null && code.Field == null) {
            code.Bind(field);
        }
        return code;
#else
        return (UICodeInputField)field;
#endif
    }

#if ML && IL2CPP
    public TMP_InputField Field { get; private set; }
    private UnityEngine.Events.UnityAction<string> deselectCallback;
    private bool bound;
    private int swallowValidateFrame = -1;
    private char swallowValidateChar;

    public string text { get => Field != null ? Field.text : string.Empty; set { if (Field != null) Field.text = value; } }
    public bool isFocused => Field != null && Field.isFocused;
    public int selectionAnchorPosition { get => Field != null ? Field.selectionAnchorPosition : 0; set { if (Field != null) Field.selectionAnchorPosition = value; } }
    public int selectionFocusPosition { get => Field != null ? Field.selectionFocusPosition : 0; set { if (Field != null) Field.selectionFocusPosition = value; } }
    public int stringPosition { get => Field != null ? Field.stringPosition : 0; set { if (Field != null) Field.stringPosition = value; } }
    public bool richText { get => Field != null && Field.richText; set { if (Field != null) Field.richText = value; } }
    public bool readOnly { get => Field != null && Field.readOnly; set { if (Field != null) Field.readOnly = value; } }
    public int caretWidth { get => Field != null ? Field.caretWidth : 1; set { if (Field != null) Field.caretWidth = value; } }
    public bool customCaretColor { get => Field != null && Field.customCaretColor; set { if (Field != null) Field.customCaretColor = value; } }
    public Color caretColor { get => Field != null ? Field.caretColor : Color.white; set { if (Field != null) Field.caretColor = value; } }
    public Color selectionColor { get => Field != null ? Field.selectionColor : Color.clear; set { if (Field != null) Field.selectionColor = value; } }
    public TMP_Text textComponent => Field != null ? Field.textComponent : null;
    public RectTransform textViewport => Field != null ? Field.textViewport : null;
    public bool onFocusSelectAll { get => Field != null && Field.onFocusSelectAll; set { if (Field != null) Field.onFocusSelectAll = value; } }
    public TMP_InputField.OnChangeEvent onValueChanged => Field != null ? Field.onValueChanged : null;
    public void ActivateInputField() => Field?.ActivateInputField();
    public void ForceLabelUpdate() => Field?.ForceLabelUpdate();

    private void Bind(TMP_InputField field) {
        Field = field;
        if (bound || field == null) {
            return;
        }
        bound = true;
        field.onFocusSelectAll = false;
        field.navigation = new Navigation { mode = Navigation.Mode.None };
        deselectCallback = new Action<string>(_ => {
            hasLastEdit = false;
            completionLocked = false;
        });
        field.onDeselect.AddListener(deselectCallback);
        field.onValidateInput = (TMP_InputField.OnValidateInput)new Func<string, int, char, char>(OnValidateKeyChar);

        var trigger = gameObject.GetComponent<EventTrigger>() ?? gameObject.AddComponent<EventTrigger>();
        O5Kit.Behaviour.UnityUtils.AddEvent(trigger, EventTriggerType.PointerDown, e => {
            var ped = O5Kit.Behaviour.UnityUtils.AsPointerEventData(e);
            if (ped != null && ped.clickCount == 2) {
                OnDoubleClick?.Invoke();
            }
        });
        SubscribeHistory();
    }

    private char OnValidateKeyChar(string current, int charIndex, char addedChar) {
        if (swallowValidateFrame == Time.frameCount && (addedChar == swallowValidateChar || (swallowValidateChar == '\n' && addedChar == '\r'))) {
            return '\0';
        }
        // EventSystem.Update can invoke OnUpdateSelected before this MonoBehaviour's Update.
        if (addedChar is '\n' or '\r' && isFocused && string.IsNullOrEmpty(O5Input.CompositionString)
            && (O5Input.GetKeyDown(KeyCode.Return) || O5Input.GetKeyDown(KeyCode.KeypadEnter))) {
            KeyCode key = O5Input.GetKeyDown(KeyCode.KeypadEnter) ? KeyCode.KeypadEnter : KeyCode.Return;
            if (HandleKey?.Invoke(key) == true) {
                swallowValidateFrame = Time.frameCount;
                swallowValidateChar = '\n';
                return '\0';
            }
        }
        return addedChar;
    }

    private void SubscribeHistory() {
        if (Field == null) {
            return;
        }
        Field.onFocusSelectAll = false;
        lastState = CaptureState();
        hasLastEdit = false;
        historyCallback ??= new Action<string>(OnHistoryValueChanged);
        Field.onValueChanged.RemoveListener(historyCallback);
        Field.onValueChanged.AddListener(historyCallback);
    }

    public void OnEnable() {
        Field ??= GetComponent<TMP_InputField>();
        if (Field != null) {
            Bind(Field);
            SubscribeHistory();
        }
    }

    public void OnDisable() {
        OnFieldDisabled?.Invoke();
        if (Field != null && historyCallback != null) {
            Field.onValueChanged.RemoveListener(historyCallback);
        }
        if (hoveredField == this) {
            hoveredField = null;
        }
        hasLastEdit = false;
        completionLocked = false;
    }

    public void OnDestroy() {
        if (Field != null && deselectCallback != null) {
            Field.onDeselect.RemoveListener(deselectCallback);
        }
        OnFieldDestroyed?.Invoke();
    }

    public void LateUpdate() {
        if (Field == null) {
            return;
        }
        RestoreLockedCaretIfNavigating();
        if (!suppressHistory && text == lastState.Text) {
            HistoryState current = CaptureState();
            if (current.Anchor != lastState.Anchor || current.Focus != lastState.Focus) {
                lastState = current;
                hasLastEdit = false;
            }
        }
        SyncCaretTransform();
        HandleShortcuts();
        if (textComponent != null) {
            AfterLabelUpdate?.Invoke(textComponent, isFocused && !string.IsNullOrEmpty(O5Input.CompositionString));
        }
        UpdateCompletionLock();
    }

    private void Update() {
        if (Field != null && isFocused && string.IsNullOrEmpty(O5Input.CompositionString)
            && swallowValidateFrame != Time.frameCount) {
            KeyCode key = O5Input.GetKeyDown(KeyCode.Tab) ? KeyCode.Tab
                : O5Input.GetKeyDown(KeyCode.Return) ? KeyCode.Return
                : O5Input.GetKeyDown(KeyCode.KeypadEnter) ? KeyCode.KeypadEnter
                : O5Input.GetKeyDown(KeyCode.UpArrow) ? KeyCode.UpArrow
                : O5Input.GetKeyDown(KeyCode.DownArrow) ? KeyCode.DownArrow
                : O5Input.GetKeyDown(KeyCode.Escape) ? KeyCode.Escape
                : KeyCode.None;
            if (key != KeyCode.None && HandleKey?.Invoke(key) == true) {
                swallowValidateFrame = Time.frameCount;
                swallowValidateChar = key == KeyCode.Tab ? '\t' : (key is KeyCode.Return or KeyCode.KeypadEnter ? '\n' : '\0');
            }
        }

        if (hoveredField == null && textViewport != null && RectTransformUtility.RectangleContainsScreenPoint(
            textViewport,
            O5Input.MousePosition,
            null
        )) {
            hoveredField = this;
        }

        if (hoveredField != this || !IsShiftHeld()) {
            return;
        }

        Vector2 delta = O5Input.MouseScrollDelta;
        float wheel = Mathf.Abs(delta.y) > 0.01f ? delta.y : delta.x;
        if (Mathf.Abs(wheel) > 0.0001f) {
            ScrollHorizontal(wheel * 32f);
        }
    }
#else
    protected override void OnEnable() {
        base.OnEnable();
        onFocusSelectAll = false;
        navigation = new Navigation { mode = Navigation.Mode.None };
        lastState = CaptureState();
        hasLastEdit = false;
        completionLocked = false;
#pragma warning disable IDE0074
        if (historyCallback == null) {
            historyCallback = OnHistoryValueChanged;
        }
#pragma warning restore IDE0074
        onValueChanged.AddListener(historyCallback);
    }

    protected override void OnDisable() {
        OnFieldDisabled?.Invoke();
        if (historyCallback != null) {
            onValueChanged.RemoveListener(historyCallback);
        }
        if (hoveredField == this) {
            hoveredField = null;
        }
        hasLastEdit = false;
        completionLocked = false;
        base.OnDisable();
    }

    protected override void OnDestroy() {
        OnFieldDestroyed?.Invoke();
        base.OnDestroy();
    }

    protected override void LateUpdate() {
        base.LateUpdate();
        RestoreLockedCaretIfNavigating();
        if (!suppressHistory && text == lastState.Text) {
            HistoryState current = CaptureState();
            if (current.Anchor != lastState.Anchor || current.Focus != lastState.Focus) {
                lastState = current;
                hasLastEdit = false;
            }
        }
        SyncCaretTransform();
        HandleShortcuts();
        if (textComponent != null) {
            AfterLabelUpdate?.Invoke(textComponent, isFocused && !string.IsNullOrEmpty(O5Input.CompositionString));
        }
        UpdateCompletionLock();
    }

    public override void OnUpdateSelected(BaseEventData eventData) {
        if (isFocused && string.IsNullOrEmpty(O5Input.CompositionString)) {
            KeyCode key = O5Input.GetKeyDown(KeyCode.Tab) ? KeyCode.Tab
                : O5Input.GetKeyDown(KeyCode.Return) ? KeyCode.Return
                : O5Input.GetKeyDown(KeyCode.KeypadEnter) ? KeyCode.KeypadEnter
                : O5Input.GetKeyDown(KeyCode.UpArrow) ? KeyCode.UpArrow
                : O5Input.GetKeyDown(KeyCode.DownArrow) ? KeyCode.DownArrow
                : O5Input.GetKeyDown(KeyCode.Escape) ? KeyCode.Escape
                : KeyCode.None;

            bool consumed = key != KeyCode.None && HandleKey?.Invoke(key) == true;
            if (!consumed && IsCompletionActive?.Invoke() == true
                && (O5Input.GetKey(KeyCode.UpArrow) || O5Input.GetKey(KeyCode.DownArrow))) {
                consumed = true;
            }
            if (consumed) {
                // Drain queued key-repeat events and mark handled so StandaloneInputModule
                // never fires Selectable.OnMove and base TMP_InputField never moves the caret.
                while (Event.PopEvent(drainEvent)) { }
                eventData?.Use();
                return;
            }
        }

        base.OnUpdateSelected(eventData);
    }

    private void Update() {
        if (hoveredField == null && textViewport != null && RectTransformUtility.RectangleContainsScreenPoint(
            textViewport,
            O5Input.MousePosition,
            null
        )) {
            hoveredField = this;
        }

        if (hoveredField != this || !IsShiftHeld()) {
            return;
        }

        Vector2 delta = O5Input.MouseScrollDelta;
        float wheel = Mathf.Abs(delta.y) > 0.01f ? delta.y : delta.x;
        if (Mathf.Abs(wheel) > 0.0001f) {
            ScrollHorizontal(wheel * 32f);
        }
    }

    public override void OnPointerDown(PointerEventData eventData) {
        base.OnPointerDown(eventData);
        if (eventData.clickCount == 2) {
            OnDoubleClick?.Invoke();
        }
    }

    public override void OnDeselect(BaseEventData eventData) {
        base.OnDeselect(eventData);
        hasLastEdit = false;
        completionLocked = false;
    }
#endif

    internal void RestoreFocusWithCaret(int anchor, int focus) {
        ActivateInputField();
        int len = (text ?? string.Empty).Length;
        int a = O5Kit.Core.MathCompat.Clamp(anchor, 0, len);
        int f = O5Kit.Core.MathCompat.Clamp(focus, 0, len);
        stringPosition = f;
        selectionAnchorPosition = a;
        selectionFocusPosition = f;
        ForceLabelUpdate();
    }

    private void RestoreLockedCaretIfNavigating() {
        bool navKey = O5Input.GetKey(KeyCode.UpArrow) || O5Input.GetKey(KeyCode.DownArrow)
            || O5Input.GetKeyDown(KeyCode.UpArrow) || O5Input.GetKeyDown(KeyCode.DownArrow)
            || O5Input.GetKeyUp(KeyCode.UpArrow) || O5Input.GetKeyUp(KeyCode.DownArrow);
        if (!completionLocked || !navKey || !isFocused || text != lockedText) {
            return;
        }

        bool caretMoved = selectionAnchorPosition != lockedAnchor
            || selectionFocusPosition != lockedFocus
            || stringPosition != lockedFocus;
        bool scrollMoved = textComponent != null
            && (textComponent.rectTransform.anchoredPosition - lockedScrollPos).sqrMagnitude > 0.001f;
        if (!caretMoved && !scrollMoved) {
            return;
        }

        stringPosition = lockedFocus;
        selectionAnchorPosition = lockedAnchor;
        selectionFocusPosition = lockedFocus;
        if (textComponent != null) {
            textComponent.rectTransform.anchoredPosition = lockedScrollPos;
        }
        ForceLabelUpdate();
    }

    private void UpdateCompletionLock() {
        completionLocked = isFocused && IsCompletionActive?.Invoke() == true;
        if (completionLocked) {
            lockedText = text ?? string.Empty;
            lockedAnchor = selectionAnchorPosition;
            lockedFocus = selectionFocusPosition;
            lockedScrollPos = textComponent != null ? textComponent.rectTransform.anchoredPosition : Vector2.zero;
        }
    }

    private static bool IsShiftHeld()
        => O5Input.GetKey(KeyCode.LeftShift) || O5Input.GetKey(KeyCode.RightShift);

    public void Undo() {
        if (!isFocused || undoHistory.Count == 0) {
            return;
        }

        int last = undoHistory.Count - 1;
        HistoryState previous = undoHistory[last];
        undoHistory.RemoveAt(last);
        redoHistory.Add(CaptureState());
        SetHistoryValue(previous);
    }

    public void Redo() {
        if (!isFocused || redoHistory.Count == 0) {
            return;
        }

        int last = redoHistory.Count - 1;
        HistoryState next = redoHistory[last];
        redoHistory.RemoveAt(last);
        undoHistory.Add(CaptureState());
        SetHistoryValue(next);
    }

    private void OnHistoryValueChanged(string value) {
        HistoryState current = CaptureState(value);
        if (suppressHistory || current.Text == lastState.Text) {
            return;
        }

        EditInfo edit = DescribeEdit(lastState, current);
        if (!CanMerge(edit)) {
            PushUndo(lastState);
            lastEdit = edit;
            hasLastEdit = true;
        } else {
            lastEdit = MergeEdits(lastEdit, edit);
        }
        redoHistory.Clear();
        lastState = current;
    }

    private void SetHistoryValue(HistoryState state) {
        suppressHistory = true;
        text = state.Text;
        suppressHistory = false;
        RestoreSelection(state);
        ForceLabelUpdate();
        SyncCaretTransform();
        lastState = state;
        hasLastEdit = false;
    }

    private HistoryState CaptureState(string value = null) {
        string current = value ?? text ?? string.Empty;
        int anchor = O5Kit.Core.MathCompat.Clamp(selectionAnchorPosition, 0, current.Length);
        int focus = O5Kit.Core.MathCompat.Clamp(selectionFocusPosition, 0, current.Length);
        return new HistoryState(current, anchor, focus);
    }

    private void RestoreSelection(HistoryState state) {
        int length = state.Text?.Length ?? 0;
        selectionAnchorPosition = O5Kit.Core.MathCompat.Clamp(state.Anchor, 0, length);
        selectionFocusPosition = O5Kit.Core.MathCompat.Clamp(state.Focus, 0, length);
    }

    private void PushUndo(HistoryState state) {
        undoHistory.Add(state);
        if (undoHistory.Count > MaxHistory) {
            undoHistory.RemoveAt(0);
        }
    }

    private bool CanMerge(EditInfo edit) {
        if (!hasLastEdit || edit.Kind != lastEdit.Kind || edit.Class != lastEdit.Class ||
           !edit.CanCoalesce || !lastEdit.CanCoalesce || edit.Class is EditClass.Punctuation or EditClass.Mixed) {
            return false;
        }

        if (edit.Kind == EditKind.Insert) {
            return edit.Start == lastEdit.Start + lastEdit.Length;
        }

        return edit.Direction == lastEdit.Direction &&
            (edit.Direction == EditDirection.Backward
                ? edit.Start + edit.Length == lastEdit.Start
                : edit.Start == lastEdit.Start);
    }

    private static EditInfo MergeEdits(EditInfo previous, EditInfo current) {
        int start = previous.Kind == EditKind.Delete && previous.Direction == EditDirection.Backward
            ? current.Start
            : previous.Start;
        return new EditInfo(
            previous.Kind,
            previous.Direction,
            start,
            previous.Length + current.Length,
            previous.Class,
            true
        );
    }

    private static EditInfo DescribeEdit(HistoryState before, HistoryState after) {
        string oldText = before.Text;
        string newText = after.Text;
        int prefix = 0;
        int sharedLength = Math.Min(oldText.Length, newText.Length);
        while (prefix < sharedLength && oldText[prefix] == newText[prefix]) {
            prefix++;
        }

        int oldEnd = oldText.Length - 1;
        int newEnd = newText.Length - 1;
        while (oldEnd >= prefix && newEnd >= prefix && oldText[oldEnd] == newText[newEnd]) {
            oldEnd--;
            newEnd--;
        }

        string removed = oldEnd >= prefix ? oldText[prefix..(oldEnd + 1)] : string.Empty;
        string inserted = newEnd >= prefix ? newText[prefix..(newEnd + 1)] : string.Empty;
        if (removed.Length == 0 && inserted.Length > 0) {
            return new EditInfo(
                EditKind.Insert,
                EditDirection.Forward,
                prefix,
                inserted.Length,
                Classify(inserted),
                inserted.Length == 1
            );
        }

        if (inserted.Length == 0 && removed.Length > 0) {
            EditDirection direction = after.Focus < before.Focus
                ? EditDirection.Backward
                : EditDirection.Forward;
            return new EditInfo(
                EditKind.Delete,
                direction,
                prefix,
                removed.Length,
                Classify(removed),
                removed.Length == 1
            );
        }

        return new EditInfo(
            EditKind.Replace,
            EditDirection.Forward,
            prefix,
            Math.Max(removed.Length, inserted.Length),
            EditClass.Mixed,
            false
        );
    }

    private static EditClass Classify(string value) {
        bool word = true;
        bool whitespace = true;
        foreach (char character in value) {
            if (char.IsLetterOrDigit(character) || character == '_') {
                whitespace = false;
            } else if (char.IsWhiteSpace(character)) {
                word = false;
            } else {
                word = false;
                whitespace = false;
            }
        }

        return word ? EditClass.Word : whitespace ? EditClass.Whitespace : EditClass.Punctuation;
    }

    private readonly struct HistoryState(string text, int anchor, int focus) {
        public readonly string Text = text ?? string.Empty;
        public readonly int Anchor = anchor;
        public readonly int Focus = focus;
    }

    private enum EditKind {
        Insert,
        Delete,
        Replace,
    }

    private enum EditDirection {
        Forward,
        Backward,
    }

    private enum EditClass {
        Word,
        Whitespace,
        Punctuation,
        Mixed,
    }

    private readonly struct EditInfo(
        EditKind kind,
        EditDirection direction,
        int start,
        int length,
        EditClass @class,
        bool canCoalesce
    ) {
        public readonly EditKind Kind = kind;
        public readonly EditDirection Direction = direction;
        public readonly int Start = start;
        public readonly int Length = length;
        public readonly EditClass Class = @class;
        public readonly bool CanCoalesce = canCoalesce;
    }

    private void HandleShortcuts() {
        if (!isFocused || !string.IsNullOrEmpty(O5Input.CompositionString) ||
           (!O5Input.GetKey(KeyCode.LeftControl) && !O5Input.GetKey(KeyCode.RightControl) &&
           !O5Input.GetKey(KeyCode.LeftCommand) && !O5Input.GetKey(KeyCode.RightCommand))) {
            return;
        }

        bool shift = O5Input.GetKey(KeyCode.LeftShift) || O5Input.GetKey(KeyCode.RightShift);
        if (O5Input.GetKeyDown(KeyCode.Z)) {
            if (shift) {
                Redo();
            } else {
                Undo();
            }
        } else if (O5Input.GetKeyDown(KeyCode.Y)) {
            Redo();
        }
    }

    private void ScrollHorizontal(float amount) {
        if (textComponent == null || textViewport == null) {
            return;
        }

        textComponent.ForceMeshUpdate();
        float contentWidth = Mathf.Max(
            textComponent.rectTransform.rect.width,
            textComponent.preferredWidth + textComponent.margin.x + textComponent.margin.z
        );
        float max = Mathf.Max(0f, contentWidth - textViewport.rect.width);
        Vector2 position = textComponent.rectTransform.anchoredPosition;
        position.x = Mathf.Clamp(position.x + amount, -max, 0f);
        textComponent.rectTransform.anchoredPosition = position;
        SyncCaretTransform();
    }

    private void SyncCaretTransform() {
        if (textComponent == null || textViewport == null) {
            return;
        }

        caretTransform ??= O5Kit.Behaviour.UnityUtils.AsRectTransform(textViewport.Find("Caret"));
        if (caretTransform == null) {
            return;
        }

        RectTransform textTransform = textComponent.rectTransform;
        caretTransform.localPosition = textTransform.localPosition;
        caretTransform.localRotation = textTransform.localRotation;
        caretTransform.localScale = textTransform.localScale;
        caretTransform.anchorMin = textTransform.anchorMin;
        caretTransform.anchorMax = textTransform.anchorMax;
        caretTransform.anchoredPosition = textTransform.anchoredPosition;
        caretTransform.sizeDelta = textTransform.sizeDelta;
        caretTransform.pivot = textTransform.pivot;
    }
}
