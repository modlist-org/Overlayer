using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.IO.UnityComponent.Impl;
namespace Overlayer.IO.Overlay;

public sealed class OvObjectSettings : ISettingsFile, ICopyable<OvObjectSettings> {
    public string Name = "OvObject";
    public bool Enabled = true;

    public RectTransformSettings RectTransformConfig = new();
    public CanvasGroupSettings CanvasGroupConfig = new();

    public TextMeshProUGUISettings TextConfig = null;
    public OvTextSettings TextEngineConfig = null;
    public ImageSettings ImageConfig = null;
    public MaskSettings MaskConfig = null;
    public ShadowSettings ShadowConfig = null;
    public OutlineSettings OutlineConfig = null;
    public bool HasRectMask2D = false;

    public JToken Serialize() {
        var obj = new JObject {
            [nameof(Name)] = Name,
            [nameof(RectTransformConfig)] = RectTransformConfig?.Serialize(),
            [nameof(CanvasGroupConfig)] = CanvasGroupConfig?.Serialize(),
        };
        if(!Enabled) {
            obj[nameof(Enabled)] = false;
        }
        if(TextConfig != null) {
            obj[nameof(TextConfig)] = TextConfig.Serialize();
            obj[nameof(TextEngineConfig)] = (TextEngineConfig ?? OvTextSettings.FromLegacy(TextConfig.Text)).Serialize();
        }
        if(ImageConfig != null) {
            obj[nameof(ImageConfig)] = ImageConfig.Serialize();
        }
        if(MaskConfig != null) {
            obj[nameof(MaskConfig)] = MaskConfig.Serialize();
        }
        if(ShadowConfig != null) {
            obj[nameof(ShadowConfig)] = ShadowConfig.Serialize();
        }
        if(OutlineConfig != null) {
            obj[nameof(OutlineConfig)] = OutlineConfig.Serialize();
        }
        if(HasRectMask2D) {
            obj[nameof(HasRectMask2D)] = true;
        }
        return obj;
    }

    public void Deserialize(JToken token) {
        if(token is not JObject obj) {
            return;
        }

        Name = IOUtils.Read(obj, nameof(Name), Name);
        Enabled = IOUtils.Read(obj, nameof(Enabled), Enabled);
        var rect = obj[nameof(RectTransformConfig)];
        if(rect != null) {
            RectTransformConfig ??= new RectTransformSettings();
            RectTransformConfig.Deserialize(rect);
        }
        var canvasGroup = obj[nameof(CanvasGroupConfig)];
        if(canvasGroup != null) {
            CanvasGroupConfig ??= new CanvasGroupSettings();
            CanvasGroupConfig.Deserialize(canvasGroup);
        }
        TextConfig = ReadConfig<TextMeshProUGUISettings>(obj, nameof(TextConfig));
        TextEngineConfig = ReadConfig<OvTextSettings>(obj, nameof(TextEngineConfig));
        if(TextConfig != null) {
            TextEngineConfig ??= OvTextSettings.FromLegacy(TextConfig.Text);
        } else {
            TextEngineConfig = null;
        }
        ImageConfig = ReadConfig<ImageSettings>(obj, nameof(ImageConfig));
        MaskConfig = ReadConfig<MaskSettings>(obj, nameof(MaskConfig));
        ShadowConfig = ReadConfig<ShadowSettings>(obj, nameof(ShadowConfig));
        OutlineConfig = ReadConfig<OutlineSettings>(obj, nameof(OutlineConfig));
        HasRectMask2D = IOUtils.Read(obj, nameof(HasRectMask2D), HasRectMask2D);
    }

    public OvObjectSettings Copy() {
        return new OvObjectSettings {
            Name = Name,
            Enabled = Enabled,
            RectTransformConfig = RectTransformConfig?.Copy(),
            CanvasGroupConfig = CanvasGroupConfig?.Copy(),
            TextConfig = TextConfig?.Copy(),
            TextEngineConfig = TextEngineConfig?.Copy(),
            ImageConfig = ImageConfig?.Copy(),
            MaskConfig = MaskConfig?.Copy(),
            ShadowConfig = ShadowConfig?.Copy(),
            OutlineConfig = OutlineConfig?.Copy(),
            HasRectMask2D = HasRectMask2D
        };
    }

    private static T ReadConfig<T>(JObject obj, string key)
        where T : class, ISettingsFile, new() {
        var token = obj[key];

        if(token == null) {
            return null;
        }

        var cfg = new T();
        cfg.Deserialize(token);

        return cfg;
    }
}
