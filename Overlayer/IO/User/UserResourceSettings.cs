using Newtonsoft.Json.Linq;
using Overlayer.Core;
using Overlayer.IO.Interface;
using Overlayer.IO.User.Impl;

namespace Overlayer.IO.User;

public sealed class UserResourceSettings : ISettingsFile, IDisposable {
    public UserTexture2D T2D { get; } = new();
    public UserSprite Spr { get; } = new();
    public UserFont Fnt { get; } = new();

    public JToken Serialize() {
        return new JObject {
            [nameof(UserTexture2D)] = T2D.Serialize(),
            [nameof(UserSprite)] = Spr.Serialize(),
            [nameof(UserFont)] = Fnt.Serialize()
        };
    }

    public void Deserialize(JToken token) {
        if(token is not JObject obj) {
            MainCore.Logger.Wrn($"[{nameof(UserResourceSettings)}] Deserialize failed: token is not JObject");
            return;
        }

        T2D.Deserialize(obj[nameof(UserTexture2D)]);
        Spr.Deserialize(obj[nameof(UserSprite)]);
        Fnt.Deserialize(obj[nameof(UserFont)]);
    }

    public void Dispose() {
        T2D.Dispose();
        Spr.Dispose();
        Fnt.Dispose();
    }
}