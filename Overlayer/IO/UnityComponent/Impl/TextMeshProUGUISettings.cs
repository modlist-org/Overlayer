using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using Overlayer.IO.User;
using UnityEngine;

#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.IO.UnityComponent.Impl;

public class TextMeshProUGUISettings : UnityComponentSettingsBase, ICopyable<TextMeshProUGUISettings> {
    public FxValue<string> Text = FxValue<string>.FromValue("Text");
    public FxValue<GradientColor> Color = new(new GradientColor(UnityEngine.Color.white, true));
    public FxValue<string> FontKey = FxValue<string>.FromValue((string)null);
    public FxValue<float> FontSize = new(48f);
    public FxValue<bool> RichText = new(true);
    public FxValue<TextAlignmentOptions> Alignment = new(TextAlignmentOptions.Center);
    public FxValue<TextWrappingModes> TextWrappingMode = new(TextWrappingModes.Normal);
    public FxValue<float> LineSpacing = new(0f);
    public FxValue<float> CharacterSpacing = new(0f);
    public FxValue<float> WordSpacing = new(0f);
    public FxValue<bool> EnableOutline = new(false);
    public FxValue<Color> OutlineColor = new(UnityEngine.Color.black);
    public FxValue<float> OutlineWidth = new(0.05f);
    public FxValue<float> FaceDilate = new(0f);
    public FxValue<float> OutlineSoftness = new(0f);
    public FxValue<bool> EnableShadow = new(true);
    public FxValue<Color> ShadowColor = new(new Color(0f, 0f, 0f, 0.5f));
    public FxValue<Vector2> ShadowOffset = new(new Vector2(0.75f, -0.75f));
    public FxValue<float> ShadowDilate = new(1f);
    public FxValue<float> ShadowSoftness = new(0.5f);
    public FxValue<TextOverflowModes> OverFlowMode = new(TextOverflowModes.Overflow);
    public FxValue<bool> AutoSize = new(false);
    public FxValue<Vector2> FontSizeRange = new(new Vector2(16, 64));

    private string _lastText = "Text";
    private GradientColor _lastColor = new(UnityEngine.Color.white, true);
    private string _lastFontKey;
    private float _lastFontSize = 48f;
    private bool _lastRichText = true;
    private TextAlignmentOptions _lastAlignment = TextAlignmentOptions.Center;
    private TextWrappingModes _lastTextWrappingMode = TextWrappingModes.Normal;
    private float _lastLineSpacing;
    private float _lastCharacterSpacing;
    private float _lastWordSpacing;
    private bool _lastEnableOutline;
    private Color _lastOutlineColor = UnityEngine.Color.black;
    private float _lastOutlineWidth = 0.05f;
    private float _lastFaceDilate;
    private float _lastOutlineSoftness;
    private bool _lastEnableShadow = true;
    private Color _lastShadowColor = new(0f, 0f, 0f, 0.5f);
    private Vector2 _lastShadowOffset = new(0.75f, -0.75f);
    private float _lastShadowDilate = 1f;
    private float _lastShadowSoftness = 0.5f;
    private TextOverflowModes _lastOverFlowMode = TextOverflowModes.Overflow;
    private bool _lastAutoSize;
    private Vector2 _lastFontSizeRange = new(16, 64);

    public override bool HasAnyFx => base.HasAnyFx
        || FxUtil.HasFx(Text)
        || FxUtil.HasFx(Color)
        || FxUtil.HasFx(FontKey)
        || FxUtil.HasFx(FontSize)
        || FxUtil.HasFx(RichText)
        || FxUtil.HasFx(Alignment)
        || FxUtil.HasFx(TextWrappingMode)
        || FxUtil.HasFx(LineSpacing)
        || FxUtil.HasFx(CharacterSpacing)
        || FxUtil.HasFx(WordSpacing)
        || FxUtil.HasFx(EnableOutline)
        || FxUtil.HasFx(OutlineColor)
        || FxUtil.HasFx(OutlineWidth)
        || FxUtil.HasFx(FaceDilate)
        || FxUtil.HasFx(OutlineSoftness)
        || FxUtil.HasFx(EnableShadow)
        || FxUtil.HasFx(ShadowColor)
        || FxUtil.HasFx(ShadowOffset)
        || FxUtil.HasFx(ShadowDilate)
        || FxUtil.HasFx(ShadowSoftness)
        || FxUtil.HasFx(OverFlowMode)
        || FxUtil.HasFx(AutoSize)
        || FxUtil.HasFx(FontSizeRange);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<TextMeshProUGUI>();
        if(com == null) {
            return false;
        }

