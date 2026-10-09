namespace Overlayer.ModuleAPI;

public static class PlaybackState {
    private static Func<bool> playingProvider = static () => false;
    private static Func<bool> pausedProvider = static () => false;

    public static bool IsPlaying {
        get {
            try {
                return playingProvider();
            } catch {
                return false;
            }
        }
    }

    public static bool IsPaused {
        get {
            try {
                return pausedProvider();
            } catch {
                return false;
            }
        }
    }

    public static IDisposable Register(Func<bool> isPlayingProvider) {
        playingProvider = isPlayingProvider ?? throw new ArgumentNullException(nameof(isPlayingProvider));
        return new Registration(isPlayingProvider);
    }

    public static IDisposable RegisterPaused(Func<bool> isPausedProvider) {
        pausedProvider = isPausedProvider ?? throw new ArgumentNullException(nameof(isPausedProvider));
        return new PausedRegistration(isPausedProvider);
    }

    private sealed class Registration(Func<bool> registeredProvider) : IDisposable {
        public void Dispose() {
            if (ReferenceEquals(playingProvider, registeredProvider)) {
                playingProvider = static () => false;
            }
        }
    }

    private sealed class PausedRegistration(Func<bool> registeredProvider) : IDisposable {
        public void Dispose() {
            if (ReferenceEquals(pausedProvider, registeredProvider)) {
                pausedProvider = static () => false;
            }
        }
    }
}
