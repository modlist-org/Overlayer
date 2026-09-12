using FuzzySharp;
using Overlayer.Compat.OVC;
using Overlayer.Tag.Core;
using Overlayer.UI.Generator;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Overlayer.UI.Utility;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.UI.Objects.Impl;

internal sealed class JsCompletionPopup : ICodeCompletion {
    private const int MaxItems = 8;
    private const float ItemHeight = 26f;
    private const float PopupWidth = 360f;

    private readonly UICodeInputField input;
    private readonly RectTransform canvasRect;
    private readonly TMP_Text sourceText;
    private readonly RectTransform popupRect;
    private readonly CompletionRow[] rows;
    private readonly List<JsItem> matches = [];

    private int selectedIndex;
    private int windowStart;
    private int replacementStart;
    private int replacementLength;
    private int visibleRowCount;
    private bool visible;
    private bool suppressRefresh;
    private string suppressedText;
    private int suppressedCaret;
    private int hoveredIndex = -1;

    private static readonly string[] Keywords = [
        "const", "let", "var", "function", "return", "if", "else",
        "for", "while", "do", "switch", "case", "break", "continue",
        "new", "delete", "typeof", "instanceof", "in", "of", "try",
        "catch", "finally", "throw", "class", "extends", "super",
        "import", "export", "default", "yield", "await", "async",
        "static", "get", "set", "this"
    ];

    private static readonly string[] Constants = [
        "true", "false", "null", "undefined", "Infinity", "NaN"
    ];

    private static readonly (string Name, bool Callable)[] MathMembers = [
        ("abs", true), ("acos", true), ("acosh", true), ("asin", true),
        ("asinh", true), ("atan", true), ("atan2", true), ("atanh", true),
        ("cbrt", true), ("ceil", true), ("clz32", true), ("cos", true),
        ("cosh", true), ("exp", true), ("expm1", true), ("floor", true),
        ("fround", true), ("hypot", true), ("imul", true), ("log", true),
        ("log1p", true), ("log2", true), ("log10", true), ("max", true),
        ("min", true), ("pow", true), ("random", true), ("round", true),
        ("sign", true), ("sin", true), ("sinh", true), ("sqrt", true),
        ("tan", true), ("tanh", true), ("trunc", true),
        ("E", false), ("LN2", false), ("LN10", false), ("LOG2E", false),
        ("LOG10E", false), ("PI", false), ("SQRT1_2", false), ("SQRT2", false)
    ];

    private static readonly (string Name, bool Callable)[] JsonMembers = [
        ("parse", true), ("stringify", true)
    ];

    public JsCompletionPopup(UICodeInputField input, TMP_Text sourceText) {
        this.input = input;
        this.sourceText = sourceText;

        canvasRect = sourceText.canvas?.rootCanvas?.GetComponent<RectTransform>()
            ?? UICore.CanvasObj?.GetComponent<RectTransform>();

        input.OnFieldDisabled = Deactivate;
        input.OnFieldDestroyed = Dispose;

        GameObject popup = new("JsCompletion");
        popup.transform.SetParent(canvasRect, false);
        popup.transform.SetAsLastSibling();

        popupRect = popup.AddComponent<RectTransform>();
        popupRect.anchorMin = new(0.5f, 0.5f);
        popupRect.anchorMax = new(0.5f, 0.5f);
        popupRect.pivot = new(0f, 1f);
        popupRect.sizeDelta = new(PopupWidth, MaxItems * ItemHeight);

        Image popupImage = popup.AddComponent<Image>();
        popupImage.color = new Color(0.10f, 0.10f, 0.14f, 0.98f);

        rows = new CompletionRow[MaxItems];

        for(int i = 0; i < MaxItems; i++) {
            rows[i] = CreateRow(popup.transform, i);
        }

        popup.SetActive(false);
    }