        _lastText = Text.Value;
        _lastColor = Color.Value;
        _lastFontKey = FontKey.Value;
        _lastFontSize = FontSize.Value;
        _lastRichText = RichText.Value;
        _lastAlignment = Alignment.Value;
        _lastTextWrappingMode = TextWrappingMode.Value;
        _lastLineSpacing = LineSpacing.Value;
        _lastCharacterSpacing = CharacterSpacing.Value;
        _lastWordSpacing = WordSpacing.Value;
        _lastEnableOutline = EnableOutline.Value;
        _lastOutlineColor = OutlineColor.Value;
        _lastOutlineWidth = OutlineWidth.Value;
        _lastFaceDilate = FaceDilate.Value;
        _lastOutlineSoftness = OutlineSoftness.Value;
        _lastEnableShadow = EnableShadow.Value;
        _lastShadowColor = ShadowColor.Value;
        _lastShadowOffset = ShadowOffset.Value;
        _lastShadowDilate = ShadowDilate.Value;
        _lastShadowSoftness = ShadowSoftness.Value;
        _lastOverFlowMode = OverFlowMode.Value;
        _lastAutoSize = AutoSize.Value;
        _lastFontSizeRange = FontSizeRange.Value;
        ApplyTextValues(com);
        ToUnity(com);
        com.UpdateMeshPadding();
        com.SetMaterialDirty();
        com.SetVerticesDirty();
        com.SetLayoutDirty();

