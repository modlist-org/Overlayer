using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;

namespace Overlayer.IO.Unity;

public class Texture2DSettings() : ISettingsFile, ICopyable<Texture2DSettings> {
    public bool MipChain = false;
    public bool Linear = false;

    public JToken Serialize() {
        return new JObject {
            [nameof(MipChain)] = MipChain,
            [nameof(Linear)] = Linear
        };
    }

    public void Deserialize(JToken token) {
        MipChain = IOUtils.Read(token, nameof(MipChain),  MipChain);
        Linear = IOUtils.Read(token, nameof(Linear), Linear);
    }

    public Texture2DSettings Copy() {
        return new Texture2DSettings {
            MipChain = MipChain,
            Linear = Linear
        };
    }
}