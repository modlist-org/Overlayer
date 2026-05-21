using Overlayer.Compat.Interface;

namespace Overlayer.Core;

public sealed class RuntimeTicks {
    private readonly List<IRuntimeTick> ticks = [];

    public void Add(IRuntimeTick tick) => ticks.Add(tick);

    public void Remove(IRuntimeTick tick) => ticks.Remove(tick);

    public void Tick() {
        foreach(var t in ticks) {
            t.Tick();
        }
    }
}