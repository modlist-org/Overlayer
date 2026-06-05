using Overlayer.IO.Interface;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Overlayer.IO.Unity;

public class SpriteSettings : ISettingsFile, ICopyable<SpriteSettings> {
    public Rect Rect = Rect.zero;
    public Vector2 Pivot = new(0.5f, 0.5f);
    public float PixelsPerUnit = 100f;
    public Vector4 Border = Vector4.zero;

    public void FromUnity(Sprite sprite) {
        Rect = sprite.rect;
        Pivot = sprite.pivot;
        PixelsPerUnit = sprite.pixelsPerUnit;
        Border = sprite.border;
    }

    public Sprite ToUnity(Texture2D texture) {
        return Sprite.Create(
            texture,
            Rect,
            Pivot,
            PixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            Border,
            false
        );
    }

    public JToken Serialize() {
        return new JObject {
            [nameof(Rect)] = IOUtils.Write(Rect),
            [nameof(Pivot)] = IOUtils.Write(Pivot),
            [nameof(PixelsPerUnit)] = PixelsPerUnit,
            [nameof(Border)] = IOUtils.Write(Border)
        };
    }

    public void Deserialize(JToken token) {
        Rect = IOUtils.Read(token, nameof(Rect), Rect);
        Pivot = IOUtils.Read(token, nameof(Pivot), Pivot);
        PixelsPerUnit = IOUtils.Read(token, nameof(PixelsPerUnit), PixelsPerUnit);
        Border = IOUtils.Read(token, nameof(Border), Border);
    }

    public SpriteSettings Copy() {
        return new SpriteSettings {
            Rect = Rect,
            Pivot = Pivot,
            PixelsPerUnit = PixelsPerUnit,
            Border = Border
        };
    }
}