using System;

namespace Overlayer.MoiduleAPI;

public interface IUiInitializable {
    void OnInitUI(object uiManager);
}

public abstract class OverlayerModule {
    public abstract string Name { get; }
    public abstract string Version { get; }

    public abstract void OnInitialize();

    public virtual void OnModEnabledChanged(bool enabled, bool isDispose) { }
    public virtual void OnDispose() { }
}
