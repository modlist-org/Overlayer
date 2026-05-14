namespace Overlayer.Resource;

internal class ResourceLoader {
    public static byte[] Load(string resourceName) {
        using Stream stream = Core.OverlayerAssembly.GetManifestResourceStream(resourceName);

        if(stream == null) {
            return null;
        }

        byte[] data = new byte[stream.Length];
        stream.Read(data, 0, (int)stream.Length);
        return data;
    }
}