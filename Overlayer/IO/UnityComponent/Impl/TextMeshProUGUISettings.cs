using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
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
    public float FontSize = 42f;
    public bool RichText = true;
    public TextAlignmentOptions Alignment = TextAlignmentOptions.Center;
    public TextWrappingModes TextWrappingMode = TextWrappingModes.Normal;
    public float LineSpacing = 0f;
    public float CharacterSpacing = 0f;
    public float WordSpacing = 0f;
    public bool EnableOutline = false;
    public Color OutlineColor = UnityEngine.Color.black;
    public float OutlineWidth = 0.2f;
    public float FaceDilate = 0f;
    public float OutlineSoftness = 0f;
    public TextOverflowModes OverFlowMode = TextOverflowModes.Overflow;
    public bool AutoSize = false;
    public Vector2 FontSizeRange = new(16, 64);

    public override bool ToUnity(GameObject target) {
        var com = target.GetComponent<TextMeshProUGUI>();
        if(com == null) {
            return false;
        }

        com.text = Text;
        com.colorGradient = Color;
        com.fontSize = FontSize;
        com.richText = RichText;
        com.alignment = Alignment;
        com.textWrappingMode = TextWrappingMode;
        com.lineSpacing = LineSpacing;
        com.characterSpacing = CharacterSpacing;
        com.wordSpacing = WordSpacing;
        var mat = com.fontMaterial;
        mat.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, EnableOutline ? OutlineWidth : 0f);
        mat.SetFloat(ShaderUtilities.ID_FaceDilate, FaceDilate);
        mat.SetFloat(ShaderUtilities.ID_OutlineSoftness, OutlineSoftness);
        com.overflowMode = OverFlowMode;
        com.enableAutoSizing = AutoSize;
        com.fontSizeMin = FontSizeRange.x;
        com.fontSizeMax = FontSizeRange.y;
        com.enableVertexGradient = true;

        return true;
    }

    public override bool FromUnity(GameObject source) {
        var com = source.GetComponent<TextMeshProUGUI>();
        if(com == null) {
            return false;
        }

        Text = com.text;
        Color = com.colorGradient;
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
        OverFlowMode = com.overflowMode;
        EnableOutline = OutlineWidth > 0f;
        AutoSize = com.enableAutoSizing;
        FontSizeRange = new Vector2(com.fontSizeMin, com.fontSizeMax);

        return true;
    }

    public override JToken Serialize() {
        return new JObject {
            [nameof(Text)] = Text,
            [nameof(Color)] = IOUtils.Write(Color),
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
            [nameof(OverFlowMode)] = IOUtils.WriteEnum(OverFlowMode),
            [nameof(OutlineSoftness)] = OutlineSoftness,
            [nameof(AutoSize)] = AutoSize,
            [nameof(FontSizeRange)] = IOUtils.Write(FontSizeRange)
        };
    }

    public override void Deserialize(JToken token) {
        Text = IOUtils.Read(token, nameof(Text), Text);
        Color = IOUtils.Read(token, nameof(Color), Color);
        FontSize = IOUtils.Read(token, nameof(FontSize), FontSize);
        RichText = IOUtils.Read(token, nameof(RichText), RichText);
        Alignment = IOUtils.ReadEnum(token, nameof(Alignment), Alignment);
        TextWrappingMode = IOUtils.ReadEnum(token, nameof(TextWrappingMode), TextWrappingMode);
        LineSpacing = IOUtils.Read(token, nameof(LineSpacing), LineSpacing);
        CharacterSpacing = IOUtils.Read(token, nameof(CharacterSpacing), CharacterSpacing);
        WordSpacing = IOUtils.Read(token, nameof(WordSpacing), WordSpacing);
        EnableOutline = IOUtils.Read(token, nameof(EnableOutline), EnableOutline);
        OutlineColor = IOUtils.Read(token, nameof(OutlineColor), OutlineColor);
        OutlineWidth = IOUtils.Read(token, nameof(OutlineWidth), OutlineWidth);
        FaceDilate = IOUtils.Read(token, nameof(FaceDilate), FaceDilate);
        OverFlowMode = IOUtils.ReadEnum(token, nameof(OverFlowMode), OverFlowMode);
        OutlineSoftness = IOUtils.Read(token, nameof(OutlineSoftness), OutlineSoftness);
        AutoSize = IOUtils.Read(token, nameof(AutoSize), AutoSize);
        FontSizeRange = IOUtils.Read(token, nameof(FontSizeRange), FontSizeRange);
    }

    public TextMeshProUGUISettings Copy() {
        return new TextMeshProUGUISettings {
            Text = Text,
            Color = Color,
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
            OverFlowMode = OverFlowMode,
            OutlineSoftness = OutlineSoftness,
            AutoSize = AutoSize,
            FontSizeRange = FontSizeRange
        };
    }
}
