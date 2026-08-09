using FuzzySharp;
using Overlayer.Compat.OVC;
using Overlayer.Tag.Core;
using Overlayer.UI.Generator;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static UnityEngine.EventSystems.PointerEventData;
using Overlayer.UI.Utility;

#if ML && IL2CPP
using MelonLoader;
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

internal sealed class TagCompletionPopup {
    private const int MaxItems = 8;
    private const float ItemHeight = 26f;
    private const float PopupWidth = 360f;

    private readonly UICodeInputField input;
    private readonly RectTransform canvasRect;
    private readonly TMP_Text sourceText;
    private readonly RectTransform popupRect;
    private readonly CompletionRow[] rows;
    private readonly List<TagCore> matches = [];
    private readonly CompletionInputHandler inputHandler;

    private int selectedIndex;
    private int windowStart;
    private int replacementStart;
    private int replacementLength;
    private int visibleRowCount;
    private readonly List<SnippetStop> snippetStops = [];
    private int snippetIndex = -1;
    private string snippetText;
    private bool visible;
    private bool suppressRefresh;
    private string suppressedText;
    private int suppressedCaret;
    private int hoveredIndex = -1;

    public TagCompletionPopup(UICodeInputField input, TMP_Text sourceText) {
        this.input = input;
        this.sourceText = sourceText;

        canvasRect = sourceText.canvas?.rootCanvas?.GetComponent<RectTransform>()
            ?? UICore.CanvasObj?.GetComponent<RectTransform>();

        input.OnFieldDisabled = Deactivate;
        input.OnFieldDestroyed = Dispose;

        GameObject popup = new("TagCompletion");
        popup.transform.SetParent(canvasRect, false);
        popup.transform.SetAsLastSibling();

        popupRect = popup.AddComponent<RectTransform>();
        popupRect.anchorMin = new(0.5f, 0.5f);
        popupRect.anchorMax = new(0.5f, 0.5f);
        popupRect.pivot = new(0f, 1f);
        popupRect.sizeDelta = new(PopupWidth, MaxItems * ItemHeight);

        inputHandler = popup.AddComponent<CompletionInputHandler>();
        inputHandler.Initialize(this);

        Image popupImage = popup.AddComponent<Image>();
        popupImage.color = new Color(0.10f, 0.10f, 0.14f, 0.98f);

        rows = new CompletionRow[MaxItems];

        for(int i = 0; i < MaxItems; i++) {
            rows[i] = CreateRow(popup.transform, i);
        }

        popup.SetActive(false);
    }

    public bool HandleKey(KeyCode key) {
        if(HasSnippet) {
            if(key == KeyCode.Tab) {
                AdvanceSnippet(IsShiftHeld());
                return true;
            }

            if(key == KeyCode.Escape) {
                ClearSnippet();
                Hide();
                return true;
            }
        }

        if(!visible || matches.Count == 0) {
            return false;
        }

        switch(key) {
            case KeyCode.Tab:
                Accept(selectedIndex);
                return true;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                Accept(selectedIndex);
                return true;

            case KeyCode.UpArrow:
                MoveSelection(-1);
                return true;

            case KeyCode.DownArrow:
                MoveSelection(1);
                return true;

            case KeyCode.Escape:
                Hide();
                return true;

            default:
                return false;
        }
    }

    public void Refresh(bool composing) {
        if(canvasRect == null || popupRect == null || !canvasRect.gameObject) {
            return;
        }

        bool focused = input.isFocused || EventSystem.current?.currentSelectedGameObject == input.gameObject;
        if(!focused) {
            ClearSnippet();
            Hide();
            return;
        }

        UpdateSnippetAfterEdit();
        if(composing || (input.selectionAnchorPosition != input.selectionFocusPosition && !HasSnippet)) {
            Hide();
            return;
        }

        string text = input.text ?? string.Empty;
        int caret = Math.Clamp(input.selectionFocusPosition, 0, text.Length);
        if(suppressRefresh) {
            bool sameState = text == suppressedText && caret == suppressedCaret;
            suppressRefresh = false;
            if(sameState) {
                Hide();
                return;
            }
        }

        if(!TryGetContext(text, caret, out string query, out int start)) {
            Hide();
            return;
        }

        replacementStart = start;
        replacementLength = caret - start;
        RebuildMatches(query);
        if(matches.Count == 0) {
            Hide();
            return;
        }

        visible = true;
        popupRect.gameObject.SetActive(true);
        popupRect.SetAsLastSibling();
        PositionPopup(caret);
        UpdateRows();
    }

