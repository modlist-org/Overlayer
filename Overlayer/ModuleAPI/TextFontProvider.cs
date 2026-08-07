#if ML && IL2CPP
using Il2CppTMPro;
#else
using TMPro;
#endif

namespace Overlayer.ModuleAPI;

public static class TextFontProvider {
    private static Func<TMP_FontAsset> provider = static () => null;

    public static TMP_FontAsset Current {
        get {
            try {
                return provider();
            } catch {
                return null;
            }
        }
    }

    public static IDisposable Register(Func<TMP_FontAsset> fontProvider) {
        if(fontProvider == null) {
            throw new ArgumentNullException(nameof(fontProvider));
        }
        provider = fontProvider;
        return new Registration(fontProvider);
    }

    private sealed class Registration(Func<TMP_FontAsset> registeredProvider) : IDisposable {
        public void Dispose() {
            if(ReferenceEquals(provider, registeredProvider)) {
                provider = static () => null;
            }
        }
    }
}
