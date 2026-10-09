namespace Overlayer.Patch.Safe;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class SafePatchAttribute(string id, string targetType, string targetMethod) : Attribute {
    public string Id { get; } = id;
    public string TargetType { get; } = targetType;
    public string TargetMethod { get; } = targetMethod;
}
