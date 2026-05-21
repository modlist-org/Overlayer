using Newtonsoft.Json.Linq;

namespace Overlayer.IO.Interface;

public interface ISettingsFile {
    JToken Serialize();

    void Deserialize(JToken token);
}