    private CompletionRow CreateRow(Transform parent, int index) {
        GameObject row = new($"Completion_{index}");
        row.transform.SetParent(parent, false);

        RectTransform rect = row.AddComponent<RectTransform>();
        rect.anchorMin = new(0f, 1f);
        rect.anchorMax = new(1f, 1f);
        rect.pivot = new(0.5f, 1f);
        rect.offsetMin = new(0f, -(index + 1) * ItemHeight);
        rect.offsetMax = new(0f, -index * ItemHeight);

        Image image = row.AddComponent<Image>();
        image.color = Color.clear;

        TextMeshProUGUI name = GenerateUI.AddText(row.transform, true);
        name.font = sourceText.font;
        name.fontSize = 14f;
        name.alignment = TextAlignmentOptions.Left;
        name.verticalAlignment = VerticalAlignmentOptions.Middle;
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.overflowMode = TextOverflowModes.Ellipsis;
        name.rectTransform.offsetMin = new(10f, 0f);
        name.rectTransform.offsetMax = new(-150f, 0f);
        name.raycastTarget = false;

        TextMeshProUGUI detail = GenerateUI.AddText(row.transform, true);
        detail.font = sourceText.font;
        detail.fontSize = 11f;
        detail.alignment = TextAlignmentOptions.Right;
        detail.verticalAlignment = VerticalAlignmentOptions.Middle;
        detail.textWrappingMode = TextWrappingModes.NoWrap;
        detail.overflowMode = TextOverflowModes.Ellipsis;
        detail.color = new Color(1f, 1f, 1f, 0.48f);
        detail.rectTransform.offsetMin = new(150f, 0f);
        detail.rectTransform.offsetMax = new(-10f, 0f);
        detail.raycastTarget = false;

        GenerateUI.AddButton(row, button => {
            if(button == InputButton.Left) {
                Accept(windowStart + index);
            }
        });

        EventTrigger trigger = row.AddComponent<EventTrigger>();

        UnityUtils.AddEvents(
            trigger,
            (EventTriggerType.PointerEnter, _ => {
                SetHoveredRow(index);
            }
        ),
            (EventTriggerType.PointerExit, _ => {
                ClearHoveredRow(index);
            }
        )
        );

        return new CompletionRow(rect, image, name, detail);
    }

    private void SetHoveredRow(int rowIndex) {
        if(!visible) {
            return;
        }

        int matchIndex = windowStart + rowIndex;

        if(rowIndex < 0 ||
           rowIndex >= visibleRowCount ||
           matchIndex < 0 ||
           matchIndex >= matches.Count) {
            return;
        }

        hoveredIndex = matchIndex;
        UpdateRows();
    }

    private void ClearHoveredRow(int rowIndex) {
        int matchIndex = windowStart + rowIndex;

        if(hoveredIndex != matchIndex) {
            return;
        }

        hoveredIndex = -1;
        UpdateRows();
    }

    private void RebuildMatches(string query) {
        string previousSelection = selectedIndex >= 0 && selectedIndex < matches.Count
            ? matches[selectedIndex].Name
            : null;

        matches.Clear();
        foreach(TagCore tag in TagManager.GetAllTags()) {
            int score = string.IsNullOrEmpty(query)
                ? 0
                : tag.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                    ? 1000 - tag.Name.Length
                    : Fuzz.WeightedRatio(query, tag.Name);

            if(string.IsNullOrEmpty(query) || score >= 45) {
                matches.Add(tag);
            }
        }

        matches.Sort((left, right) => {
            int leftScore = GetScore(query, left.Name);
            int rightScore = GetScore(query, right.Name);
            int score = rightScore.CompareTo(leftScore);
            return score != 0 ? score : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        });

        selectedIndex = 0;
        if(previousSelection != null) {
            int previousIndex = matches.FindIndex(tag => tag.Name == previousSelection);
            if(previousIndex >= 0) {
                selectedIndex = previousIndex;
            }
        }
    }

