using System;

namespace Overlayer.ModuleAPI;

public abstract class OverlayerModule {
    public abstract string Name { get; }
    public abstract string Author { get; }
    public abstract string Version { get; }

    public abstract void OnInitialize();

    public virtual void OnModEnabledChanged(bool enabled, bool isDispose) { }
    public virtual void OnDispose() { }
}
