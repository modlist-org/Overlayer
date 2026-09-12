using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;

namespace Overlayer.IO.Unity;

public class Texture2DSettings() : ISettingsFile, ICopyable<Texture2DSettings> {
    public FxValue<bool> MipChain = new(false);
    public FxValue<bool> Linear = new(false);

    public JToken Serialize() {
        return new JObject {
            [nameof(MipChain)] = IOUtils.WriteFx(MipChain),
            [nameof(Linear)] = IOUtils.WriteFx(Linear)
        };
    }

    public void Deserialize(JToken token) {
        MipChain = IOUtils.ReadFx(token, nameof(MipChain), MipChain);
        Linear = IOUtils.ReadFx(token, nameof(Linear), Linear);
    }

    public Texture2DSettings Copy() {
        return new Texture2DSettings {
            MipChain = MipChain?.Copy(),
            Linear = Linear?.Copy()
        };
    }
}