    private static int GetScore(string query, string name)
        => string.IsNullOrEmpty(query)
            ? 0
            : name.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                ? 1000 - name.Length
                : Fuzz.WeightedRatio(query, name);

    private void UpdateRows() {
        if(matches.Count == 0) {
            return;
        }

        int pageSize = Math.Max(1, visibleRowCount);
        if(selectedIndex < windowStart) {
            windowStart = selectedIndex;
        } else if(selectedIndex >= windowStart + pageSize) {
            windowStart = selectedIndex - pageSize + 1;
        }

        for(int i = 0; i < rows.Length; i++) {
            int matchIndex = windowStart + i;
            bool active = visible && i < visibleRowCount && matchIndex < matches.Count;
            rows[i].Rect.gameObject.SetActive(active);
            if(!active) {
                continue;
            }

            TagCore tag = matches[matchIndex];
            if(matchIndex == selectedIndex) {
                rows[i].Image.color = UIColors.MenuHover;
            } else if(matchIndex == hoveredIndex) {
                Color hoverColor = UIColors.MenuHover;
                hoverColor.a *= 0.35f;
                rows[i].Image.color = hoverColor;
            } else {
                rows[i].Image.color = Color.clear;
            }
            rows[i].Name.text = tag.Name;
            rows[i].Detail.text = FormatSignature(tag);
        }
    }

    private void Accept(int index) {
        if(index < 0 || index >= matches.Count) {
            return;
        }

        string text = input.text ?? string.Empty;
        int start = Math.Clamp(replacementStart, 0, text.Length);
        int end = Math.Clamp(start + replacementLength, start, text.Length);
        TagCore tag = matches[index];
        string name = tag.Name;
        bool hasClosingDelimiter = end < text.Length && text[end] == '}';
        bool hasClosingFunction = end + 1 < text.Length && text[end] == ')' && text[end + 1] == '}';
        string insertion = name;
        List<SnippetStop> stops = [];
        bool functionSyntax = tag.Parameters.Length >= 2;

        if(HasUserParameters(tag)) {
            insertion += functionSyntax ? "(" : ":";
            for(int i = 0; i < tag.Parameters.Length; i++) {
                if(i > 0) {
                    insertion += ",";
                }

                int parameterStart = start + insertion.Length;
                insertion += GetParameterName(tag.Parameters[i], i);
                stops.Add(new SnippetStop(parameterStart, start + insertion.Length));
            }

            if(functionSyntax && !hasClosingFunction) {
                insertion += ")";
            }
        }

        if(!hasClosingDelimiter && (!functionSyntax || !hasClosingFunction)) {
            insertion += "}";
        }

        input.text = text[..start] + insertion + text[end..];
        input.ActivateInputField();

        if(stops.Count > 0) {
            snippetStops.Clear();
            snippetStops.AddRange(stops);
            snippetIndex = 0;
            snippetText = input.text;
            SelectSnippet();
        } else {
            int caret = start + insertion.Length;
            ClearSnippet();
            SetCaret(caret);
        }

        suppressRefresh = true;
        suppressedText = input.text;
        suppressedCaret = input.selectionFocusPosition;
        Hide();
    }

    private void AdvanceSnippet(bool backwards) {
        if(!HasSnippet) {
            return;
        }

        int next = snippetIndex + (backwards ? -1 : 1);
        if(next < 0) {
            SetCaret(snippetStops[0].Start);
            ClearSnippet();
            return;
        }

        if(next >= snippetStops.Count) {
            SnippetStop last = snippetStops[^1];
            int caret = last.End;
            string text = input.text ?? string.Empty;
            while(caret < text.Length && (text[caret] == ')' || text[caret] == '}')) {
                caret++;
            }

            SetCaret(caret);
            ClearSnippet();
            return;
        }

        snippetIndex = next;
        SelectSnippet();
    }

