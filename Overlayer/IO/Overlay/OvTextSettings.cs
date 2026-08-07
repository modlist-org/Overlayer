using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;

namespace Overlayer.IO.Overlay;

public sealed class OvTextSettings : ISettingsFile, ICopyable<OvTextSettings> {
    public string PlayingText = "Text";
    public string NotPlayingText = "Text";

    public JToken Serialize() => new JObject {
        [nameof(PlayingText)] = PlayingText,
        [nameof(NotPlayingText)] = NotPlayingText
    };

    public void Deserialize(JToken token) {
        PlayingText = IOUtils.Read(token, nameof(PlayingText), PlayingText);
        NotPlayingText = IOUtils.Read(token, nameof(NotPlayingText), NotPlayingText);
    }

    public OvTextSettings Copy() => new() {
        PlayingText = PlayingText,
        NotPlayingText = NotPlayingText
    };

    public static OvTextSettings FromLegacy(string text) => new() {
        PlayingText = text ?? string.Empty,
        NotPlayingText = text ?? string.Empty
    };
}
