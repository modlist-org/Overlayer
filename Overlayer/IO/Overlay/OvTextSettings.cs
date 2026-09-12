using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;

namespace Overlayer.IO.Overlay;

public sealed class OvTextSettings : ISettingsFile, ICopyable<OvTextSettings> {
    public FxValue<string> PlayingText = FxValue<string>.FromValue("Text");
    public FxValue<string> NotPlayingText = FxValue<string>.FromValue("Text");

    public JToken Serialize() => new JObject {
        [nameof(PlayingText)] = IOUtils.WriteFx(PlayingText),
        [nameof(NotPlayingText)] = IOUtils.WriteFx(NotPlayingText)
    };

    public void Deserialize(JToken token) {
        PlayingText = IOUtils.ReadFx(token, nameof(PlayingText), PlayingText);
        NotPlayingText = IOUtils.ReadFx(token, nameof(NotPlayingText), NotPlayingText);
    }

    public OvTextSettings Copy() => new() {
        PlayingText = PlayingText?.Copy(),
        NotPlayingText = NotPlayingText?.Copy()
    };

    public static OvTextSettings FromLegacy(string text) => new() {
        PlayingText = FxValue<string>.FromValue(text ?? string.Empty),
        NotPlayingText = FxValue<string>.FromValue(text ?? string.Empty)
    };
}
