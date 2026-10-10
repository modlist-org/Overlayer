using Overlayer.Compat.Interface;

namespace Overlayer.Core;

public sealed class RuntimeServices {
    private readonly List<IRuntimeService> services = [];

    public void Add(IRuntimeService service) => services.Add(service);

    /// <summary>
    /// Initializes each service in isolation: one failing service must not abort
    /// the rest of the mod (e.g. V8 native missing on an exotic title).
    /// </summary>
    public void Initialize() {
        foreach (var service in services) {
            try {
                service.Initialize();
            } catch (Exception ex) {
                Report($"[RuntimeServices] {service.GetType().Name} init failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public void Dispose() {
        for (int i = services.Count - 1; i >= 0; i--) {
            try {
                services[i].Dispose();
            } catch (Exception ex) {
                Report($"[RuntimeServices] {services[i].GetType().Name} dispose failed: {ex.Message}");
            }
        }
    }

    private static void Report(string message) {
        try {
            MainCore.Log?.Err(message);
            return;
        } catch {
        }
        try {
            Console.WriteLine(message);
        } catch {
        }
    }
}