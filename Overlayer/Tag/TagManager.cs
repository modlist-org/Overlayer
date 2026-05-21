using System.Reflection;

namespace Overlayer.Tag;

public static class TagManager {
    private static Dictionary<string, Tag> tags = [];
    public static int Count => tags.Count;

    public static Tag Get(string name) => tags.TryGetValue(name, out Tag tag) ? tag : null!;
    public static void Set(Tag tag) => tags[tag.Name] = tag;
}