using Overlayer.Compat.Interface;
using Overlayer.UI;

namespace Overlayer.Core.Service;

public sealed class UIService : IRuntimeService, IRuntimeTick {
    public void Initialize() {
        Overlayer.Compat.O5KitAdapters.Setup();
        UICore.Initialize();
    }

    /// <summary>Rebuilds UI state after a scene wipe destroyed our objects.</summary>
    public void Reinitialize() => UICore.Reinitialize();

    public void Dispose() => UICore.Dispose();
    public void Tick() => UICore.HandleUpdate();
}