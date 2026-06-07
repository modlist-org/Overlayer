using UnityEngine;

#if IL2CPP
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif

namespace Overlayer.Compat.OVC;

public static class OVC_Texture2D {
    public static bool LoadImage(Texture2D tex, byte[] data, bool markNonReadable = false) {
#if IL2CPP
        Il2CppStructArray<byte> il2cppData = new(data.Length);
        for(int i = 0; i < data.Length; i++) {
            il2cppData[i] = data[i];
        }
        return tex.LoadImage(il2cppData, markNonReadable);
#else
        return tex.LoadImage(data, markNonReadable);
#endif
    }
}
