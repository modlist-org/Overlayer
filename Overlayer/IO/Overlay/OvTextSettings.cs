using Newtonsoft.Json.Linq;
using Overlayer.IO.Fx;
using Overlayer.IO.Interface;

namespace Overlayer.IO.Overlay;

public sealed class OvTextSettings : ISettingsFile, ICopyable<OvTextSettings> {
    public FxValue<string> Text = FxValue<string>.FromValue("Text");

    public JToken Serialize() => new JObject {
        [nameof(Text)] = IOUtils.WriteFx(Text)
    };

    public void Deserialize(JToken token) {
        // Breaking: Playing/NotPlaying merged into Text.
        // Migrate legacy PlayingText, drop NotPlayingText.
        if (token["PlayingText"] != null) {
            Text = IOUtils.ReadFx(token, "PlayingText", Text);
        } else {
            Text = IOUtils.ReadFx(token, nameof(Text), Text);
        }
    }

    public OvTextSettings Copy() => new() {
        Text = Text?.Copy()
    };

    public static OvTextSettings FromLegacy(string text) => new() {
        Text = FxValue<string>.FromValue(text ?? string.Empty)
    };
}
