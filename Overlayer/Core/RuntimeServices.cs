using Overlayer.Compat.Interface;

namespace Overlayer.Core;

public sealed class RuntimeServices {
    private readonly List<IRuntimeService> services = [];

    public void Add(IRuntimeService service) => services.Add(service);

    public void Initialize() {
        foreach(var service in services) {
            service.Initialize();
        }
    }

    public void Dispose() {
        for(int i = services.Count - 1; i >= 0; i--) {
            services[i].Dispose();
        }
    }
}