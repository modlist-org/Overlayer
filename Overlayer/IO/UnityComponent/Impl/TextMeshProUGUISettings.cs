using System.Linq;
using Newtonsoft.Json.Linq;
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
    public string Text = "Text";
    public GradientColor Color = UnityEngine.Color.white;
    public string FontKey = null;
    public float FontSize = 48f;
    public bool RichText = true;
    public TextAlignmentOptions Alignment = TextAlignmentOptions.Center;
    public TextWrappingModes TextWrappingMode = TextWrappingModes.Normal;
    public float LineSpacing = 0f;
    public float CharacterSpacing = 0f;
    public float WordSpacing = 0f;
    public bool EnableOutline = false;
    public Color OutlineColor = UnityEngine.Color.black;
    public float OutlineWidth = 0.05f;
    public float FaceDilate = 0f;
    public float OutlineSoftness = 0f;
    public bool EnableShadow = true;
    public Color ShadowColor = new(0f, 0f, 0f, 0.5f);
    public Vector2 ShadowOffset = new(0.75f, -0.75f);
    public float ShadowDilate = 1f;
    public float ShadowSoftness = 0.5f;
    public TextOverflowModes OverFlowMode = TextOverflowModes.Overflow;
    public bool AutoSize = false;
    public Vector2 FontSizeRange = new(16, 64);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<TextMeshProUGUI>();
        if(com == null) {
            return false;
        }

        com.text = Text;
        com.color = UnityEngine.Color.white;
        com.colorGradient = Color;
        if(!string.IsNullOrEmpty(FontKey) && UserResourceManager.Fnt.TryGet(FontKey, out var fontAsset)) {
            com.font = fontAsset;
        }
        com.fontSize = FontSize;
        com.richText = RichText;
        com.alignment = Alignment;
        com.textWrappingMode = TextWrappingMode;
        com.lineSpacing = LineSpacing;
        com.characterSpacing = CharacterSpacing;
        com.wordSpacing = WordSpacing;
        var mat = com.fontMaterial;
        float outlineWidth = EnableOutline ? Mathf.Clamp01(OutlineWidth) : 0f;
        Color appliedOutlineColor = EnableOutline
            ? OutlineColor
            : new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, 0f);
        float outlineSoftness = EnableOutline ? Mathf.Clamp01(OutlineSoftness) : 0f;
        com.outlineColor = appliedOutlineColor;
        com.outlineWidth = outlineWidth;
        mat = com.fontMaterial;
        mat.SetColor(ShaderUtilities.ID_OutlineColor, appliedOutlineColor);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, FaceDilate);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, outlineSoftness);
        if(outlineWidth > 0f) {
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
        } else {
            mat.DisableKeyword(ShaderUtilities.Keyword_Outline);
        }
        mat.SetColor(ShaderUtilities.ID_UnderlayColor, EnableShadow
            ? ShadowColor
            : new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, 0f));
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, ShadowOffset.x);
        mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, ShadowOffset.y);
        mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, Mathf.Clamp01(ShadowDilate));
        mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, Mathf.Clamp01(ShadowSoftness));
        if(EnableShadow) {
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        } else {
            mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
        }
        com.overflowMode = OverFlowMode;
        com.enableAutoSizing = AutoSize;
        com.fontSizeMin = Mathf.Min(FontSizeRange.x, FontSizeRange.y);
        com.fontSizeMax = Mathf.Max(FontSizeRange.x, FontSizeRange.y);
        com.enableVertexGradient = true;
        ToUnity(com);
        com.UpdateMeshPadding();
        com.SetMaterialDirty();
        com.SetVerticesDirty();
        com.SetLayoutDirty();

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<TextMeshProUGUI>();
        if(com == null) {
            return false;
        }

        Text = com.text;
        Color = com.colorGradient;
        FontKey = UserResourceManager.Fnt.Keys.FirstOrDefault(key => UserResourceManager.Fnt.TryGet(key, out var font) && font == com.font);
        FontSize = com.fontSize;
        RichText = com.richText;
        Alignment = com.alignment;
        TextWrappingMode = com.textWrappingMode;
        LineSpacing = com.lineSpacing;
        CharacterSpacing = com.characterSpacing;
        WordSpacing = com.wordSpacing;
        var mat = com.fontMaterial;
        OutlineColor = mat.GetColor(ShaderUtilities.ID_OutlineColor);
        OutlineWidth = mat.GetFloat(ShaderUtilities.ID_OutlineWidth);
        FaceDilate = mat.GetFloat(ShaderUtilities.ID_FaceDilate);
        OutlineSoftness = mat.GetFloat(ShaderUtilities.ID_OutlineSoftness);
        ShadowColor = mat.GetColor(ShaderUtilities.ID_UnderlayColor);
        ShadowOffset = new Vector2(
            mat.GetFloat(ShaderUtilities.ID_UnderlayOffsetX),
            mat.GetFloat(ShaderUtilities.ID_UnderlayOffsetY)
        );
        ShadowDilate = mat.GetFloat(ShaderUtilities.ID_UnderlayDilate);
        ShadowSoftness = mat.GetFloat(ShaderUtilities.ID_UnderlaySoftness);
        EnableShadow = mat.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay) && ShadowColor.a > 0f;
        OverFlowMode = com.overflowMode;
        EnableOutline = OutlineWidth > 0f && mat.IsKeywordEnabled(ShaderUtilities.Keyword_Outline);
        AutoSize = com.enableAutoSizing;
        FontSizeRange = new Vector2(com.fontSizeMin, com.fontSizeMax);
        FromUnity(com);

        return true;
    }

    public override JToken Serialize() {
        return SerializeComponent(new JObject {
            [nameof(Text)] = Text,
            [nameof(Color)] = IOUtils.Write(Color),
            [nameof(FontKey)] = FontKey,
            [nameof(FontSize)] = FontSize,
            [nameof(RichText)] = RichText,
            [nameof(Alignment)] = IOUtils.WriteEnum(Alignment),
            [nameof(TextWrappingMode)] = IOUtils.WriteEnum(TextWrappingMode),
            [nameof(LineSpacing)] = LineSpacing,
            [nameof(CharacterSpacing)] = CharacterSpacing,
            [nameof(WordSpacing)] = WordSpacing,
            [nameof(EnableOutline)] = EnableOutline,
            [nameof(OutlineColor)] = IOUtils.Write(OutlineColor),
            [nameof(OutlineWidth)] = OutlineWidth,
            [nameof(FaceDilate)] = FaceDilate,
            [nameof(EnableShadow)] = EnableShadow,
            [nameof(ShadowColor)] = IOUtils.Write(ShadowColor),
            [nameof(ShadowOffset)] = IOUtils.Write(ShadowOffset),
            [nameof(ShadowDilate)] = ShadowDilate,
            [nameof(ShadowSoftness)] = ShadowSoftness,
            [nameof(OverFlowMode)] = IOUtils.WriteEnum(OverFlowMode),
            [nameof(OutlineSoftness)] = OutlineSoftness,
            [nameof(AutoSize)] = AutoSize,
            [nameof(FontSizeRange)] = IOUtils.Write(FontSizeRange)
        });
    }

    public override void Deserialize(JToken token) {
        DeserializeComponent(token);
        Text = IOUtils.Read(token, nameof(Text), Text);
        Color = IOUtils.Read(token, nameof(Color), Color);
        FontKey = IOUtils.Read(token, nameof(FontKey), FontKey);
        FontSize = IOUtils.Read(token, nameof(FontSize), FontSize);
        if(Mathf.Approximately(FontSize, 42f)) {
            FontSize = 48f;
        }
        RichText = IOUtils.Read(token, nameof(RichText), RichText);
        Alignment = IOUtils.ReadEnum(token, nameof(Alignment), Alignment);
        TextWrappingMode = IOUtils.ReadEnum(token, nameof(TextWrappingMode), TextWrappingMode);
        LineSpacing = IOUtils.Read(token, nameof(LineSpacing), LineSpacing);
        CharacterSpacing = IOUtils.Read(token, nameof(CharacterSpacing), CharacterSpacing);
        WordSpacing = IOUtils.Read(token, nameof(WordSpacing), WordSpacing);
        EnableOutline = IOUtils.Read(token, nameof(EnableOutline), EnableOutline);
        OutlineColor = IOUtils.Read(token, nameof(OutlineColor), OutlineColor);
        OutlineWidth = IOUtils.Read(token, nameof(OutlineWidth), OutlineWidth);
        if(Mathf.Approximately(OutlineWidth, 0.2f)) {
            OutlineWidth = 0.05f;
        }
        FaceDilate = IOUtils.Read(token, nameof(FaceDilate), FaceDilate);
        EnableShadow = IOUtils.Read(token, nameof(EnableShadow), EnableShadow);
        ShadowColor = IOUtils.Read(token, nameof(ShadowColor), ShadowColor);
        ShadowOffset = IOUtils.Read(token, nameof(ShadowOffset), ShadowOffset);
        if((Mathf.Approximately(ShadowOffset.x, 0.5f)
                && Mathf.Approximately(ShadowOffset.y, -0.5f))
            || (Mathf.Approximately(ShadowOffset.x, 0.25f)
                && Mathf.Approximately(ShadowOffset.y, -0.25f))) {
            ShadowOffset = new Vector2(0.75f, -0.75f);
        }
        ShadowDilate = IOUtils.Read(token, nameof(ShadowDilate), ShadowDilate);
        ShadowSoftness = IOUtils.Read(token, nameof(ShadowSoftness), ShadowSoftness);
        OverFlowMode = IOUtils.ReadEnum(token, nameof(OverFlowMode), OverFlowMode);
        OutlineSoftness = IOUtils.Read(token, nameof(OutlineSoftness), OutlineSoftness);
        AutoSize = IOUtils.Read(token, nameof(AutoSize), AutoSize);
        FontKey = IOUtils.Read(token, nameof(FontKey), FontKey);
        FontSizeRange = IOUtils.Read(token, nameof(FontSizeRange), FontSizeRange);
    }

    public TextMeshProUGUISettings Copy() {
        return new TextMeshProUGUISettings {
            ComponentEnabled = ComponentEnabled,
            Text = Text,
            Color = Color,
            FontKey = FontKey,
            FontSize = FontSize,
            RichText = RichText,
            Alignment = Alignment,
            TextWrappingMode = TextWrappingMode,
            LineSpacing = LineSpacing,
            CharacterSpacing = CharacterSpacing,
            WordSpacing = WordSpacing,
            EnableOutline = EnableOutline,
            OutlineColor = OutlineColor,
            OutlineWidth = OutlineWidth,
            FaceDilate = FaceDilate,
            EnableShadow = EnableShadow,
            ShadowColor = ShadowColor,
            ShadowOffset = ShadowOffset,
            ShadowDilate = ShadowDilate,
            ShadowSoftness = ShadowSoftness,
            OverFlowMode = OverFlowMode,
            OutlineSoftness = OutlineSoftness,
            AutoSize = AutoSize,
            FontSizeRange = FontSizeRange
        };
    }
}
