namespace Overlayer.ModuleAPI;

public static class PlaybackState {
    private static Func<bool> provider = static () => false;

    public static bool IsPlaying {
        get {
            try {
                return provider();
            } catch {
                return false;
            }
        }
    }

    public static IDisposable Register(Func<bool> isPlayingProvider) {
        if(isPlayingProvider == null) {
            throw new ArgumentNullException(nameof(isPlayingProvider));
        }
        provider = isPlayingProvider;
        return new Registration(isPlayingProvider);
    }

    private sealed class Registration(Func<bool> registeredProvider) : IDisposable {
        public void Dispose() {
            if(ReferenceEquals(provider, registeredProvider)) {
                provider = static () => false;
            }
        }
    }
}
