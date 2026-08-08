using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.IO.UnityComponent.Impl;
namespace Overlayer.IO.Overlay;

public sealed class OvObjectSettings : ISettingsFile, ICopyable<OvObjectSettings> {
    public string Name = "OvObject";
    public bool Enabled = true;

    public RectTransformSettings RectTransformConfig = new();
    public CanvasGroupSettings CanvasGroupConfig = new();
    public ContentSizeFitterSettings ContentSizeFitterConfig = null;

    public TextMeshProUGUISettings TextConfig = null;
    public OvTextSettings TextEngineConfig = null;
    public ImageSettings ImageConfig = null;
    public BoxCollider2DSettings BoxCollider2DConfig = null;
    public Rigidbody2DSettings Rigidbody2DConfig = null;
    public MaskSettings MaskConfig = null;
    public ShadowSettings ShadowConfig = null;
    public OutlineSettings OutlineConfig = null;
    public bool HasRectMask2D = false;
    public bool RectMask2DEnabled = true;

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
        if(BoxCollider2DConfig != null) {
            obj[nameof(BoxCollider2DConfig)] = BoxCollider2DConfig.Serialize();
        }
        if(Rigidbody2DConfig != null) {
            obj[nameof(Rigidbody2DConfig)] = Rigidbody2DConfig.Serialize();
        }
        if(ContentSizeFitterConfig != null) {
            obj[nameof(ContentSizeFitterConfig)] = ContentSizeFitterConfig.Serialize();
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
            if(!RectMask2DEnabled) {
                obj[nameof(RectMask2DEnabled)] = false;
            }
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
        var contentSizeFitterProperty = obj.Property(nameof(ContentSizeFitterConfig));
        var contentSizeFitter = contentSizeFitterProperty?.Value;
        if(contentSizeFitterProperty == null || contentSizeFitter?.Type == JTokenType.Null) {
            ContentSizeFitterConfig = null;
        } else {
            ContentSizeFitterConfig ??= new ContentSizeFitterSettings();
            ContentSizeFitterConfig.Deserialize(contentSizeFitter);
        }
        TextConfig = ReadConfig<TextMeshProUGUISettings>(obj, nameof(TextConfig));
        TextEngineConfig = ReadConfig<OvTextSettings>(obj, nameof(TextEngineConfig));
        if(TextConfig != null) {
            TextEngineConfig ??= OvTextSettings.FromLegacy(TextConfig.Text);
        } else {
            TextEngineConfig = null;
        }
        ImageConfig = ReadConfig<ImageSettings>(obj, nameof(ImageConfig));
        BoxCollider2DConfig = ReadConfig<BoxCollider2DSettings>(obj, nameof(BoxCollider2DConfig));
        Rigidbody2DConfig = ReadConfig<Rigidbody2DSettings>(obj, nameof(Rigidbody2DConfig));
        MaskConfig = ReadConfig<MaskSettings>(obj, nameof(MaskConfig));
        ShadowConfig = ReadConfig<ShadowSettings>(obj, nameof(ShadowConfig));
        OutlineConfig = ReadConfig<OutlineSettings>(obj, nameof(OutlineConfig));
        HasRectMask2D = IOUtils.Read(obj, nameof(HasRectMask2D), HasRectMask2D);
        RectMask2DEnabled = IOUtils.Read(obj, nameof(RectMask2DEnabled), RectMask2DEnabled);
    }

    public OvObjectSettings Copy() {
        return new OvObjectSettings {
            Name = Name,
            Enabled = Enabled,
            RectTransformConfig = RectTransformConfig?.Copy(),
            CanvasGroupConfig = CanvasGroupConfig?.Copy(),
            ContentSizeFitterConfig = ContentSizeFitterConfig?.Copy(),
            TextConfig = TextConfig?.Copy(),
            TextEngineConfig = TextEngineConfig?.Copy(),
            ImageConfig = ImageConfig?.Copy(),
            BoxCollider2DConfig = BoxCollider2DConfig?.Copy(),
            Rigidbody2DConfig = Rigidbody2DConfig?.Copy(),
            MaskConfig = MaskConfig?.Copy(),
            ShadowConfig = ShadowConfig?.Copy(),
            OutlineConfig = OutlineConfig?.Copy(),
            HasRectMask2D = HasRectMask2D,
            RectMask2DEnabled = RectMask2DEnabled
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
