using UnityEngine;

namespace Overlayer.UI.UISprites;

public static class SpriteFactory {
    public static Sprite Create(Texture2D tex) {
        return Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f)
        );
    }

    public static Sprite CreateSliced(Texture2D tex, float ppui, Vector4 border) {
        return Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            ppui,
            0,
            SpriteMeshType.FullRect,
            border
        );
    }
}