namespace Overlayer.ModuleAPI;

public abstract class OverlayerModule {
    public abstract string Name { get; }
    public abstract string Author { get; }
    public abstract string Version { get; }

    public abstract void OnInitialize();

    /// <summary>The host app's own UI language as a code like "ko-KR", or null. Asked once, right
    /// after <see cref="OnInitialize"/> on a fresh Overlayer install, to start in that language;
    /// a code Overlayer has no translation for keeps the default (English).</summary>
    public virtual string GetPreferredLanguage() => null;

    public virtual void OnModEnabledChanged(bool enabled, bool isDispose) { }
    public virtual void OnDispose() { }
}
