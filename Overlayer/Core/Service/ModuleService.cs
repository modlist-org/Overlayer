using Overlayer.Compat;
using Overlayer.ModuleAPI;

namespace Overlayer.Core.Service;

public sealed class ModuleService(OverlayerLogger logger) : IDisposable {
    private readonly List<OverlayerModule> _loadedModules = [];

    public void DiscoverAndRegisterModules() {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach(var assembly in assemblies) {
            try {
                foreach(var type in assembly.GetTypes()) {
                    if(type.Namespace == null || !type.Namespace.StartsWith("Overlayer.Module")) {
                        continue;
                    }

                    if(!typeof(OverlayerModule).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract) {
                        continue;
                    }

                    var moduleInstance = (OverlayerModule)Activator.CreateInstance(type);
                    _loadedModules.Add(moduleInstance);
                    logger.Msg($"[ModuleService] Module found: {moduleInstance.Name} (v{moduleInstance.Version}) by {moduleInstance.Author}");
                }
            } catch(Exception ex) {
                logger.Err($"[ModuleService] Assembly scan failed ({assembly.FullName}): {ex.Message}");
            }
        }
    }

    public void InitializeAllModules() {
        foreach(var module in _loadedModules) {
            try {
                module.OnInitialize();
            } catch(Exception ex) {
                logger.Err($"[ModuleService] Failed to initialize core module ({module.Name}): {ex.Message}");
            }
        }
    }

    public void NotifyEnabledChanged(bool enabled, bool isDispose) {
        foreach(var module in _loadedModules) {
            try {
                module.OnModEnabledChanged(enabled, isDispose);
            } catch(Exception ex) {
                logger.Err($"[ModuleService] Failed to notify status change for module ({module.Name}): {ex.Message}");
            }
        }
    }

    public void Dispose() {
        foreach(var module in _loadedModules) {
            try {
                module.OnDispose();
            } catch(Exception ex) {
                logger.Err($"[ModuleService] Failed to dispose module ({module.Name}): {ex.Message}");
            }
        }

        _loadedModules.Clear();
    }
}