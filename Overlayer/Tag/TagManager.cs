using Overlayer.Tag.Replace;
using System.Reflection;

namespace Overlayer.Tag;

public static class TagManager {
    private static Dictionary<string, Tag> tags = [];
    public static int Count => tags.Count;

    private static Task initTask = null!;

    public static Task InitializeAsync(Assembly asm) {
        if(initTask != null) {
            return initTask;
        }

        initTask = Task.Run(async () => {
            List<Tag> list = await TagLoader.LoadAsync(asm);
            Dictionary<string, Tag> dict = new(list.Count);
            foreach(Tag tag in list) {
                dict[tag.Name] = tag;
            }

            tags = dict;
        });

        return initTask;
    }

    public static Tag Get(string name) => tags.TryGetValue(name, out Tag tag) ? tag : null!;
    public static void Set(Tag tag) => tags[tag.Name] = tag;
}