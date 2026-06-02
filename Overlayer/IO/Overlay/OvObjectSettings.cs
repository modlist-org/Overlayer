using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
using Overlayer.IO.UnityComponent.Impl;
using Unity.Profiling.LowLevel.Unsafe;
namespace Overlayer.IO.Overlay;

public sealed class OvObjectSettings : ISettingsFile {
    public string Name = "OvObject";

    public RectTransformSettings RectTransformConfig = new();

    public TextMeshProUGUISettings TextConfig = null;
    public ImageSettings ImageConfig = null;
    public MaskSettings MaskConfig = null;
    public ShadowSettings ShadowConfig = null;
    public OutlineSettings OutlineConfig = null;
    public bool HasRectMask2D = false;

    public List<OvObjectSettings> Children = [];

    public JToken Serialize() {
        var obj = new JObject {
            [nameof(Name)] = Name,
            [nameof(RectTransformConfig)] = RectTransformConfig?.Serialize(),
            [nameof(Children)] = new JArray(Children.Select(x => x.Serialize()))
        };
        if(TextConfig != null) {
            obj[nameof(TextConfig)] = TextConfig.Serialize();
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
        var rect = obj[nameof(RectTransformConfig)];
        if(rect != null) {
            RectTransformConfig ??= new RectTransformSettings();
            RectTransformConfig.Deserialize(rect);
        }
        TextConfig = ReadConfig<TextMeshProUGUISettings>(obj, nameof(TextConfig));
        ImageConfig = ReadConfig<ImageSettings>(obj, nameof(ImageConfig));
        MaskConfig = ReadConfig<MaskSettings>(obj, nameof(MaskConfig));
        ShadowConfig = ReadConfig<ShadowSettings>(obj, nameof(ShadowConfig));
        OutlineConfig = ReadConfig<OutlineSettings>(obj, nameof(OutlineConfig));
        HasRectMask2D = IOUtils.Read(obj, nameof(HasRectMask2D), HasRectMask2D);

        Children.Clear();

        if(obj[nameof(Children)] is JArray arr) {
            foreach(var item in arr) {
                var child = new OvObjectSettings();
                child.Deserialize(item);
                Children.Add(child);
            }
        }
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