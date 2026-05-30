using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using TMPro;
using UnityEngine;

namespace Overlayer.IO.UnityComponent.Impl;

public class TextMeshProUGUISettings : ISettingsFile {
    public string Text = "Text";
    public GradientColor Color = UnityEngine.Color.white;
    public float FontSize = 24f;
    public bool RichText = true;
    public TextAlignmentOptions Alignment = TextAlignmentOptions.Center;
    public bool EnableWordWrapping = true;
    public float LineSpacing = 0f;
    public float CharacterSpacing = 0f;
    public float WordSpacing = 0f;
    public bool EnableOutline = false;
    public Color OutlineColor = UnityEngine.Color.black;
    public float OutlineWidth = 0.2f;
    public float FaceDilate = 0f;
    public float OutlineSoftness = 0f;
    public bool AutoSize = false;
    public Vector2 FontSizeRange = new(16, 64);

    public JToken Serialize() {
        return new JObject {
            [nameof(Text)] = Text,
            [nameof(Color)] = IOUtils.Write(Color),
            [nameof(FontSize)] = FontSize,
            [nameof(RichText)] = RichText,
            [nameof(Alignment)] = IOUtils.WriteEnum(Alignment),
            [nameof(EnableWordWrapping)] = EnableWordWrapping,
            [nameof(LineSpacing)] = LineSpacing,
            [nameof(CharacterSpacing)] = CharacterSpacing,
            [nameof(WordSpacing)] = WordSpacing,
            [nameof(EnableOutline)] = EnableOutline,
            [nameof(OutlineColor)] = IOUtils.Write(OutlineColor),
            [nameof(OutlineWidth)] = OutlineWidth,
            [nameof(FaceDilate)] = FaceDilate,
            [nameof(OutlineSoftness)] = OutlineSoftness,
            [nameof(AutoSize)] = AutoSize,
            [nameof(FontSizeRange)] = IOUtils.Write(FontSizeRange)
        };
    }

    public void Deserialize(JToken token) {
        Text = IOUtils.Read(token, nameof(Text), Text);
        Color = IOUtils.Read(token, nameof(Color), Color);
        FontSize = IOUtils.Read(token, nameof(FontSize), FontSize);
        RichText = IOUtils.Read(token, nameof(RichText), RichText);
        Alignment = IOUtils.ReadEnum(token, nameof(Alignment), Alignment);
        EnableWordWrapping = IOUtils.Read(token, nameof(EnableWordWrapping), EnableWordWrapping);
        LineSpacing = IOUtils.Read(token, nameof(LineSpacing), LineSpacing);
        CharacterSpacing = IOUtils.Read(token, nameof(CharacterSpacing), CharacterSpacing);
        WordSpacing = IOUtils.Read(token, nameof(WordSpacing), WordSpacing);
        EnableOutline = IOUtils.Read(token, nameof(EnableOutline), EnableOutline);
        OutlineColor = IOUtils.Read(token, nameof(OutlineColor), OutlineColor);
        OutlineWidth = IOUtils.Read(token, nameof(OutlineWidth), OutlineWidth);
        FaceDilate = IOUtils.Read(token, nameof(FaceDilate), FaceDilate);
        OutlineSoftness = IOUtils.Read(token, nameof(OutlineSoftness), OutlineSoftness);
        AutoSize = IOUtils.Read(token, nameof(AutoSize), AutoSize);
        FontSizeRange = IOUtils.Read(token, nameof(FontSizeRange), FontSizeRange);
    }
}