    public bool HandleKey(KeyCode key) {
        if(!visible || matches.Count == 0) {
            return false;
        }

        switch(key) {
            case KeyCode.Tab:
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
            Hide();
            return;
        }

        if(composing || input.selectionAnchorPosition != input.selectionFocusPosition) {
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

        if(!TryGetContext(text, caret, out string query, out int start, out string qualifier)) {
            Hide();
            return;
        }

        replacementStart = start;
        replacementLength = caret - start;
        RebuildMatches(query, qualifier);
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
            if(button == PointerEventData.InputButton.Left) {
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

    private void RebuildMatches(string query, string qualifier) {
        string previousSelection = selectedIndex >= 0 && selectedIndex < matches.Count
            ? matches[selectedIndex].Name
            : null;

        matches.Clear();
        foreach(var item in CollectItems(qualifier)) {
            int score = string.IsNullOrEmpty(query)
                ? 0
                : item.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                    ? 1000 - item.Name.Length
                    : Fuzz.WeightedRatio(query, item.Name);

            if(string.IsNullOrEmpty(query) || score >= 45) {
                matches.Add(item);
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
            int previousIndex = matches.FindIndex(item => item.Name == previousSelection);
            if(previousIndex >= 0) {
                selectedIndex = previousIndex;
            }
        }
    }

    private static IEnumerable<JsItem> CollectItems(string qualifier) {
        if(qualifier != null) {
            if(qualifier == "Tag") {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach(var tag in Overlayer.Tag.Core.TagManager.GetAllTags()) {
                    if(string.IsNullOrEmpty(tag.Name) || !seen.Add(tag.Name)) {
                        continue;
                    }

                    yield return new JsItem(tag.Name, FormatTagDetail(tag), true);
                }

                yield break;
            }

            if(qualifier == "Math") {
                foreach(var (name, callable) in MathMembers) {
                    yield return new JsItem(name, "Math static", callable);
                }

                yield break;
            }

            if(qualifier == "JSON") {
                foreach(var (name, callable) in JsonMembers) {
                    yield return new JsItem(name, "JSON static", callable);
                }

                yield break;
            }

            yield break;
        }

        yield return new JsItem("Tag", "namespace", false);
        yield return new JsItem("Math", "namespace", false);
        yield return new JsItem("JSON", "namespace", false);

        foreach(string keyword in Keywords) {
            yield return new JsItem(keyword, "keyword", false);
        }

        foreach(string constant in Constants) {
            yield return new JsItem(constant, "constant", false);
        }

        var seenTags = new HashSet<string>(StringComparer.Ordinal);
        foreach(var tag in Overlayer.Tag.Core.TagManager.GetAllTags()) {
            if(string.IsNullOrEmpty(tag.Name) || !seenTags.Add(tag.Name)) {
                continue;
            }

            yield return new JsItem(tag.Name, FormatTagDetail(tag), true);
        }
    }

    private static string FormatTagDetail(Overlayer.Tag.Core.TagCore tag) {
        if(tag.Parameters.Length == 0) {
            return string.IsNullOrEmpty(tag.ReturnType?.Name) ? "tag" : tag.ReturnType.Name;
        }

        var names = new List<string>();
        for(int i = 0; i < tag.Parameters.Length; i++) {
            var parameter = tag.Parameters[i];
            names.Add(string.IsNullOrEmpty(parameter?.Name) ? $"arg{i + 1}" : parameter.Name);
        }

        return $"({string.Join(", ", names)})";
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

            JsItem item = matches[matchIndex];
            if(matchIndex == selectedIndex) {
                rows[i].Image.color = UIColors.MenuHover;
            } else if(matchIndex == hoveredIndex) {
                Color hoverColor = UIColors.MenuHover;
                hoverColor.a *= 0.35f;
                rows[i].Image.color = hoverColor;
            } else {
                rows[i].Image.color = Color.clear;
            }
            rows[i].Name.text = item.Callable ? item.Name + "()" : item.Name;
            rows[i].Detail.text = item.Detail;
        }
    }

    private void Accept(int index) {
        if(index < 0 || index >= matches.Count) {
            return;
        }

        string text = input.text ?? string.Empty;
        int start = Math.Clamp(replacementStart, 0, text.Length);
        int end = Math.Clamp(start + replacementLength, start, text.Length);
        JsItem item = matches[index];

        string insertion = item.Name;
        int caretOffset = insertion.Length;
        if(item.Callable) {
            insertion += "()";
            caretOffset = insertion.Length - 1;
        }

        input.text = text[..start] + insertion + text[end..];
        input.ActivateInputField();

        int caret = Math.Clamp(start + caretOffset, 0, (input.text ?? string.Empty).Length);
        input.selectionAnchorPosition = caret;
        input.selectionFocusPosition = caret;
        input.ForceLabelUpdate();

        suppressRefresh = true;
        suppressedText = input.text;
        suppressedCaret = input.selectionFocusPosition;
        Hide();
    }

    private void MoveSelection(int delta) {
        if(!visible || matches.Count == 0) {
            return;
        }

        selectedIndex = (selectedIndex + delta + matches.Count) % matches.Count;
        UpdateRows();
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

    private static bool TryGetContext(string text, int caret, out string query, out int start, out string qualifier) {
        query = string.Empty;
        start = caret;
        qualifier = null;
        if(caret <= 0 || caret > text.Length) {
            return false;
        }

        int end = caret;
        int i = end - 1;
        while(i >= 0 && IsWordChar(text[i])) {
            i--;
        }

        start = i + 1;
        if(start >= end) {
            return false;
        }

        query = text[start..end];

        if(i >= 0 && text[i] == '.') {
            int j = i - 1;
            while(j >= 0 && IsWordChar(text[j])) {
                j--;
            }

            if(j + 1 > i - 1) {
                return false;
            }

            qualifier = text[(j + 1)..i];
        }

        return true;
    }

    private static bool IsWordChar(char c)
        => char.IsLetterOrDigit(c) || c == '_' || c == '$';

    private readonly struct JsItem(string name, string detail, bool callable) {
        public readonly string Name = name;
        public readonly string Detail = detail;
        public readonly bool Callable = callable;
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
}
