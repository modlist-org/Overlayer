using Overlayer.Core;

namespace Overlayer.Resource;

public static class ResourceLoader {
    public static byte[] Load(string resourceName) {
        if(string.IsNullOrWhiteSpace(resourceName)) {
            return null;
        }

        try {
            using var stream = MainCore.Assembly?.GetManifestResourceStream(resourceName);

            if(stream == null) {
                MainCore.Logger.Wrn($"Resource not found: {resourceName}");
                return null;
            }

            if(stream.Length <= 0) {
                return [];
            }

            byte[] data = new byte[stream.Length];

            int offset = 0;

            while(offset < data.Length) {
                int read = stream.Read(
                    data,
                    offset,
                    data.Length - offset
                );

                if(read <= 0) {
                    break;
                }

                offset += read;
            }

            if(offset == data.Length) {
                return data;
            }

            MainCore.Logger.Wrn($"Incomplete resource read: {resourceName}");
            return null;

        } catch(Exception e) {
            MainCore.Logger.Err($"Failed to load resource '{resourceName}': {e}");
            return null;
        }
    }
}