    private void SelectSnippet() {
        if(!HasSnippet) {
            return;
        }

        SnippetStop stop = snippetStops[snippetIndex];
        input.selectionAnchorPosition = stop.Start;
        input.selectionFocusPosition = stop.End;
        input.ForceLabelUpdate();
    }

    private void SetCaret(int caret) {
        int length = input.text?.Length ?? 0;
        caret = Math.Clamp(caret, 0, length);
        input.selectionAnchorPosition = caret;
        input.selectionFocusPosition = caret;
        input.ForceLabelUpdate();
    }

    private void MoveSelection(int delta) {
        if(!visible || matches.Count == 0) {
            return;
        }

        selectedIndex = (selectedIndex + delta + matches.Count) % matches.Count;
        UpdateRows();
    }

    private void UpdateSnippetAfterEdit() {
        if(!HasSnippet || input.text == snippetText) {
            return;
        }

        string currentText = input.text ?? string.Empty;
        SnippetStop active = snippetStops[snippetIndex];
        int caret = Math.Clamp(input.selectionFocusPosition, 0, currentText.Length);
        int delta = currentText.Length - snippetText.Length;
        int expectedEnd = active.End + delta;
        if(caret < active.Start || caret > expectedEnd) {
            ClearSnippet();
            return;
        }

        active.End = caret;
        snippetStops[snippetIndex] = active;
        for(int i = snippetIndex + 1; i < snippetStops.Count; i++) {
            SnippetStop stop = snippetStops[i];
            stop.Start += delta;
            stop.End += delta;
            snippetStops[i] = stop;
        }

        snippetText = currentText;
    }

    private bool HasSnippet
        => snippetIndex >= 0 && snippetIndex < snippetStops.Count;

    private void ClearSnippet() {
        snippetStops.Clear();
        snippetIndex = -1;
        snippetText = null;
    }

    private void Hide() {
        visible = false;
        matches.Clear();
        windowStart = 0;
        visibleRowCount = 0;
        hoveredIndex = -1;
        popupRect?.gameObject.SetActive(false);
    }

    private void Deactivate() {
        ClearSnippet();
        suppressRefresh = false;
        Hide();
    }

    private void Dispose() {
        Hide();
        if(popupRect != null) {
            UnityEngine.Object.Destroy(popupRect.gameObject);
        }
    }

    private void PositionPopup(int caret) {
        int maxRows = Math.Min(matches.Count, MaxItems);
        visibleRowCount = maxRows;
        float height = visibleRowCount * ItemHeight;

        TMP_TextInfo textInfo = sourceText.textInfo;
        Vector3 localPosition = Vector3.zero;

        if(textInfo.characterCount > 0) {
            int characterIndex = Math.Clamp(caret, 0, textInfo.characterCount - 1);
            TMP_CharacterInfo character = textInfo.characterInfo[characterIndex];
            if(caret >= textInfo.characterCount) {
                localPosition = new(character.xAdvance, character.bottomLeft.y - 4f, 0f);
            } else {
                localPosition = new(character.origin, character.bottomLeft.y - 4f, 0f);
            }
        }

        Vector3 worldPosition = sourceText.transform.TransformPoint(localPosition);
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            null,
            out Vector2 canvasPosition
        );

        float width = Math.Min(PopupWidth, Math.Max(120f, canvasRect.rect.width - 8f));
        popupRect.sizeDelta = new(width, height);
        float minX = canvasRect.rect.xMin + 4f;
        float maxX = canvasRect.rect.xMax - width - 4f;
        canvasPosition.x = maxX >= minX
            ? Mathf.Clamp(canvasPosition.x, minX, maxX)
            : minX;

