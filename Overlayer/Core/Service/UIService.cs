using Overlayer.Compat.Interface;
using Overlayer.UI;

namespace Overlayer.Core.Service;

public sealed class UIService : IRuntimeService, IRuntimeTick {
    public void Initialize() {
        Overlayer.Compat.O5KitAdapters.Setup();
        UICore.Initialize();
    }
    public void Dispose() => UICore.Dispose();
    public void Tick() => UICore.HandleUpdate();
}