        return true;
    }

    private void ApplyTextValues(TextMeshProUGUI com) {
        com.text = _lastText;
        com.color = UnityEngine.Color.white;
        com.colorGradient = _lastColor;
        if(!string.IsNullOrEmpty(_lastFontKey) && UserResourceManager.Fnt.TryGet(_lastFontKey, out var fontAsset)) {
            com.font = fontAsset;
        }
        com.fontSize = _lastFontSize;
        com.richText = _lastRichText;
        com.alignment = _lastAlignment;
        com.textWrappingMode = _lastTextWrappingMode;
        com.lineSpacing = _lastLineSpacing;
        com.characterSpacing = _lastCharacterSpacing;
        com.wordSpacing = _lastWordSpacing;
        var mat = com.fontMaterial;
        float outlineWidth = _lastEnableOutline ? Mathf.Clamp01(_lastOutlineWidth) : 0f;
        Color appliedOutlineColor = _lastEnableOutline
            ? _lastOutlineColor
            : new Color(_lastOutlineColor.r, _lastOutlineColor.g, _lastOutlineColor.b, 0f);
        float outlineSoftness = _lastEnableOutline ? Mathf.Clamp01(_lastOutlineSoftness) : 0f;
        com.outlineColor = appliedOutlineColor;
        com.outlineWidth = outlineWidth;
        mat = com.fontMaterial;
        mat.SetColor(ShaderUtilities.ID_OutlineColor, appliedOutlineColor);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, _lastFaceDilate);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, outlineSoftness);
        if(outlineWidth > 0f) {
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        } else {
            mat.DisableKeyword(ShaderUtilities.Keyword_Outline);
        }
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, _lastEnableShadow
            ? _lastShadowColor
            : new Color(_lastShadowColor.r, _lastShadowColor.g, _lastShadowColor.b, 0f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, _lastShadowOffset.x);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, _lastShadowOffset.y);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, Mathf.Clamp01(_lastShadowDilate));
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, Mathf.Clamp01(_lastShadowSoftness));
        if(_lastEnableShadow) {
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        } else {
            mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        }
        com.overflowMode = _lastOverFlowMode;
        com.enableAutoSizing = _lastAutoSize;
        com.fontSizeMin = Mathf.Min(_lastFontSizeRange.x, _lastFontSizeRange.y);
        com.fontSizeMax = Mathf.Max(_lastFontSizeRange.x, _lastFontSizeRange.y);
        com.enableVertexGradient = true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<TextMeshProUGUI>();
        if(com == null) {
            return false;
        }

        Text.Value = com.text;
        Color.Value = com.colorGradient;
        FontKey.Value = UserResourceManager.Fnt.Keys.FirstOrDefault(key => UserResourceManager.Fnt.TryGet(key, out var font) && font == com.font);
        FontSize.Value = com.fontSize;
        RichText.Value = com.richText;
        Alignment.Value = com.alignment;
        TextWrappingMode.Value = com.textWrappingMode;
        LineSpacing.Value = com.lineSpacing;
        CharacterSpacing.Value = com.characterSpacing;
        WordSpacing.Value = com.wordSpacing;
        var mat = com.fontMaterial;
        OutlineColor.Value = mat.GetColor(ShaderUtilities.ID_OutlineColor);
        OutlineWidth.Value = mat.GetFloat(ShaderUtilities.ID_OutlineWidth);
        FaceDilate.Value = mat.GetFloat(ShaderUtilities.ID_FaceDilate);
        OutlineSoftness.Value = mat.GetFloat(ShaderUtilities.ID_OutlineSoftness);
        ShadowColor.Value = mat.GetColor(ShaderUtilities.ID_UnderlayColor);
        ShadowOffset.Value = new Vector2(
            mat.GetFloat(ShaderUtilities.ID_UnderlayOffsetX),
            mat.GetFloat(ShaderUtilities.ID_UnderlayOffsetY)
        );
        ShadowDilate.Value = mat.GetFloat(ShaderUtilities.ID_UnderlayDilate);
        ShadowSoftness.Value = mat.GetFloat(ShaderUtilities.ID_UnderlaySoftness);
        EnableShadow.Value = mat.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay) && ShadowColor.Value.a > 0f;
        OverFlowMode.Value = com.overflowMode;
        EnableOutline.Value = OutlineWidth.Value > 0f && mat.IsKeywordEnabled(ShaderUtilities.Keyword_Outline);
        AutoSize.Value = com.enableAutoSizing;
        FontSizeRange.Value = new Vector2(com.fontSizeMin, com.fontSizeMax);
        _lastText = Text.Value;
        _lastColor = Color.Value;
        _lastFontKey = FontKey.Value;
        _lastFontSize = FontSize.Value;
        _lastRichText = RichText.Value;
        _lastAlignment = Alignment.Value;
        _lastTextWrappingMode = TextWrappingMode.Value;
        _lastLineSpacing = LineSpacing.Value;
        _lastCharacterSpacing = CharacterSpacing.Value;
        _lastWordSpacing = WordSpacing.Value;
        _lastEnableOutline = EnableOutline.Value;
        _lastOutlineColor = OutlineColor.Value;
        _lastOutlineWidth = OutlineWidth.Value;
        _lastFaceDilate = FaceDilate.Value;
        _lastOutlineSoftness = OutlineSoftness.Value;
        _lastEnableShadow = EnableShadow.Value;
        _lastShadowColor = ShadowColor.Value;
        _lastShadowOffset = ShadowOffset.Value;
        _lastShadowDilate = ShadowDilate.Value;
        _lastShadowSoftness = ShadowSoftness.Value;
        _lastOverFlowMode = OverFlowMode.Value;
        _lastAutoSize = AutoSize.Value;
        _lastFontSizeRange = FontSizeRange.Value;
        FromUnity(com);

        return true;
    }

    public override void RefreshFx(GameObject target) {
        if(!HasAnyFx) {
            return;
        }

        var com = target.GetComponent<TextMeshProUGUI>();
        if(com == null) {
            return;
        }

        RefreshEnabled(com);
        bool changed = false;
        changed |= FxUtil.ApplyIfChanged(ref _lastText, Text.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastColor, Color.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastFontKey, FontKey.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastFontSize, FontSize.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastRichText, RichText.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastAlignment, Alignment.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastTextWrappingMode, TextWrappingMode.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastLineSpacing, LineSpacing.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastCharacterSpacing, CharacterSpacing.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastWordSpacing, WordSpacing.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastEnableOutline, EnableOutline.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastOutlineColor, OutlineColor.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastOutlineWidth, OutlineWidth.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastFaceDilate, FaceDilate.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastOutlineSoftness, OutlineSoftness.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastEnableShadow, EnableShadow.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastShadowColor, ShadowColor.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastShadowOffset, ShadowOffset.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastShadowDilate, ShadowDilate.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastShadowSoftness, ShadowSoftness.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastOverFlowMode, OverFlowMode.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastAutoSize, AutoSize.Value, _ => { });
        changed |= FxUtil.ApplyIfChanged(ref _lastFontSizeRange, FontSizeRange.Value, _ => { });
        if(!changed) {
            return;
        }

        ApplyTextValues(com);
        com.SetMaterialDirty();
        com.SetVerticesDirty();
        com.SetLayoutDirty();
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Text)] = IOUtils.WriteFx(Text),
            [nameof(Color)] = IOUtils.WriteFx(Color),
            [nameof(FontKey)] = IOUtils.WriteFx(FontKey),
            [nameof(FontSize)] = IOUtils.WriteFx(FontSize),
            [nameof(RichText)] = IOUtils.WriteFx(RichText),
            [nameof(Alignment)] = IOUtils.WriteFx(Alignment),
            [nameof(TextWrappingMode)] = IOUtils.WriteFx(TextWrappingMode),
            [nameof(LineSpacing)] = IOUtils.WriteFx(LineSpacing),
            [nameof(CharacterSpacing)] = IOUtils.WriteFx(CharacterSpacing),
            [nameof(WordSpacing)] = IOUtils.WriteFx(WordSpacing),
            [nameof(EnableOutline)] = IOUtils.WriteFx(EnableOutline),
            [nameof(OutlineColor)] = IOUtils.WriteFx(OutlineColor),
            [nameof(OutlineWidth)] = IOUtils.WriteFx(OutlineWidth),
            [nameof(FaceDilate)] = IOUtils.WriteFx(FaceDilate),
            [nameof(EnableShadow)] = IOUtils.WriteFx(EnableShadow),
            [nameof(ShadowColor)] = IOUtils.WriteFx(ShadowColor),
            [nameof(ShadowOffset)] = IOUtils.WriteFx(ShadowOffset),
            [nameof(ShadowDilate)] = IOUtils.WriteFx(ShadowDilate),
            [nameof(ShadowSoftness)] = IOUtils.WriteFx(ShadowSoftness),
            [nameof(OverFlowMode)] = IOUtils.WriteFx(OverFlowMode),
            [nameof(OutlineSoftness)] = IOUtils.WriteFx(OutlineSoftness),
            [nameof(AutoSize)] = IOUtils.WriteFx(AutoSize),
            [nameof(FontSizeRange)] = IOUtils.WriteFx(FontSizeRange)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Text = IOUtils.ReadFx(token, nameof(Text), Text);
        Color = IOUtils.ReadFx(token, nameof(Color), Color);
        FontKey = IOUtils.ReadFx(token, nameof(FontKey), FontKey);
        FontSize = IOUtils.ReadFx(token, nameof(FontSize), FontSize);
        if(!FontSize.UseFx && Mathf.Approximately(FontSize.Value, 42f)) {
            FontSize.Value = 48f;
        }
        RichText = IOUtils.ReadFx(token, nameof(RichText), RichText);
        Alignment = IOUtils.ReadFx(token, nameof(Alignment), Alignment);
        TextWrappingMode = IOUtils.ReadFx(token, nameof(TextWrappingMode), TextWrappingMode);
        LineSpacing = IOUtils.ReadFx(token, nameof(LineSpacing), LineSpacing);
        CharacterSpacing = IOUtils.ReadFx(token, nameof(CharacterSpacing), CharacterSpacing);
        WordSpacing = IOUtils.ReadFx(token, nameof(WordSpacing), WordSpacing);
        EnableOutline = IOUtils.ReadFx(token, nameof(EnableOutline), EnableOutline);
        OutlineColor = IOUtils.ReadFx(token, nameof(OutlineColor), OutlineColor);
        OutlineWidth = IOUtils.ReadFx(token, nameof(OutlineWidth), OutlineWidth);
        if(!OutlineWidth.UseFx && Mathf.Approximately(OutlineWidth.Value, 0.2f)) {
            OutlineWidth.Value = 0.05f;
        }
        FaceDilate = IOUtils.ReadFx(token, nameof(FaceDilate), FaceDilate);
        EnableShadow = IOUtils.ReadFx(token, nameof(EnableShadow), EnableShadow);
        ShadowColor = IOUtils.ReadFx(token, nameof(ShadowColor), ShadowColor);
        ShadowOffset = IOUtils.ReadFx(token, nameof(ShadowOffset), ShadowOffset);
        if(!ShadowOffset.UseFx && ((Mathf.Approximately(ShadowOffset.Value.x, 0.5f)
                && Mathf.Approximately(ShadowOffset.Value.y, -0.5f))
            || (Mathf.Approximately(ShadowOffset.Value.x, 0.25f)
                && Mathf.Approximately(ShadowOffset.Value.y, -0.25f)))) {
            ShadowOffset.Value = new Vector2(0.75f, -0.75f);
        }
        ShadowDilate = IOUtils.ReadFx(token, nameof(ShadowDilate), ShadowDilate);
        ShadowSoftness = IOUtils.ReadFx(token, nameof(ShadowSoftness), ShadowSoftness);
        OverFlowMode = IOUtils.ReadFx(token, nameof(OverFlowMode), OverFlowMode);
        OutlineSoftness = IOUtils.ReadFx(token, nameof(OutlineSoftness), OutlineSoftness);
        AutoSize = IOUtils.ReadFx(token, nameof(AutoSize), AutoSize);
        FontKey = IOUtils.ReadFx(token, nameof(FontKey), FontKey);
        FontSizeRange = IOUtils.ReadFx(token, nameof(FontSizeRange), FontSizeRange);
    }

    public TextMeshProUGUISettings Copy() {
        return new TextMeshProUGUISettings {
            ComponentEnabled = ComponentEnabled?.Copy(),
            Text = Text?.Copy(),
            Color = Color?.Copy(),
            FontKey = FontKey?.Copy(),
            FontSize = FontSize?.Copy(),
            RichText = RichText?.Copy(),
            Alignment = Alignment?.Copy(),
            TextWrappingMode = TextWrappingMode?.Copy(),
            LineSpacing = LineSpacing?.Copy(),
            CharacterSpacing = CharacterSpacing?.Copy(),
            WordSpacing = WordSpacing?.Copy(),
            EnableOutline = EnableOutline?.Copy(),
            OutlineColor = OutlineColor?.Copy(),
            OutlineWidth = OutlineWidth?.Copy(),
            FaceDilate = FaceDilate?.Copy(),
            EnableShadow = EnableShadow?.Copy(),
            ShadowColor = ShadowColor?.Copy(),
            ShadowOffset = ShadowOffset?.Copy(),
            ShadowDilate = ShadowDilate?.Copy(),
            ShadowSoftness = ShadowSoftness?.Copy(),
            OverFlowMode = OverFlowMode?.Copy(),
            OutlineSoftness = OutlineSoftness?.Copy(),
            AutoSize = AutoSize?.Copy(),
            FontSizeRange = FontSizeRange?.Copy()
        };
    }
}
