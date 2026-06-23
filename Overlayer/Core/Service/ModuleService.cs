using Overlayer.Compat;
using Overlayer.ModuleAPI;
using System.Reflection;

namespace Overlayer.Core.Service;

public sealed class ModuleService(OverlayerLogger logger) : IDisposable {
    private readonly OverlayerLogger _logger = logger;
    private readonly List<OverlayerModule> _loadedModules = [];

    public IReadOnlyList<OverlayerModule> LoadedModules => _loadedModules;

    public void DiscoverAndRegisterModules() {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.FullName.StartsWith("UnityEngine") && !a.FullName.StartsWith("Unity") && !a.FullName.StartsWith("System"));

        foreach(var assembly in assemblies) {
            try {
                Type[] types;
                try {
                    types = assembly.GetTypes();
                } catch(ReflectionTypeLoadException e) {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                foreach(var type in types) {
                    if(type.Namespace != null && type.Namespace.StartsWith("Overlayer.Module")) {
                        if(!typeof(OverlayerModule).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract) {
                            continue;
                        }

                        var moduleInstance = (OverlayerModule)Activator.CreateInstance(type);
                        _loadedModules.Add(moduleInstance);
                        _logger.Msg($"[{nameof(ModuleService)}] Module found: {moduleInstance.Name} (v{moduleInstance.Version}) by {moduleInstance.Author}");
                    }
                }
            } catch(Exception ex) {
                _logger.Err($"[{nameof(ModuleService)}] Failed to scan assembly {assembly.FullName}: {ex.Message}");
            }
        }
    }

    public void InitializeAllModules() {
        foreach(var module in _loadedModules) {
            try { module.OnInitialize(); } catch(Exception ex) { _logger.Err($"[{nameof(ModuleService)}] Failed to initialize core module ({module.Name}): {ex.Message}"); }
        }
    }

    public void NotifyEnabledChanged(bool enabled, bool isDispose) {
        foreach(var module in _loadedModules) {
            try { module.OnModEnabledChanged(enabled, isDispose); } catch(Exception ex) { _logger.Err($"[{nameof(ModuleService)}] Failed to notify status change for module ({module.Name}): {ex.Message}"); }
        }
    }

    public void Dispose() {
        foreach(var module in _loadedModules) {
            try { module.OnDispose(); } catch(Exception ex) { _logger.Err($"[{nameof(ModuleService)}] Failed to dispose module ({module.Name}): {ex.Message}"); }
        }
        _loadedModules.Clear();
    }
}