        Vector3 topWorldPosition = sourceText.transform.TransformPoint(new Vector3(
            localPosition.x,
            localPosition.y + 20f,
            localPosition.z
        ));
        Vector2 topScreenPosition = RectTransformUtility.WorldToScreenPoint(null, topWorldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            topScreenPosition,
            null,
            out Vector2 topCanvasPosition
        );

        popupRect.pivot = new(0f, 1f);
        if(canvasPosition.y - height < canvasRect.rect.yMin + 4f &&
           topCanvasPosition.y + height <= canvasRect.rect.yMax - 4f) {
            popupRect.pivot = new(0f, 0f);
            canvasPosition.y = topCanvasPosition.y;
        }

        popupRect.anchoredPosition = canvasPosition;
    }

    private static bool TryGetContext(string text, int caret, out string query, out int start) {
        query = null;
        start = 0;
        if(caret <= 0) {
            return false;
        }

        int opening = text.LastIndexOf('{', caret - 1);
        int closing = text.LastIndexOf('}', caret - 1);
        if(opening < 0 || closing > opening) {
            return false;
        }

        start = opening + 1;
        query = text[start..caret];
        return query.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private static string FormatSignature(TagCore tag) {
        string parameters = tag.Parameters.Length == 0
            ? string.Empty
            : $"({string.Join(", ", tag.Parameters.Select(GetParameterName))})";
        return parameters.Length > 0 ? parameters : tag.ReturnType?.Name ?? string.Empty;
    }

    private static bool HasUserParameters(TagCore tag)
        => tag.Parameters.Length > 0 && (tag.TagType & TagType.Advanced) == 0;

    private static string GetParameterName(ParameterInfo parameter, int index)
        => string.IsNullOrEmpty(parameter?.Name) ? $"arg{index + 1}" : parameter.Name;

    private static bool IsShiftHeld()
        => OVC_Input.GetKey(KeyCode.LeftShift) || OVC_Input.GetKey(KeyCode.RightShift);

    private struct SnippetStop(int start, int end) {
        public int Start = start;
        public int End = end;
    }

    private readonly struct CompletionRow(
        RectTransform rect,
        Image image,
        TextMeshProUGUI name,
        TextMeshProUGUI detail
    ) {
        public readonly RectTransform Rect = rect;
        public readonly Image Image = image;
        public readonly TextMeshProUGUI Name = name;
        public readonly TextMeshProUGUI Detail = detail;
    }

#if ML && IL2CPP
[RegisterTypeInIl2Cpp]
#endif
    private sealed class CompletionInputHandler
#if ML && IL2CPP
    (IntPtr ptr) : MonoBehaviour(ptr)
#else
        : MonoBehaviour
#endif
    {
        private TagCompletionPopup popup;

        private KeyCode repeatKey;
        private float nextRepeatTime;
        private bool repeating;

        private const float InitialDelay = 0.35f;
        private const float RepeatInterval = 0.05f;

        public void Initialize(TagCompletionPopup popup) => this.popup = popup;

        private void Update() {
            if(popup == null || !popup.visible || popup.matches.Count == 0) {
                repeating = false;
                return;
            }

            KeyCode key = GetHeldNavigationKey();

            if(key == KeyCode.None) {
                repeating = false;
                return;
            }

            if(!repeating || repeatKey != key) {
                repeatKey = key;
                repeating = true;
                nextRepeatTime = Time.unscaledTime + InitialDelay;
                return;
            }

            float now = Time.unscaledTime;
            if(now < nextRepeatTime) {
                return;
            }

            popup.MoveSelection(
                key == KeyCode.DownArrow ? 1 : -1
            );

            nextRepeatTime = now + RepeatInterval;
        }

        private static KeyCode GetHeldNavigationKey() {
            if(OVC_Input.GetKey(KeyCode.DownArrow)) {
                return KeyCode.DownArrow;
            }

            if(OVC_Input.GetKey(KeyCode.UpArrow)) {
                return KeyCode.UpArrow;
            }

            return KeyCode.None;
        }
    }
}
