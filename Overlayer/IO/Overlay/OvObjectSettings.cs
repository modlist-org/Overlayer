using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using Overlayer.IO.UnityComponent.Impl;
namespace Overlayer.IO.Overlay;

public sealed class OvObjectSettings : ISettingsFile, ICopyable<OvObjectSettings> {
    public FxValue<string> Name = FxValue<string>.FromValue("OvObject");
    public FxValue<bool> Enabled = new(true);

    public RectTransformSettings RectTransformConfig = new();
    public CanvasGroupSettings CanvasGroupConfig = new();
    public ContentSizeFitterSettings ContentSizeFitterConfig = null;

    public TextMeshProUGUISettings TextConfig = null;
    public OvTextSettings TextEngineConfig = null;
    public MovingManSettings MovingManConfig = null;
    public ColorRangeSettings ColorRangeConfig = null;
    public ImageSettings ImageConfig = null;
    public MaskSettings MaskConfig = null;
    public ShadowSettings ShadowConfig = null;
    public OutlineSettings OutlineConfig = null;
    public FxValue<bool> HasRectMask2D = new(false);
    public FxValue<bool> RectMask2DEnabled = new(true);
#if !IL2CPP
    public BoxCollider2DSettings BoxCollider2DConfig = null;
    public Rigidbody2DSettings Rigidbody2DConfig = null;
#endif

    public bool HasAnyFx => FxUtil.HasFx(Name)
        || FxUtil.HasFx(Enabled)
        || (RectTransformConfig?.HasAnyFx ?? false)
        || (CanvasGroupConfig?.HasAnyFx ?? false)
        || (ContentSizeFitterConfig?.HasAnyFx ?? false)
        || (TextConfig?.HasAnyFx ?? false)
        || FxUtil.HasFx(TextEngineConfig?.PlayingText)
        || FxUtil.HasFx(TextEngineConfig?.NotPlayingText)
        || (MovingManConfig?.HasAnyFx ?? false)
        || (ColorRangeConfig?.HasAnyFx ?? false)
        || (ImageConfig?.HasAnyFx ?? false)
        || (MaskConfig?.HasAnyFx ?? false)
        || (ShadowConfig?.HasAnyFx ?? false)
        || (OutlineConfig?.HasAnyFx ?? false)
        || FxUtil.HasFx(HasRectMask2D)
        || FxUtil.HasFx(RectMask2DEnabled)
#if !IL2CPP
        || (BoxCollider2DConfig?.HasAnyFx ?? false)
        || (Rigidbody2DConfig?.HasAnyFx ?? false)
#endif
        ;

    public JToken Serialize() {
        var obj = new JObject {
            [nameof(Name)] = IOUtils.WriteFx(Name),
            [nameof(RectTransformConfig)] = RectTransformConfig?.Serialize(),
            [nameof(CanvasGroupConfig)] = CanvasGroupConfig?.Serialize(),
        };
        if(Enabled.UseFx || !Enabled.Value) {
            obj[nameof(Enabled)] = IOUtils.WriteFx(Enabled);
        }
        if(TextConfig != null) {
            obj[nameof(TextConfig)] = TextConfig.Serialize();
            obj[nameof(TextEngineConfig)] = (TextEngineConfig ?? OvTextSettings.FromLegacy(TextConfig.Text.Value)).Serialize();
        }
        if(MovingManConfig != null) {
            obj[nameof(MovingManConfig)] = MovingManConfig.Serialize();
        }
        if(ColorRangeConfig != null) {
            obj[nameof(ColorRangeConfig)] = ColorRangeConfig.Serialize();
        }
        if(ImageConfig != null) {
            obj[nameof(ImageConfig)] = ImageConfig.Serialize();
        }
#if !IL2CPP
        if(BoxCollider2DConfig != null) {
            obj[nameof(BoxCollider2DConfig)] = BoxCollider2DConfig.Serialize();
        }
        if(Rigidbody2DConfig != null) {
            obj[nameof(Rigidbody2DConfig)] = Rigidbody2DConfig.Serialize();
        }
#endif
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
        if(HasRectMask2D.UseFx || HasRectMask2D.Value) {
            obj[nameof(HasRectMask2D)] = IOUtils.WriteFx(HasRectMask2D);
            if(RectMask2DEnabled.UseFx || !RectMask2DEnabled.Value) {
                obj[nameof(RectMask2DEnabled)] = IOUtils.WriteFx(RectMask2DEnabled);
            }
        }
        return obj;
    }

    public void Deserialize(JToken token) {
        if(token is not JObject obj) {
            return;
        }

        Name = IOUtils.ReadFx(obj, nameof(Name), Name);
        Enabled = IOUtils.ReadFx(obj, nameof(Enabled), Enabled);
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
            TextEngineConfig ??= OvTextSettings.FromLegacy(TextConfig.Text.Value);
        } else {
            TextEngineConfig = null;
        }
        MovingManConfig = ReadConfig<MovingManSettings>(obj, nameof(MovingManConfig));
        ColorRangeConfig = ReadConfig<ColorRangeSettings>(obj, nameof(ColorRangeConfig));
        ImageConfig = ReadConfig<ImageSettings>(obj, nameof(ImageConfig));
#if !IL2CPP
        BoxCollider2DConfig = ReadConfig<BoxCollider2DSettings>(obj, nameof(BoxCollider2DConfig));
        Rigidbody2DConfig = ReadConfig<Rigidbody2DSettings>(obj, nameof(Rigidbody2DConfig));
#endif
        MaskConfig = ReadConfig<MaskSettings>(obj, nameof(MaskConfig));
        ShadowConfig = ReadConfig<ShadowSettings>(obj, nameof(ShadowConfig));
        OutlineConfig = ReadConfig<OutlineSettings>(obj, nameof(OutlineConfig));
        HasRectMask2D = IOUtils.ReadFx(obj, nameof(HasRectMask2D), HasRectMask2D);
        RectMask2DEnabled = IOUtils.ReadFx(obj, nameof(RectMask2DEnabled), RectMask2DEnabled);
    }

    public OvObjectSettings Copy() {
        return new OvObjectSettings {
            Name = Name?.Copy(),
            Enabled = Enabled?.Copy(),
            RectTransformConfig = RectTransformConfig?.Copy(),
            CanvasGroupConfig = CanvasGroupConfig?.Copy(),
            ContentSizeFitterConfig = ContentSizeFitterConfig?.Copy(),
            TextConfig = TextConfig?.Copy(),
            TextEngineConfig = TextEngineConfig?.Copy(),
            MovingManConfig = MovingManConfig?.Copy(),
            ColorRangeConfig = ColorRangeConfig?.Copy(),
            ImageConfig = ImageConfig?.Copy(),
#if !IL2CPP
            BoxCollider2DConfig = BoxCollider2DConfig?.Copy(),
            Rigidbody2DConfig = Rigidbody2DConfig?.Copy(),
#endif
            MaskConfig = MaskConfig?.Copy(),
            ShadowConfig = ShadowConfig?.Copy(),
            OutlineConfig = OutlineConfig?.Copy(),
            HasRectMask2D = HasRectMask2D?.Copy(),
            RectMask2DEnabled = RectMask2DEnabled?.Copy()
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
