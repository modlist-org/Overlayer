namespace Overlayer.Core.Service;

public sealed class PathService(string rootPath) {
    public string RootPath { get; } = rootPath;

    public string ConfigPath => Path.Combine(RootPath, "Settings.json");
    public string LangPath => Path.Combine(RootPath, "Lang");
    public string TempPath => Path.Combine(RootPath, "Temp");
    public string ModulePath => Path.Combine(RootPath, "Module");

    public string UserResourcePath => Path.Combine(RootPath, "UserResources.json");

    public void Initialize() {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(LangPath);
        Directory.CreateDirectory(TempPath);
        Directory.CreateDirectory(ModulePath);
    }
}