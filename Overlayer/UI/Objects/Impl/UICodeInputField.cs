using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using Overlayer.Compat.OVC;

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
    (IntPtr ptr) : TMP_InputField(ptr), IPointerEnterHandler, IPointerExitHandler
#else
    : TMP_InputField, IPointerEnterHandler, IPointerExitHandler
#endif
{
    public Action<TMP_Text, bool> AfterLabelUpdate;
    public Func<KeyCode, bool> HandleKey;
    public Action OnFieldDisabled;
    public bool CanUndo => undoHistory.Count > 0;
    public bool CanRedo => redoHistory.Count > 0;

    private readonly Stack<HistoryState> undoHistory = [];
    private readonly Stack<HistoryState> redoHistory = [];
    private HistoryState lastState;
    private EditInfo lastEdit;
    private bool hasLastEdit;
    private bool suppressHistory;
#if ML && IL2CPP
    private UnityEngine.Events.UnityAction<string> historyCallback;
#else
    private UnityAction<string> historyCallback;
#endif
    private const int MaxHistory = 100;
    private static UICodeInputField hoveredField;
    private RectTransform caretTransform;

    public static bool ShouldConsumeParentScroll
        => hoveredField != null && IsShiftHeld();

    protected override void OnEnable() {
        base.OnEnable();
        onFocusSelectAll = false;
        lastState = CaptureState();
        hasLastEdit = false;
        if(historyCallback == null) {
            historyCallback =
#if ML && IL2CPP
                DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<string>>(new Action<string>(OnHistoryValueChanged));
#else
                OnHistoryValueChanged;
#endif
        }
        onValueChanged.AddListener(historyCallback);
    }

    protected override void OnDisable() {
        OnFieldDisabled?.Invoke();
        if(historyCallback != null) {
            onValueChanged.RemoveListener(historyCallback);
        }
        if(hoveredField == this) {
            hoveredField = null;
        }
        hasLastEdit = false;
        base.OnDisable();
    }

    protected override void LateUpdate() {
        base.LateUpdate();
        if(!suppressHistory && text == lastState.Text) {
            HistoryState current = CaptureState();
            if(current.Anchor != lastState.Anchor || current.Focus != lastState.Focus) {
                lastState = current;
                hasLastEdit = false;
            }
        }
        SyncCaretTransform();
        HandleShortcuts();
        if(textComponent != null) {
            AfterLabelUpdate?.Invoke(textComponent, isFocused && !string.IsNullOrEmpty(Input.compositionString));
        }
    }

    public override void OnUpdateSelected(BaseEventData eventData) {
        if(isFocused && string.IsNullOrEmpty(Input.compositionString)) {
            KeyCode key = OVC_Input.GetKeyDown(KeyCode.Tab) ? KeyCode.Tab
                : OVC_Input.GetKeyDown(KeyCode.Return) ? KeyCode.Return
                : OVC_Input.GetKeyDown(KeyCode.KeypadEnter) ? KeyCode.KeypadEnter
                : OVC_Input.GetKeyDown(KeyCode.UpArrow) ? KeyCode.UpArrow
                : OVC_Input.GetKeyDown(KeyCode.DownArrow) ? KeyCode.DownArrow
                : OVC_Input.GetKeyDown(KeyCode.Escape) ? KeyCode.Escape
                : KeyCode.None;

            if(key != KeyCode.None && HandleKey?.Invoke(key) == true) {
                return;
            }
        }

        base.OnUpdateSelected(eventData);
    }

    private void Update() {
        if(hoveredField == null && textViewport != null && RectTransformUtility.RectangleContainsScreenPoint(
            textViewport,
            OVC_Input.MousePosition,
            null
        )) {
            hoveredField = this;
        }

        if(hoveredField != this || !IsShiftHeld()) return;
        Vector2 delta = OVC_Input.MouseScrollDelta;
        float wheel = Mathf.Abs(delta.y) > 0.01f ? delta.y : delta.x;
        if(Mathf.Abs(wheel) > 0.0001f) ScrollHorizontal(wheel * 32f);
    }

    public override void OnPointerEnter(PointerEventData eventData) {
        base.OnPointerEnter(eventData);
        hoveredField = this;
    }

    public override void OnPointerExit(PointerEventData eventData) {
        base.OnPointerExit(eventData);
        if(hoveredField == this) hoveredField = null;
    }

    public override void OnDeselect(BaseEventData eventData) {
        base.OnDeselect(eventData);
        hasLastEdit = false;
    }

    private static bool IsShiftHeld()
        => OVC_Input.GetKey(KeyCode.LeftShift) || OVC_Input.GetKey(KeyCode.RightShift);

    public void Undo() {
        if(!isFocused || undoHistory.Count == 0) return;
        HistoryState previous = undoHistory.Pop();
        redoHistory.Push(CaptureState());
        SetHistoryValue(previous);
    }

    public void Redo() {
        if(!isFocused || redoHistory.Count == 0) return;
        HistoryState next = redoHistory.Pop();
        undoHistory.Push(CaptureState());
        SetHistoryValue(next);
    }

    private void OnHistoryValueChanged(string value) {
        HistoryState current = CaptureState(value);
        if(suppressHistory || current.Text == lastState.Text) return;
        EditInfo edit = DescribeEdit(lastState, current);
        if(!CanMerge(edit)) {
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
        int anchor = Math.Clamp(selectionAnchorPosition, 0, current.Length);
        int focus = Math.Clamp(selectionFocusPosition, 0, current.Length);
        return new HistoryState(current, anchor, focus);
    }

    private void RestoreSelection(HistoryState state) {
        int length = state.Text?.Length ?? 0;
        selectionAnchorPosition = Math.Clamp(state.Anchor, 0, length);
        selectionFocusPosition = Math.Clamp(state.Focus, 0, length);
    }

    private void PushUndo(HistoryState state) {
        undoHistory.Push(state);
        if(undoHistory.Count <= MaxHistory) return;

        var values = undoHistory.Take(MaxHistory).ToArray();
        undoHistory.Clear();
        for(int i = values.Length - 1; i >= 0; i--) {
            undoHistory.Push(values[i]);
        }
    }

    private bool CanMerge(EditInfo edit) {
        if(!hasLastEdit || edit.Kind != lastEdit.Kind || edit.Class != lastEdit.Class ||
           !edit.CanCoalesce || !lastEdit.CanCoalesce || edit.Class is EditClass.Punctuation or EditClass.Mixed) {
            return false;
        }

        if(edit.Kind == EditKind.Insert) {
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
        while(prefix < sharedLength && oldText[prefix] == newText[prefix]) prefix++;

        int oldEnd = oldText.Length - 1;
        int newEnd = newText.Length - 1;
        while(oldEnd >= prefix && newEnd >= prefix && oldText[oldEnd] == newText[newEnd]) {
            oldEnd--;
            newEnd--;
        }

        string removed = oldEnd >= prefix ? oldText[prefix..(oldEnd + 1)] : string.Empty;
        string inserted = newEnd >= prefix ? newText[prefix..(newEnd + 1)] : string.Empty;
        if(removed.Length == 0 && inserted.Length > 0) {
            return new EditInfo(
                EditKind.Insert,
                EditDirection.Forward,
                prefix,
                inserted.Length,
                Classify(inserted),
                inserted.Length == 1
            );
        }

        if(inserted.Length == 0 && removed.Length > 0) {
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
        foreach(char character in value) {
            if(char.IsLetterOrDigit(character) || character == '_') {
                whitespace = false;
            } else if(char.IsWhiteSpace(character)) {
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
        if(!isFocused || !string.IsNullOrEmpty(Input.compositionString) ||
           !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl) &&
           !Input.GetKey(KeyCode.LeftCommand) && !Input.GetKey(KeyCode.RightCommand)) {
            return;
        }

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if(Input.GetKeyDown(KeyCode.Z)) {
            if(shift) Redo();
            else Undo();
        } else if(Input.GetKeyDown(KeyCode.Y)) {
            Redo();
        }
    }

    private void ScrollHorizontal(float amount) {
        if(textComponent == null || textViewport == null) return;
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
        if(textComponent == null || textViewport == null) return;
        caretTransform ??= textViewport.Find("Caret") as RectTransform;
        if(caretTransform == null) return;

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
