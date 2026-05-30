using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;
namespace Overlayer.IO.UnityComponent.Impl;

public class MaskSettings : ISettingsFile {
    public bool ShowMaskGraphic = true;
    public bool UseSpriteMesh = false;

    public JToken Serialize() {
        return new JObject {
            [nameof(ShowMaskGraphic)] = ShowMaskGraphic,
            [nameof(UseSpriteMesh)] = UseSpriteMesh
        };
    }

    public void Deserialize(JToken token) {
        ShowMaskGraphic = IOUtils.Read(token, nameof(ShowMaskGraphic), ShowMaskGraphic);
        UseSpriteMesh = IOUtils.Read(token, nameof(UseSpriteMesh), UseSpriteMesh);
    }
}