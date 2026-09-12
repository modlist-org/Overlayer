using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;
using UnityEngine;

namespace Overlayer.IO.Unity;

public class SpriteSettings : ISettingsFile, ICopyable<SpriteSettings> {
    public FxValue<Rect> Rect = new(UnityEngine.Rect.zero);
    public FxValue<Vector2> Pivot = new(new Vector2(0.5f, 0.5f));
    public FxValue<float> PixelsPerUnit = new(100f);
    public FxValue<Vector4> Border = new(Vector4.zero);

    public void FromUnity(Sprite sprite) {
        Rect.Value = sprite.rect;
        Pivot.Value = sprite.pivot;
        PixelsPerUnit.Value = sprite.pixelsPerUnit;
        Border.Value = sprite.border;
    }

    public Sprite ToUnity(Texture2D texture) {
        return Sprite.Create(
            texture,
            Rect.Value,
            Pivot.Value,
            PixelsPerUnit.Value,
            0,
            SpriteMeshType.FullRect,
            Border.Value,
            false
        );
    }

    public JToken Serialize() {
        return new JObject {
            [nameof(Rect)] = IOUtils.WriteFx(Rect),
            [nameof(Pivot)] = IOUtils.WriteFx(Pivot),
            [nameof(PixelsPerUnit)] = IOUtils.WriteFx(PixelsPerUnit),
            [nameof(Border)] = IOUtils.WriteFx(Border)
        };
    }

    public void Deserialize(JToken token) {
        Rect = IOUtils.ReadFx(token, nameof(Rect), Rect);
        Pivot = IOUtils.ReadFx(token, nameof(Pivot), Pivot);
        PixelsPerUnit = IOUtils.ReadFx(token, nameof(PixelsPerUnit), PixelsPerUnit);
        Border = IOUtils.ReadFx(token, nameof(Border), Border);
    }

    public SpriteSettings Copy() {
        return new SpriteSettings {
            Rect = Rect?.Copy(),
            Pivot = Pivot?.Copy(),
            PixelsPerUnit = PixelsPerUnit?.Copy(),
            Border = Border?.Copy()
        };
    }
}
