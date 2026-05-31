using Newtonsoft.Json.Linq;
using Overlayer.IO.Interface;

namespace Overlayer.IO.User;

public class UserResourceSettings : ISettingsFile {
    

    public JToken Serialize() {
        var obj = new JObject {
            
        };

        return obj;
    }

    public void Deserialize(JToken token) {
        
    }
}
